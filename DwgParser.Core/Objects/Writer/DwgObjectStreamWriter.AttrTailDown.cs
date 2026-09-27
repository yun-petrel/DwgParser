using DwgParser.Core.IO;

namespace DwgParser.Core.Objects;

public static partial class DwgObjectStreamWriter
{
    internal static bool AttrTailDowngradesToAc1018(DwgObjectRecord rec, Target t)
        => IsAttributeFamily(rec)
           && rec.SourceVersion >= DwgVersion.AC1021
           && t.Version == DwgVersion.AC1018
           && System.Environment.GetEnvironmentVariable("ATTDN_OFF") != "1"
           && rec.AttTailLockedBit > 0
           && rec.AttTailKeepDuplicateBit > rec.AttTailLockedBit;

    internal static bool CarryAttrTailDowngradeToAc1018(BitWriter body, DwgObjectRecord rec, byte[] src,
        long geoStart, int geoLen, Target t, string? hatchGradientName)
    {
        if (!AttrTailDowngradesToAc1018(rec, t)) return false;
        long geoEnd = geoStart + geoLen;
        long a1 = rec.AttTailLockedBit;
        long a2 = rec.AttTailKeepDuplicateBit;
        if (a1 <= geoStart || a2 <= a1 || a2 > geoEnd) return false;
        bool srcLocked = rec.SourceVersion >= DwgVersion.AC1024;
        bool srcMtext = rec.SourceVersion >= DwgVersion.AC1032;
        bool attdef = rec.LogicalType == TypeAttDef;
        long srcCur = a1 + (srcLocked ? 8 : 0) + (srcMtext ? 8 : 0);
        long keepLen = attdef && srcLocked ? 8 : 0;
        long tailStart = a2 + keepLen;
        if (srcCur > a2 - 1 || tailStart > geoEnd) return false;
        long ss = rec.StringStreamStartBit;
        long tailEnd = ss >= tailStart && ss < geoEnd ? ss : geoEnd;
        if (tailEnd < tailStart) return false;

        CarryWithStrings(body, rec, src, geoStart, (int)(a1 - geoStart), t, hatchGradientName);
        CopyBits(body, src, srcCur, (int)(a2 - 1 - srcCur));
        CarryWithStrings(body, rec, src, tailStart, (int)(tailEnd - tailStart), t, hatchGradientName);

        if (DebugTrace)
            System.Console.Error.WriteLine(
                $"ATTDN h={rec.Handle:X} {(attdef ? "ATTDEF" : "ATTRIB")} {rec.SourceVersion}→{t.Version} " +
                $"locked={srcLocked} mtext={srcMtext} keep={keepLen} " +
                $"strip=[{a1 - geoStart},{srcCur - geoStart})+[{a2 - 1 - geoStart},{a2 - geoStart})+" +
                $"[{a2 - geoStart},{tailStart - geoStart}) tail=[{tailStart - geoStart},{tailEnd - geoStart})");
        return true;
    }
}
