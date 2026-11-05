using DwgParser.Core.IO;

namespace DwgParser.Core.Objects;

public static partial class DwgObjectStreamWriter
{
    private static bool HatchR2010CrossRelevant(DwgObjectRecord rec, Target t)
        => rec.LogicalType == TypeHatch
           && rec.SourceVersion >= DwgVersion.AC1021 && t.UnicodeStrings
           && rec.SourceVersion.IsR2010Plus() != t.R2010Plus;

    internal static List<BitInsert>? LocateHatchFitInsertsForPreR2007Source(DwgObjectRecord rec,
        byte[] src, Target t, long geoStart, int geoLen)
    {
        if (rec.LogicalType != TypeHatch) return null;
        if (rec.SourceVersion > DwgVersion.AC1018 || !t.R2010Plus || !t.UnicodeStrings) return null;
        long geoEnd = geoStart + geoLen;
        if (geoLen <= 0 || geoEnd > (long)src.Length * 8) return null;
        long walkFrom = rec.SourceVersion == DwgVersion.AC1015
            ? geoStart : rec.HatchGradientEndBit;
        if (walkFrom < geoStart || walkFrom >= geoEnd) return null;
        var anchors = new List<BitInsert>();
        try
        {
            var br = new BitReader(src, 0);
            DwgObjectRecord.SeekTo(br, walkFrom);
            br.ReadBD();
            br.Read3BD();
            br.ReadTV();
            br.ReadB();
            br.ReadB();
            long numPaths = br.ReadBL();
            if (numPaths is < 0 or > 1000) return null;
            for (int p = 0; p < numPaths; p++)
            {
                long flag = br.ReadBL();
                if ((flag & 2) == 0)
                {
                    long numSegs = br.ReadBL();
                    if (numSegs is < 0 or > 100000) return null;
                    for (int s = 0; s < numSegs; s++)
                    {
                        switch (br.ReadRC())
                        {
                            case 1:
                                br.Read2RD(); br.Read2RD();
                                break;
                            case 2:
                                br.Read2RD(); br.ReadBD(); br.ReadBD(); br.ReadBD(); br.ReadB();
                                break;
                            case 3:
                                br.Read2RD(); br.Read2RD();
                                br.ReadBD(); br.ReadBD(); br.ReadBD(); br.ReadB();
                                break;
                            case 4:
                            {
                                br.ReadBL();
                                bool rational = br.ReadB();
                                br.ReadB();
                                long numKnots = br.ReadBL();
                                long numCtrl = br.ReadBL();
                                if (numKnots is < 0 or > 100000 || numCtrl is < 0 or > 100000)
                                    break;
                                for (int k = 0; k < numKnots; k++) br.ReadBD();
                                for (int k = 0; k < numCtrl; k++)
                                {
                                    br.Read2RD();
                                    if (rational) br.ReadBD();
                                }
                                if (br.BitPosition >= geoEnd) break;
                                anchors.Add(new BitInsert(br.BitPosition, "10"));
                                break;
                            }
                            default:
                                goto doneSegs;
                        }
                    }
                    doneSegs:
                    ;
                }
                else
                {
                    bool bulges = br.ReadB();
                    br.ReadB();
                    long numPts = br.ReadBL();
                    if (numPts is < 0 or > 100000) return null;
                    for (int k = 0; k < numPts; k++)
                    {
                        br.Read2RD();
                        if (bulges) br.ReadBD();
                    }
                }
                br.ReadBL();
            }
            if (br.BitPosition > geoEnd && anchors.Count == 0) return null;
        }
        catch
        {
            if (anchors.Count == 0) return null;
        }
        if (anchors.Count == 0) return null;
        if (DebugTrace)
            System.Console.Error.WriteLine(
                $"HTCHUP h={rec.Handle:X} {rec.SourceVersion}→{t.Version} "
                + $"splines={anchors.Count} geoBits={geoLen}→{geoLen + 2 * anchors.Count}");
        return anchors;
    }

