using System.IO;
using DwgParser.Core.IO;

namespace DwgParser.Core.Objects;

public static partial class DwgObjectStreamWriter
{
    private static bool CarriesViewStyleAc1018ToAc1015(DwgObjectRecord rec, Target t)
        => t.Version == DwgVersion.AC1015
           && rec.SourceVersion == DwgVersion.AC1018
           && rec.LogicalType is TypeDetailViewStyle or TypeSectionViewStyle
           && rec.HasHandleStream && rec.HandleStreamStartBit > 0;

    private static bool CarryViewStyleAc1018ToAc1015(BitWriter body, DwgObjectRecord rec,
        byte[] src, out BitWriter? handles)
    {
        handles = null;
        long start = rec.CarryStartBit + 32;
        long hs = rec.HandleStreamStartBit;
        if (start <= 0 || hs <= start) return false;
        var cmcs = LocateViewStyleAc1018CmcSpans(src, start, hs,
            section: rec.LogicalType == TypeSectionViewStyle);
        if (cmcs is null) return false;
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
            br.ReadB();
            long headEnd = br.BitPosition;
            if (headEnd > cmcs[0].Start) return false;

            CopyBits(body, src, start, (int)(eedStart - start));
            if (!TryWriteEedPreR2007Range(body, src, eedStart, eedLen))
                CopyBits(body, src, eedStart, eedLen);
            body.WriteBL(nr);
            long cur = headEnd;
            foreach (var (cs, ce) in cmcs)
            {
                if (cs < cur || ce <= cs || ce > hs) return false;
                CopyBits(body, src, cur, (int)(cs - cur));
                if (!WriteFoldedCmc(body, src, cs, ce)) return false;
                cur = ce;
            }
            CopyBits(body, src, cur, (int)(hs - cur));

            long hsEnd = LastMeaningfulHandleEnd(src, hs, (long)rec.BodyEnd * 8);
            if (hsEnd <= hs) return false;
            var hw = new BitWriter(16);
            CopyBits(hw, src, hs, (int)(hsEnd - hs));
            if (!HandleStreamHasXdicSlot(hw.ToArray(), hw.BitPosition)) hw.WriteRC(0x30);
            handles = hw;
            if (DebugTrace)
                System.Console.Error.WriteLine(
                    $"VSDNVS h={rec.Handle:X} t={rec.LogicalType} AC1018→AC1015 折 {cmcs.Count} 枚 CMC " +
                    $"body={body.BitPosition} hs={hw.BitPosition}位");
            return true;
        }
        catch { return false; }
    }

    private static List<(long Start, long End)>? LocateViewStyleAc1018CmcSpans(
        byte[] src, long start, long hs, bool section)
    {
        var br = new BitReader(src, 0);
        DwgObjectRecord.SeekTo(br, start);
        br.ReadHandle();
        if (!SkipEedWriter(br)) return null;
        if (br.ReadBL() is < 0 or > 8) return null;
        br.ReadB();
        if (br.ReadBS() != 0) return null;
        var cmcs = new List<(long, long)>(7);
        try
        {
            SkipTv(br);
            br.ReadB();
            if (br.ReadBS() != 0) return null;
            br.ReadBL();
            cmcs.Add(CmcSpan(br));
            br.ReadBD();
            if (section)
            {
                cmcs.Add(CmcSpan(br));
                br.ReadBD();
                SkipTv(br);
                br.ReadBD(); br.ReadBL();
                cmcs.Add(CmcSpan(br));
                br.ReadBL();
                cmcs.Add(CmcSpan(br));
                br.ReadBD(); br.ReadBD();
                cmcs.Add(CmcSpan(br));
                br.ReadBD(); br.ReadBL();
                br.ReadBD(); br.ReadBL();
                SkipTv(br);
                cmcs.Add(CmcSpan(br));
                cmcs.Add(CmcSpan(br));
                SkipTv(br);
                br.ReadBD();
                br.ReadBL(); br.ReadB(); br.ReadB();
                br.ReadBL(); br.ReadBD(); br.ReadBL();
                br.ReadBD();
                int n = br.ReadBL();
                if (n is < 0 or > 1024) return null;
                for (int k = 0; k < n; k++) br.ReadBD();
            }
            else
            {
                SkipTv(br);
                br.ReadBD();
                br.ReadRC();
                cmcs.Add(CmcSpan(br));
                br.ReadBD();
                br.ReadBL();
                cmcs.Add(CmcSpan(br));
                cmcs.Add(CmcSpan(br));
                br.ReadBD();
                br.ReadBL();
                br.ReadBD();
                br.ReadBL();
                SkipTv(br);
                br.ReadBL();
                cmcs.Add(CmcSpan(br));
                br.ReadBL();
                cmcs.Add(CmcSpan(br));
                br.ReadRC();
            }
        }        catch { return null; }
        return br.BitPosition == hs && cmcs.Count > 0 ? cmcs : null;
    }

    private static (long Start, long End) CmcSpan(BitReader br)
    {
        long s = br.BitPosition;
        short idx = br.ReadBS();
        br.ReadBL();
        br.ReadRC();
        if (idx is < 0 or > 256) throw new InvalidDataException($"CMC idx({idx})");
        return (s, br.BitPosition);
    }
}
