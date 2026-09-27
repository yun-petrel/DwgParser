using DwgParser.Core.IO;

namespace DwgParser.Core.Objects;

public static partial class DwgObjectStreamWriter
{
    internal static bool AttrTailDowngradesToAc1015(DwgObjectRecord rec, Target t)
        => IsAttributeFamily(rec)
           && rec.SourceVersion >= DwgVersion.AC1021
           && t.Version == DwgVersion.AC1015
           && System.Environment.GetEnvironmentVariable("ATTDN15_OFF") != "1"
           && rec.AttTailLockedBit > 0
           && rec.AttTailKeepDuplicateBit > rec.AttTailLockedBit
           && rec.StringStreamStartBit > 0;

    internal static bool CarryAttrTailDowngradeToAc1015(BitWriter body, DwgObjectRecord rec, byte[] src,
        long geoStart, int geoLen, Target t, string? hatchGradientName)
    {
        if (!AttrTailDowngradesToAc1015(rec, t)) return false;
        long geoEnd = geoStart + geoLen;
        long a1 = rec.AttTailLockedBit;
        long a2 = rec.AttTailKeepDuplicateBit;
        if (a1 <= geoStart || a2 <= a1 || a2 > geoEnd) return false;
        bool srcLocked = rec.SourceVersion >= DwgVersion.AC1024;
        bool srcMtext = rec.SourceVersion >= DwgVersion.AC1032;
        long srcCur = a1 + (srcLocked ? 8 : 0) + (srcMtext ? 8 : 0);
        long keepLen = rec.LogicalType == TypeAttDef && srcLocked ? 8 : 0;
        long tailStart = a2 + keepLen;
        if (srcCur > a2 - 1 || tailStart > geoEnd) return false;
        long ss = rec.StringStreamStartBit;
        if (ss < tailStart || ss > geoEnd) return false;

        CarryAc1015Window(body, rec, src, geoStart, a1);
        CopyBits(body, src, srcCur, (int)(a2 - 1 - srcCur));
        CarryAc1015Window(body, rec, src, tailStart, ss);

        if (DebugTrace)
            System.Console.Error.WriteLine(
                $"ATTDN15 h={rec.Handle:X} {(rec.LogicalType == TypeAttDef ? "ATTDEF" : "ATTRIB")} " +
                $"{rec.SourceVersion}→{t.Version} locked={srcLocked} mtext={srcMtext} keep={keepLen} " +
                $"strip=[{a1 - geoStart},{srcCur - geoStart})+[{a2 - 1 - geoStart},{a2 - geoStart})" +
                $"+[{a2 - geoStart},{tailStart - geoStart}) drop=[{ss - geoStart},{geoEnd - geoStart})");
        return true;
    }

    private static void CarryAc1015Window(BitWriter w, DwgObjectRecord rec, byte[] src, long start, long end)
    {
        long pos = start;
        for (int si = 0; si < rec.StringFields.Count; si++)
        {
            var f = rec.StringFields[si];
            if (f.Start < pos || f.Start + f.BitLen > end) continue;
            CopyBits(w, src, pos, (int)(f.Start - pos));
            if (rec.Kind == DwgObjectRecordKind.Entity) WriteTvReinlineNul(w, f.Value);
            else w.WriteTV(f.Value, false);
            WriteSideEdits.NoteFieldEmitted(rec.Handle, si);
            pos = f.Start + f.BitLen;
        }
        CopyBits(w, src, pos, (int)(end - pos));
    }
}
