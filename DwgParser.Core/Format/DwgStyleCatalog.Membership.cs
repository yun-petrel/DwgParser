using DwgParser.Core.Objects;

namespace DwgParser.Core.Format;

public sealed partial class DwgStyleCatalog
{
    private static readonly short[] TextCodes = { 0x35 };
    private static readonly short[] LtypeCodes = { 0x39 };
    private static readonly short[] DimStyleCodes = { 0x45 };
    private static readonly short[] MlineStyleCodes = { 0x49 };
    private static readonly short[] TableStyleCodes = { 504 };
    private static readonly short[] MLeaderStyleCodes = { 507 };
    private static readonly short[] VisualStyleCodes = { 509 };
    private static readonly short[] ViewStyleCodes = { 510, 511 };

    public sealed record MembershipAuditRow(
        string Family,
        int InCatalog,
        int Claimed,
        bool ClaimsAvailable,
        IReadOnlyList<ulong> Unclaimed,
        IReadOnlyList<ulong> Unmaterialized);

    public static IReadOnlyList<MembershipAuditRow> AuditMembership(DwgObjectReader.WalkResult objects)
    {
        var claimed = CollectContainerClaims(objects);

        var typeByHandle = new Dictionary<ulong, short>(objects.Records.Count);
        foreach (var rec in objects.Records) typeByHandle[rec.Handle] = rec.LogicalType;

        var catalog = From(objects);
        return new[]
        {
            Row("Text", catalog.Text.Keys, TextCodes, claimed, typeByHandle),
            Row("Linetype", catalog.Linetype.Keys, LtypeCodes, claimed, typeByHandle),
            Row("Dimension", catalog.Dimension.Keys, DimStyleCodes, claimed, typeByHandle),
            Row("MLineStyle", catalog.MLineStyle.Keys, MlineStyleCodes, claimed, typeByHandle),
            Row("TableStyle", catalog.TableStyle.Keys, TableStyleCodes, claimed, typeByHandle),
            Row("MLeaderStyle", catalog.MLeaderStyle.Keys, MLeaderStyleCodes, claimed, typeByHandle),
            Row("Visual", catalog.Visual.Keys, VisualStyleCodes, claimed, typeByHandle),
            Row("ViewStyle", catalog.ViewStyle.Keys, ViewStyleCodes, claimed, typeByHandle),
            Row("ViewStyleTemplate", catalog.ViewStyleTemplate.Keys, ViewStyleCodes, claimed, typeByHandle),
        };
    }

    private static HashSet<ulong> CollectContainerClaims(DwgObjectReader.WalkResult objects)
    {
        var claimed = new HashSet<ulong>();
        foreach (var rec in objects.Records)
        {
            if (rec.TableControl is { EntriesCaptured: true, EntriesValidated: true } tc)
            {
                foreach (var h in tc.EntryHandles) if (h != 0) claimed.Add(h);
                foreach (var h in tc.MoreHandleRefs) if (h != 0) claimed.Add(h);
            }
            foreach (var (_, value) in rec.DictionaryEntries)
                if (value != 0) claimed.Add(value);
        }
        return claimed;
    }

    private static MembershipAuditRow Row(
        string family, IEnumerable<ulong> catalogKeys, short[] memberCodes,
        HashSet<ulong> claimed, Dictionary<ulong, short> typeByHandle)
    {
        var keys = catalogKeys.ToHashSet();
        var claimedOfFamily = new HashSet<ulong>();
        foreach (var (handle, type) in typeByHandle)
            if (Array.IndexOf(memberCodes, type) >= 0 && claimed.Contains(handle))
                claimedOfFamily.Add(handle);

        var unclaimed = keys.Where(h => !claimed.Contains(h)).OrderBy(h => h).ToArray();
        var unmaterialized = claimedOfFamily.Where(h => !keys.Contains(h)).OrderBy(h => h).ToArray();
        return new MembershipAuditRow(
            family, keys.Count, claimedOfFamily.Count, claimedOfFamily.Count > 0,
            unclaimed, unmaterialized);
    }
}
