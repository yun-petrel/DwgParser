using System.Reflection;
using DwgParser.Core;
using DwgParser.Core.Format;
using DwgParser.Core.Objects;

namespace DwgParser.Cli;

public static partial class Program
{
    private static readonly FieldInfo[] CensusCaptureSlots =
        typeof(DwgObjectRecord)
            .GetFields(BindingFlags.Public | BindingFlags.Instance)
            .Where(f => f.FieldType.IsClass
                        && f.FieldType.Namespace == "DwgParser.Core.Objects.Records")
            .ToArray();

    internal static bool? CensusSecondLane(DwgObjectRecord r, DwgObjectReader.WalkResult objs)
        => r.LogicalType switch
        {
            0x31 => objs.BlockHeaderByHandle.TryGetValue(r.Handle, out var region)
                        ? region.FieldsCaptured
                        : (bool?)null,
            _ => null,
        };

    internal static string? CensusSecondLaneName(DwgObjectRecord r)
        => r.LogicalType == 0x31 ? "BlockRegion.FieldsCaptured" : null;

    internal static IEnumerable<string> CensusFailReasons(object capture)
    {
        var t = capture.GetType();
        foreach (var m in t.GetMembers(BindingFlags.Public | BindingFlags.Instance))
        {
            string? name = m switch
            {
                FieldInfo fi when fi.FieldType == typeof(string) && fi.Name.EndsWith("FailReason", StringComparison.Ordinal)
                    => (string?)fi.GetValue(capture),
                PropertyInfo pi when pi.PropertyType == typeof(string) && pi.Name.EndsWith("FailReason", StringComparison.Ordinal)
                    => (string?)pi.GetValue(capture),
                _ => null,
            };
            if (!string.IsNullOrEmpty(name)) yield return name;
        }
    }

    private static void CensusReport(string path, DwgDocument doc)
    {
        var objs = doc.GetObjects();

        var agg = new Dictionary<string, int[]>();
        var slotsByClass = new Dictionary<string, SortedSet<string>>();
        var failsByClass = new Dictionary<string, SortedDictionary<string, int>>();
        int regionSeen = 0, regionCaptured = 0;

        foreach (var r in objs.Records)
        {
            string key = CensusKey(r);
            var a = agg.TryGetValue(key, out var e) ? e : agg[key] = new int[6];
            a[0]++;
            bool anySlot = false, committed = r.DictionaryNumItems >= 0;
            var names = slotsByClass.TryGetValue(key, out var s) ? s : slotsByClass[key] = new SortedSet<string>();
            foreach (var f in CensusCaptureSlots)
            {
                var v = f.GetValue(r);
                if (v is null) continue;
                anySlot = true;
                names.Add(f.Name);
                if (CensusCommitted(v)) committed = true;
            }
            bool? lane = CensusSecondLane(r, objs);
            if (lane is not null)
            {
                anySlot |= lane.Value;
                if (lane.Value) { committed = true; names.Add(CensusSecondLaneName(r)!); }
                regionSeen++;
                if (lane.Value) regionCaptured++;
            }
            if (!committed && anySlot)
            {
                var fs = failsByClass.TryGetValue(key, out var d0) ? d0 : failsByClass[key] = new();
                bool anyReason = false;
                foreach (var f in CensusCaptureSlots)
                {
                    if (f.GetValue(r) is not { } v) continue;
                    foreach (var why in CensusFailReasons(v))
                    {
                        fs[why] = fs.TryGetValue(why, out var n) ? n + 1 : 1;
                        anyReason = true;
                    }
                }
                if (!anyReason) fs["<none>"] = fs.TryGetValue("<none>", out var z) ? z + 1 : 1;
            }
            if (committed) a[1]++;
            else if (anySlot) a[5]++;
            switch (r.Kind)
            {
                case DwgObjectRecordKind.Entity: a[2]++; break;
                case DwgObjectRecordKind.TableControl: a[3]++; break;
                default: a[4]++; break;
            }
        }

        Console.WriteLine($"CENSUS file={System.IO.Path.GetFileName(path)} ver={doc.Version} " +
            $"seen={objs.ObjectsSeen} decoded={objs.ObjectsDecoded} errors={objs.Errors} " +
            $"records={objs.Records.Count} dimstyles={objs.DimStyles.Count}" +
            $" ltypestyles={objs.LtypeStyles.Count} stylefonts={objs.StyleFonts.Count}" +
            $" blockfields={regionCaptured}/{regionSeen}");
        foreach (var (key, a) in agg.OrderBy(kv => kv.Value[0]).Reverse())
            Console.WriteLine($"CENSUS-CLS {key} total={a[0]} captured={a[1]} rejected={a[5]} " +
                $"ent={a[2]} tbl={a[3]} opa={a[4]}" +
                (slotsByClass.TryGetValue(key, out var names) && names.Count > 0
                    ? " slots=" + string.Join("|", names) : "") +
                (failsByClass.TryGetValue(key, out var fails) && fails.Count > 0
                    ? " fails[" + string.Join(" | ", fails.OrderByDescending(kv => kv.Value)
                        .Select(kv => $"{kv.Key}={kv.Value}")) + "]" : ""));

        short[] ctls = { 0x30, 0x32, 0x34, 0x38, 0x3C, 0x3E, 0x40, 0x42, 0x44, 0x46 };
        foreach (var r in objs.Records.Where(r => ctls.Contains(r.LogicalType)).OrderBy(r => r.LogicalType))
        {
            int sameTableRecords = objs.Records.Count(x => x.TypeCode == (short)(r.LogicalType + 1));
            var c = r.TableControl;
            Console.WriteLine($"CENSUS-CTL t=0x{r.LogicalType:X2} h={r.Handle:X} captured={c?.Captured} " +
                $"entries={c?.NumEntries} more={c?.NumMoreHandles} strings={c?.StringStreamPresent} " +
                $"sameCodeRecords={sameTableRecords} members={c?.EntryHandles.Count(x => x != 0)} " +
                $"slots={c?.EntryHandles.Count} moreRefs={c?.MoreHandleRefs.Count} " +
                $"entriesCap={c?.EntriesCaptured} validated={c?.EntriesValidated} " +
                (string.IsNullOrEmpty(c?.EntriesFailReason) ? "" : $"entriesFail={c!.EntriesFailReason}"));
        }

        foreach (var row in DwgStyleCatalog.AuditMembership(objs))
        {
            var causes = UnmaterializedCauses(objs, row);
            Console.WriteLine($"CENSUS-AUDIT fam={row.Family} catalog={row.InCatalog} claimed={row.Claimed} " +
                $"available={row.ClaimsAvailable}" +
                (row.Unclaimed.Count > 0
                    ? " UNCLAIMED=" + string.Join(",", row.Unclaimed.Select(h => h.ToString("X"))) : "") +
                (row.Unmaterialized.Count > 0
                    ? $" unmaterialized={row.Unmaterialized.Count} causes={string.Join("/", causes)}" : ""));
        }
    }

