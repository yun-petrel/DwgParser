using DwgParser.Core.IO;

namespace DwgParser.Core.Objects;

public static partial class DwgObjectStreamWriter
{
    private static bool SrcLocked(DwgObjectRecord rec) => rec.SourceVersion >= DwgVersion.AC1024;
    private static bool SrcMtext(DwgObjectRecord rec) => rec.SourceVersion >= DwgVersion.AC1032;
    private static bool TgtLocked(Target t) => t.R2010Plus;
    private static bool TgtMtext(Target t) => t.Version >= DwgVersion.AC1032;

    private static byte AttTailRc(int captured, int fallback) => captured >= 0 ? (byte)(captured & 0xFF) : (byte)fallback;

    private static byte AttTailLockedUp(DwgObjectRecord rec) =>
        AttTailRc(rec.AttRoundTripLockedInBlock, 0);

    private static byte AttTailKeepUp(DwgObjectRecord rec) =>
        AttTailRc(rec.AttRoundTripKeepDuplicateRecords, 0);

    private static byte AttTailMTextUp(DwgObjectRecord rec) =>
        AttTailRc(rec.AttRoundTripMTextType, 1);

    private static string Rc8(byte v) => Convert.ToString(v, 2).PadLeft(8, '0');

    internal static int AttTailLockedUpForTest(DwgObjectRecord rec) => AttTailLockedUp(rec);
    internal static int AttTailMTextUpForTest(DwgObjectRecord rec) => AttTailMTextUp(rec);
    internal static int AttTailKeepUpForTest(DwgObjectRecord rec) => AttTailKeepUp(rec);
    internal static string Rc8ForTest(byte v) => Rc8(v);

    internal static bool NeedsAttributeTailInsert(DwgObjectRecord rec, Target t) =>
        IsAttributeFamily(rec) && rec.SourceVersion >= DwgVersion.AC1021
        && (TgtLocked(t) && !SrcLocked(rec) || TgtMtext(t) && !SrcMtext(rec));

    internal static bool StripsAttributeTailInsert(DwgObjectRecord rec, Target t) =>
        IsAttributeFamily(rec) && rec.SourceVersion >= DwgVersion.AC1021
        && (SrcLocked(rec) && !TgtLocked(t) || SrcMtext(rec) && !TgtMtext(t));

    internal static bool AttributeTailCrossesVersion(DwgObjectRecord rec, Target t) =>
        NeedsAttributeTailInsert(rec, t) || StripsAttributeTailInsert(rec, t);

    internal static bool AttributeTailCrossesR2007(DwgObjectRecord rec, Target t) =>
        IsAttributeFamily(rec)
        && rec.SourceVersion < DwgVersion.AC1021
        && t.Version >= DwgVersion.AC1021
        && rec.AttTailKeepDuplicateBit > 0;
}
