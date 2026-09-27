using DwgParser.Core.IO;
using DwgParser.Core.Objects.Records;

namespace DwgParser.Core.Objects;

public static partial class DwgObjectReader
{
    private const short TypeVxTableRecord = 0x47;

    private static void TryDecodeVxTableRecord(BitReader shared, DwgVersion version,
        DwgObjectRecord? record, System.Text.Encoding? legacyEncoding)
    {
        if (record is null) return;
        if (version != DwgVersion.AC1015) return;

        record.VxTableRecord ??= new VxTableRecordCapture();
        var cap = record.VxTableRecord;
        try
        {
            var br = new BitReader(shared.UnderlyingData(), 0)
            {
                UnicodeStrings = false,
                LegacyEncoding = legacyEncoding,
            };
            DwgObjectRecord.SeekTo(br, record.CarryStartBit);
            long dataStart = br.BitPosition;
            uint rl = br.ReadRL();
            long dataEnd = (long)record.BodyStart * 8 + rl;
            cap.SourceDataBits = (int)rl;
            if (dataEnd > (long)record.BodyEnd * 8) { FailVxTableRecord(cap, br, dataStart, "帧界"); return; }

            br.ReadHandle();
            if (!TrySkipEed(br)) { FailVxTableRecord(cap, br, dataStart, "EED"); return; }
            long numReactors = br.ReadBL();
            if (numReactors is < 0 or > 100_000)
            {
                FailVxTableRecord(cap, br, dataStart, "num_reactors");
                return;
            }

            string name = br.ReadTV();
            bool xrefRef = br.ReadB();
            int xrefResolved = br.ReadBS();
            bool xrefDep = br.ReadB();
            bool isOn = br.ReadB();

            if (br.BitPosition != dataEnd)
            {
                FailVxTableRecord(cap, br, dataStart, $"停点 {br.BitPosition - dataEnd}");
                return;
            }

            cap.Name = name;
            cap.IsXrefRef = xrefRef;
            cap.XrefResolved = xrefResolved;
            cap.IsXrefDep = xrefDep;
            cap.IsOn = isOn;
            cap.WalkEndBit = br.BitPosition;

            var hb = new BitReader(shared.UnderlyingData(), 0) { UnicodeStrings = false };
            hb.SetPosition((int)(dataEnd >> 3), (int)(dataEnd & 7));
            hb.ReadHandle();
            for (long i = 0; i < numReactors; i++) hb.ReadHandle();
            hb.ReadHandle();
            cap.Xref = hb.ReadHandle().Resolve(record.Handle);
            cap.Viewport = hb.ReadHandle().Resolve(record.Handle);
            cap.PrevEntry = hb.ReadHandle().Resolve(record.Handle);

            long pad = (long)record.BodyEnd * 8 - hb.BitPosition;
            if (pad is < 0 or > 7)
            {
                FailVxTableRecord(cap, br, dataStart, $"句柄流余 {pad}");
                return;
            }

            cap.FailReason = "";
            cap.Captured = true;
        }
        catch (Exception e)
        {
            cap.Captured = false;
            if (string.IsNullOrEmpty(cap.FailReason))
                FailVxTableRecord(cap, shared, record.CarryStartBit, e.GetType().Name);
        }
    }

    private static void FailVxTableRecord(VxTableRecordCapture cap, BitReader br, long dataStart, string why)
    {
        cap.Captured = false;
        cap.FailReason = $"VXTR@{why}:";
        cap.FailBit = br.BitPosition - dataStart;
    }
}
