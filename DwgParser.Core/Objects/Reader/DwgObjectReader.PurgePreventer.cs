using DwgParser.Core.IO;
using DwgParser.Core.Objects.Records;

namespace DwgParser.Core.Objects;

public static partial class DwgObjectReader
{
    private static void TryDecodePurgePreventer(BitReader shared, DwgVersion version,
        DwgObjectRecord? record)
    {
        if (record is null) return;
        if (version != DwgVersion.AC1015 && version != DwgVersion.AC1018) return;
        var name = (record.ClassName ?? "").ToUpperInvariant();
        if (!name.Contains("DYNAMICBLOCKPURGEPREVENTER")) return;

        record.PurgePreventer ??= new PurgePreventerCapture();
        var cap = record.PurgePreventer;
        try
        {
            var br = new BitReader(shared.UnderlyingData(), 0) { UnicodeStrings = false };
            DwgObjectRecord.SeekTo(br, record.CarryStartBit);
            long dataStart = br.BitPosition;
            uint rl = br.ReadRL();
            long dataEnd = (long)record.BodyStart * 8 + rl;
            cap.SourceDataBits = (int)rl;
            if (dataEnd > (long)record.BodyEnd * 8) { FailPurgePreventer(cap, br, dataStart, "帧界"); return; }

            br.ReadHandle();
            if (!TrySkipEed(br)) { FailPurgePreventer(cap, br, dataStart, "EED"); return; }
            if (br.ReadBL() is < 0 or > 100_000)
            {
                FailPurgePreventer(cap, br, dataStart, "num_reactors");
                return;
            }
            if (version >= DwgVersion.AC1018) br.ReadB();

            cap.TailBits = (int)(dataEnd - br.BitPosition);
            short flag = br.ReadBS();
            if (br.BitPosition != dataEnd)
            {
                FailPurgePreventer(cap, br, dataStart, $"停点 {br.BitPosition - dataEnd}");
                return;
            }

            cap.Flag = flag;
            cap.WalkEndBit = br.BitPosition;
            cap.FailReason = "";
            cap.Captured = true;
            if (System.Environment.GetEnvironmentVariable("DWGDBG") == "1")
                System.Console.Error.WriteLine(
                    $"DBPP h={record.Handle:X} v={version} rl={rl} tail={cap.TailBits} flag={flag}");
        }
        catch (Exception e)
        {
            cap.Captured = false;
            if (string.IsNullOrEmpty(cap.FailReason))
                FailPurgePreventer(cap, shared, record.CarryStartBit, e.GetType().Name);
        }
    }

    private static void FailPurgePreventer(PurgePreventerCapture cap, BitReader br, long dataStart, string why)
    {
        cap.Captured = false;
        cap.FailReason = $"DBPP@{why}:";
        cap.FailBit = br.BitPosition - dataStart;
    }
}
