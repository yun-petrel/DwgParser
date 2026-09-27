using System.Collections.Concurrent;
using System.Text;
using DwgParser.Core.Render;

namespace DwgParser.Rendering;

public static class ShxFontResolver
{
    private static readonly ConcurrentDictionary<string, ShxFont?> Cache = new(StringComparer.OrdinalIgnoreCase);

    private static IEnumerable<string> SearchDirs(string? drawingDir)
    {
        var env = Environment.GetEnvironmentVariable("SHX_FONT_DIRS");
        if (!string.IsNullOrWhiteSpace(env))
            foreach (var d in env.Split(';', StringSplitOptions.RemoveEmptyEntries))
                yield return d;
        if (!string.IsNullOrWhiteSpace(drawingDir)) yield return drawingDir!;
        foreach (var d in new[]
        {
            @":\ZWCAD2025\fonts",
            @":\Project\DrawLib\Fonts",
        })
            yield return d;
    }

    public static ShxFont? Resolve(string fontFileName, string? drawingDir = null)
    {
        if (string.IsNullOrWhiteSpace(fontFileName)) return null;
        string file = Path.GetFileName(fontFileName);
        if (!file.EndsWith(".shx", StringComparison.OrdinalIgnoreCase)) return null;
        return Cache.GetOrAdd(file, _ =>
        {
            foreach (var dir in SearchDirs(drawingDir))
            {
                try
                {
                    foreach (var cand in Directory.EnumerateFiles(dir, "*.shx", SearchOption.TopDirectoryOnly))
                    {
                        if (!Path.GetFileName(cand).Equals(file, StringComparison.OrdinalIgnoreCase)) continue;
                        if (ShxFont.TryLoad(cand, out var f)) return f;
                    }
                }
                catch {  }
            }
            return ShxBuiltinFonts.Load(file);
        });
    }

    public static List<string> ScanFontNames(byte[]? sourceData)
    {
        var found = new List<string>();
        if (sourceData == null) return found;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        static bool NameChar(byte c) =>
            (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') ||
            (c >= '0' && c <= '9') || c == '-' || c == '_' || c == '.';
        int i = 0;
        while (i < sourceData.Length)
        {
            if (!NameChar(sourceData[i])) { i++; continue; }
            int start = i;
            while (i < sourceData.Length && NameChar(sourceData[i])) i++;
            string s = Encoding.ASCII.GetString(sourceData, start, i - start);
            int dot = s.LastIndexOf(".shx", StringComparison.OrdinalIgnoreCase);
            if (dot <= 0) continue;
            string name = s[..(dot + 4)];
            int slash = Math.Max(name.LastIndexOf('\\'), name.LastIndexOf('/'));
            if (slash >= 0) name = name[(slash + 1)..];
            if (name.Length > 5 && name[..^4].Any(ch => ch != '.') && seen.Add(name))
                found.Add(name);
        }
        return found;
    }

    public static List<ShxFont> BypassFonts(byte[]? sourceData)
    {
        var list = new List<ShxFont>();
        foreach (var name in ScanFontNames(sourceData))
        {
            var f = Resolve(name);
            if (f != null) list.Add(f);
        }
        return list;
    }
}