    internal static bool CarryHatchR2010Cross(BitWriter w, DwgObjectRecord rec, byte[] src,
        long geoStart, int geoLen, Target t)
    {
        bool srcR2010 = rec.SourceVersion.IsR2010Plus();
        long geoEnd = geoStart + geoLen;
        if (geoLen <= 0 || geoEnd > (long)src.Length * 8) return false;
        if (srcR2010 == t.R2010Plus) return false;
        if (rec.SourceVersion < DwgVersion.AC1021 || !t.UnicodeStrings) return false;

        var tmp = new BitWriter(64);
        var fitBlocks = new List<(long Start, long End)>();
        try
        {
            var br = new BitReader(src, 0);
            DwgObjectRecord.SeekTo(br, geoStart);

            br.ReadBL();
            br.ReadBL();
            br.ReadBD();
            br.ReadBD();
            br.ReadBL();
            br.ReadBD();
            long numColors = br.ReadBL();
            if (numColors is < 0 or > 1000) return false;
            for (int i = 0; i < numColors; i++)
            {
                br.ReadBD();
                DwgObjectReader.ReadCmc(br, out _);
            }
            br.ReadBD();
            br.Read3BD();
            br.ReadB();
            br.ReadB();
            long numPaths = br.ReadBL();
            if (numPaths is < 0 or > 1000) return false;

            for (int p = 0; p < numPaths; p++)
            {
                long flag = br.ReadBL();
                if ((flag & 2) == 0)
                {
                    long numSegs = br.ReadBL();
                    if (numSegs is < 0 or > 100000) return false;
                    for (int s = 0; s < numSegs; s++)
                    {
                        switch (br.ReadRC())
                        {
                            case 1:
                                br.Read2RD(); br.Read2RD();
                                break;
                            case 2:
                                br.Read2RD(); br.ReadBD(); br.ReadBD(); br.ReadBD(); br.ReadB();
                                break;
                            case 3:
                                br.Read2RD(); br.Read2RD();
                                br.ReadBD(); br.ReadBD(); br.ReadBD(); br.ReadB();
                                break;
                            case 4:
                            {
                                br.ReadBL();
                                bool rational = br.ReadB();
                                br.ReadB();
                                long numKnots = br.ReadBL();
                                long numCtrl = br.ReadBL();
                                if (numKnots is < 0 or > 100000 || numCtrl is < 0 or > 100000)
                                    return false;
                                for (int k = 0; k < numKnots; k++) br.ReadBD();
                                for (int k = 0; k < numCtrl; k++)
                                {
                                    br.Read2RD();
                                    if (rational) br.ReadBD();
                                }
                                long fitStart = br.BitPosition;
                                if (srcR2010)
                                {
                                    long numFit = br.ReadBL();
                                    if (numFit is < 0 or > 100000) return false;
                                    if (numFit > 0)
                                    {
                                        for (int k = 0; k < numFit; k++) br.Read2RD();
                                        br.Read2RD();
                                        br.Read2RD();
                                    }
                                }
                                fitBlocks.Add((fitStart, br.BitPosition));
                                break;
                            }
                            default: return false;
                        }
                    }
                }
                else
                {
                    bool bulges = br.ReadB();
                    br.ReadB();
                    long numPts = br.ReadBL();
                    if (numPts is < 0 or > 100000) return false;
                    for (int k = 0; k < numPts; k++)
                    {
                        br.Read2RD();
                        if (bulges) br.ReadBD();
                    }
                }
                br.ReadBL();
            }
            if (br.BitPosition > geoEnd) return false;
            if (fitBlocks.Count == 0) return false;

            long pos = geoStart;
            bool up = !srcR2010;
            foreach (var (start, end) in fitBlocks)
            {
                if (start < pos || end > geoEnd || end < start) return false;
                CopyBits(tmp, src, pos, (int)(start - pos));
                if (up) tmp.WriteBL(0);
                pos = up ? start : end;
            }
            if (pos > geoEnd) return false;
            CopyBits(tmp, src, pos, (int)(geoEnd - pos));
        }
        catch
        {
            return false;
        }

        Append(w, tmp);
        if (DebugTrace)
            System.Console.Error.WriteLine(
                $"HTCH{(srcR2010 ? "DN" : "UP")} h={rec.Handle:X} {rec.SourceVersion}→{t.Version} " +
                $"splines={fitBlocks.Count} geoBits={geoLen}→{tmp.BitPosition}");
        return true;
    }

    internal static bool HatchDowngradesR2007(DwgObjectRecord rec, Target t)
        => rec.LogicalType == TypeHatch
           && rec.SourceVersion >= DwgVersion.AC1021 && !t.R2007Plus
           && rec.HatchPatternSlotBit > 0
           && t.Version == DwgVersion.AC1018;

    internal static bool CarryHatchDowngrade(BitWriter body, DwgObjectRecord rec, byte[] src,
        long geoStart, int geoLen, Target t)
    {
        if (!HatchDowngradesR2007(rec, t)) return false;
        long slot = rec.HatchPatternSlotBit;
        long geoEnd = geoStart + geoLen;
        long ss = rec.StringStreamStartBit;
        if (ss > geoStart && ss < geoEnd) geoEnd = ss;
        long grad = rec.HatchGradientEndBit;
        if (grad < geoStart || grad > slot || slot > geoEnd) return false;
        if (rec.HatchPatternName.Length > 255) return false;
        if (rec.HatchGradientName.Length > 255) return false;

        var spans = rec.SourceVersion.IsR2010Plus() && HtchSfVar() != 0
            ? rec.HatchR2010SplineSpans : null;
        int sfSpans = spans?.Count ?? 0, sfBits = spans?.Sum(s => s.Bits) ?? 0;
        if (spans is { Count: > 0 })
        {
            long prev = slot;
            foreach (var (st, bits) in spans)
            {
                if (st < prev || bits < 0 || st + bits > geoEnd) { spans = null; break; }
                prev = st + bits;
            }
        }
        CopyBits(body, src, geoStart, (int)(grad - geoStart));
        body.WriteTV(rec.HatchGradientName, false);
        CopyBits(body, src, grad, (int)(slot - grad));
        body.WriteTV(rec.HatchPatternName, false);
        if (spans is { Count: > 0 })
        {
            long from = slot;
            foreach (var (st, bits) in spans)
            {
                CopyBits(body, src, from, (int)(st - from));
                from = st + bits;
            }
            CopyBits(body, src, from, (int)(geoEnd - from));
        }
        else CopyBits(body, src, slot, (int)(geoEnd - slot));
        if (DebugTrace)
            System.Console.Error.WriteLine(
                $"HTCHDN h={rec.Handle:X} {rec.SourceVersion}→{t.Version} " +
                $"geo=({geoStart},{geoEnd - geoStart}) grad@{grad - geoStart} slot@{slot - geoStart} " +
                $"sfSpans={sfSpans} sfBits={sfBits} applied={(spans is { Count: > 0 } ? 1 : 0)} " +
                $"gradName=\"{rec.HatchGradientName}\" name=\"{rec.HatchPatternName}\"");
        return true;
    }

    private static int HtchSfVar()
    {
        var s = System.Environment.GetEnvironmentVariable("HTCHSF");
        return int.TryParse(s, out var v) ? v : 1;
    }
}
