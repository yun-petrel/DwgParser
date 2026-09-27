using DwgParser.Core.IO;

namespace DwgParser.Core.Objects;

public static partial class DwgObjectStreamWriter
{
    private static bool CarriesTableStyleR2010ToAc1015(DwgObjectRecord rec, Target t)
        => t.Version == DwgVersion.AC1015
           && rec.SourceVersion >= DwgVersion.AC1021
           && rec.LogicalType == TypeTableStyle
           && rec.TableStyle is { Captured: true } or { DownReady: true }
           && rec.StringStreamStartBit > 0;

    private static bool CarryTableStyleR2010ToAc1015(BitWriter body, DwgObjectRecord rec,
        byte[] src, Target t, out BitWriter? handles)
    {
        handles = null;
        var ts = rec.TableStyle!;
        long hs = rec.HandleStreamStartBit;
        if (hs <= 0) return false;
        long rlIn = rec.SourceVersion.IsR2010Plus() ? 0 : 32;
        long start = rec.CarryStartBit + rlIn;
        long headEnd = ts.HeadEndBit;
        if (start <= 0 || headEnd <= start || headEnd > hs) return false;
        try
        {
            var br = new BitReader(src, 0);
            DwgObjectRecord.SeekTo(br, start);
            br.ReadHandle();
            long eedStart = br.BitPosition;
            if (!SkipEedWriter(br)) return false;
            int eedLen = (int)(br.BitPosition - eedStart);
            int nr = br.ReadBL();
            if (nr is < 0 or > 8) return false;
            bool xdicMissing = br.ReadB();
            if (rec.SourceVersion >= DwgVersion.AC1027) br.ReadB();
            if (br.BitPosition != headEnd) return false;

            CopyBits(body, src, start, (int)(eedStart - start));
            if (!TryWriteEedPreR2007Range(body, src, eedStart, eedLen))
                CopyBits(body, src, eedStart, eedLen);
            body.WriteBL(nr);

            var plan = new Records.TableStyleCarryR2010ToAc1018();
            if (!plan.TryCarry(body, src, headEnd, hs, rec, t)) return false;

            if (xdicMissing)
            {
                long commonEnd = ts.HsCommonEndBit;
                if (commonEnd <= hs || commonEnd > (long)rec.BodyEnd * 8) return false;
                var hw = new BitWriter(16);
                CopyBits(hw, src, hs, (int)(commonEnd - hs));
                if (!HandleStreamHasXdicSlot(hw.ToArray(), hw.BitPosition))
                    hw.WriteRC(0x30);
                hw.WriteHandleRef(5, ts.Tsty[1]);
                hw.WriteHandleRef(5, ts.Tsty[2]);
                hw.WriteHandleRef(5, ts.Tsty[0]);
                handles = hw;
            }
            if (DebugTrace)
                System.Console.Error.WriteLine(
                    $"TS15DN h={rec.Handle:X} {rec.SourceVersion}→AC1015 body={body.BitPosition}位 " +
                    $"from={start} headEnd={headEnd} hs={hs} nr={nr} xdicMiss={xdicMissing} " +
                    $"自出句柄流={(handles is null ? 0 : (int)handles.BitPosition)}位 " +
                    $"cap={ts.Captured} name=\"{ts.Name}\"");
            return true;
        }
        catch { return false; }
    }

}
