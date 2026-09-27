using DwgParser.Core.IO;

namespace DwgParser.Core.Objects;

public static partial class DwgObjectStreamWriter
{
    internal static void CopyCommonHeadWithEedDown(BitWriter body, byte[] src, long headStart,
        long patchEnd, DwgObjectRecord rec, Target t)
    {
        if (t.R2007Plus || rec.SourceVersion < DwgVersion.AC1021)
        {
            if (patchEnd > headStart) CopyBits(body, src, headStart, (int)(patchEnd - headStart));
            return;
        }
        CopyHeadWindowWithEedDown(body, src, headStart, patchEnd, rec);
    }

    internal static void CopyHeadWindowWithEedDown(BitWriter body, byte[] src, long headStart,
        long patchEnd, DwgObjectRecord rec)
    {
        if (patchEnd <= headStart) return;
        long eedS = -1; int eedL = 0;
        bool eedDown = TryMeasureEedRange(src, rec, out eedS, out eedL)
            && eedS >= headStart && eedS + eedL <= patchEnd;
        if (!eedDown)
        {
            CopyBits(body, src, headStart, (int)(patchEnd - headStart));
            return;
        }
        CopyBits(body, src, headStart, (int)(eedS - headStart));
        if (!TryWriteEedPreR2007Range(body, src, eedS, eedL))
            CopyBits(body, src, eedS, eedL);
        CopyBits(body, src, eedS + eedL, (int)(patchEnd - eedS - eedL));
    }
}