    private static readonly Dictionary<string, string[]> BagFields = new()
    {
        ["Text"] = new[] { "Style" },
        ["Linetype"] = new[] { "LType" },
        ["Dimension"] = new[] { "DimStyle" },
        ["TableStyle"] = new[] { "TableStyle" },
        ["MLeaderStyle"] = new[] { "MLeader" },
        ["MLineStyle"] = new[] { "MLineStyle" },
        ["Visual"] = new[] { "VisualStyle", "VisualStyleModel" },
        ["ViewStyle"] = new[] { "ViewStyle" },
        ["ViewStyleTemplate"] = new[] { "ViewStyle" },
    };

    private static IEnumerable<string> UnmaterializedCauses(DwgObjectReader.WalkResult objs,
        DwgStyleCatalog.MembershipAuditRow row)
    {
        if (row.Unmaterialized.Count == 0) yield break;
        if (!BagFields.TryGetValue(row.Family, out var names)) { yield return "无袋字段"; yield break; }

        int outOfRange = 0, judgeFail = 0, notCollected = 0;
        foreach (var h in row.Unmaterialized)
        {
            var rec = objs.Records.FirstOrDefault(r => r.Handle == h);
            object? bag = null;
            if (rec is not null)
                foreach (var n in names)
                {
                    var fi = typeof(DwgObjectRecord).GetField(n, BindingFlags.Public | BindingFlags.Instance);
                    var pi = typeof(DwgObjectRecord).GetProperty(n, BindingFlags.Public | BindingFlags.Instance);
                    bag = fi is not null ? fi.GetValue(rec) : pi?.GetValue(rec);
                    if (bag is not null) break;
                }
            if (bag is null) { outOfRange++; continue; }
            if (CensusCommitted(bag)) notCollected++;
            else judgeFail++;
        }
        if (outOfRange > 0) yield return $"outOfRange:{outOfRange}";
        if (judgeFail > 0) yield return $"judgeFail:{judgeFail}";
        if (notCollected > 0) yield return $"notCollected:{notCollected}";
    }

    private static bool CensusCommitted(object capture)
    {
        var t = capture.GetType();
        var gates = t.GetMembers(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m is PropertyInfo pi && pi.PropertyType == typeof(bool)
                        && (pi.Name.EndsWith("Captured", StringComparison.Ordinal) || pi.Name.EndsWith("Committed", StringComparison.Ordinal))
                        || m is FieldInfo fi && fi.FieldType == typeof(bool)
                        && (fi.Name.EndsWith("Captured", StringComparison.Ordinal) || fi.Name.EndsWith("Committed", StringComparison.Ordinal)))
            .ToList();
        foreach (var m in gates)
        {
            bool val = m is PropertyInfo p ? (bool)p.GetValue(capture)! : (bool)((FieldInfo)m).GetValue(capture)!;
            if (val) return true;
        }
        return gates.Count == 0;
    }

    private static string CensusKey(DwgObjectRecord r) =>
        ClassTypeCodes.DiagnosticClassOf(r.TypeCode, r.ClassName);
}
