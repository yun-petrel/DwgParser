using System.Text.RegularExpressions;

namespace DwgParser.Core.Objects;

public sealed record ClassOccurrence(string ClassName, int Count);

public static partial class ObjectScanner
{
    [GeneratedRegex(@"cDb[A-Za-z0-9_]{2,40}")]
    private static partial Regex AcDbClassRegex();

    [GeneratedRegex(@"CAD_[A-Z0-9_]{2,32}")]
    private static partial Regex AcadProxyRegex();

    public static List<ClassOccurrence> ScanClasses(byte[] data)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (Match m in AcDbClassRegex().Matches(System.Text.Encoding.ASCII.GetString(data)))
        {
            var name = m.Value;
            counts[name] = counts.TryGetValue(name, out var n) ? n + 1 : 1;
        }
        foreach (Match m in AcadProxyRegex().Matches(System.Text.Encoding.ASCII.GetString(data)))
        {
            counts[m.Value] = counts.TryGetValue(m.Value, out var n) ? n + 1 : 1;
        }
        return counts
            .OrderByDescending(kv => kv.Value)
            .Select(kv => new ClassOccurrence(kv.Key, kv.Value))
            .ToList();
    }
}
