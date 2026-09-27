using DwgParser.Core.Objects;

namespace DwgParser.Core.Render;

public static class FontResolver
{
    public sealed record ResolvedFont(string Family, bool Vertical);

    private static readonly string[] CjkPreferred =
        ["SimSun", "Microsoft YaHei", "SimHei", "FangSong", "KaiTi", "Source Han Sans SC", "Noto Sans CJK SC",
         "宋体", "微软雅黑", "黑体", "仿宋", "楷体"];

    private static readonly string[] AnyPreferred =
        ["Arial", "Segoe UI", "Tahoma", "Times New Roman", "Courier New",
         "Noto Sans", "DejaVu Sans", "Liberation Sans"];

    public static ResolvedFont Resolve(DwgObjectReader.TextStyleInfo? style, string text)
    {
        bool hasCjk = ContainsCjk(text);
        string family = ResolveFamily(style, hasCjk);
        return new ResolvedFont(family, style?.IsVertical ?? false);
    }

    public static ResolvedFont Resolve(DwgObjectReader.TextStyleInfo? style)
        => Resolve(style, "");

    public static string ResolveRunFamily(string? fontSpec, string fallbackFamily)
    {
        if (string.IsNullOrWhiteSpace(fontSpec)) return fallbackFamily;
        string name = fontSpec.Split('|')[0].Trim();
        if (name.Length == 0) return fallbackFamily;

        string ext = Path.GetExtension(name).ToLowerInvariant();
        if (ext is ".ttf" or ".ttc" or ".otf")
        {
            string? m = MatchFontFile(name) ?? MatchInstalledFamily(name);
            if (m != null) return m;
        }
        else if (ext == ".shx")
        {
            string m = MapShx(name, preferCjk: ContainsCjk(name));
            if (m.Length > 0) return m;
        }
        else
        {
            string? m = MatchInstalledFamily(name);
            if (m != null) return m;
            string s = MapShx(name, preferCjk: ContainsCjk(name));
            if (s.Length > 0) return s;
        }
        return fallbackFamily;
    }

    private static string? MatchFontFile(string fileName)
    {
        string stem = StripExt(fileName);
        foreach (var stemCandidate in new[] { stem, stem.Replace(" ", "").Replace("-", "") })
        {
            string? hit = FontCatalog.ResolveFamilyFromFontFile(stemCandidate, fileName);
            if (!string.IsNullOrEmpty(hit) && FontCatalog.IsInstalled(hit)) return hit;
        }
        return null;
    }

    private static string ResolveFamily(DwgObjectReader.TextStyleInfo? style, bool hasCjk)
    {
        string main = style?.FontFile ?? "";
        string big = style?.BigfontFile ?? "";

        if (!string.IsNullOrWhiteSpace(big))
        {
            string mapped = MapShx(big, preferCjk: true);
            if (!string.IsNullOrEmpty(mapped)) return mapped;
        }

        if (!string.IsNullOrWhiteSpace(main))
        {
            string ext = Path.GetExtension(main).ToLowerInvariant();
            if (ext is ".ttf" or ".ttc" or ".otf")
            {
                string? matched = MatchFontFile(main) ?? MatchInstalledFamily(StripExt(main));
                if (matched != null)
                {
                    if (!(hasCjk && IsShapeFontFamily(matched) && !FontCatalog.CoversCjk(matched)))
                        return matched;
                }
            }
            else
            {
                string mapped = MapShx(main, preferCjk: hasCjk);
                if (!string.IsNullOrEmpty(mapped)) return mapped;
            }
        }

        return DefaultCjkFamily();
    }

    private static string MapShx(string fileName, bool preferCjk)
    {
        string stem = Path.GetFileNameWithoutExtension(fileName).ToLowerInvariant();

        if (stem.Contains("gbcbig") || stem.Contains("hzfs") || stem.Contains("fs64") || stem.Contains("fshx"))
            return Installed("FangSong", "SimSun");
        if (stem.Contains("hztxt") || stem.Contains("hzxk") || stem.Contains("txt64") || stem.Contains("china"))
            return Installed("SimSun", "Microsoft YaHei");
        if (stem.Contains("hzhei") || stem.Contains("st64s") || stem.Contains("hei64"))
            return Installed("SimHei", "Microsoft YaHei");
        if (stem.Contains("hzpy") || stem.Contains("kai64") || stem.Contains("hzkai"))
            return Installed("KaiTi", "SimSun");
        if (stem.Contains("bigfont") || stem.Contains("cht"))
            return DefaultCjkFamily();

        if (preferCjk)
        {
            return stem.Contains("iso") || stem.Contains("gos")
                ? Installed("SimSun", "Microsoft YaHei")
                : Installed("SimSun", "Microsoft YaHei");
        }

        return InstalledShapeFont("Arial", "SimSun");
    }

    private static string Installed(params string[] candidates)
    {
        foreach (var c in candidates)
            if (FontCatalog.IsInstalled(c)) return c;
        return DefaultCjkFamily();
    }

    private static readonly string[] ShapeFontFamilies =
        ["Arial", "Times New Roman", "Courier New", "Calibri", "Tahoma", "Segoe UI"];

    private static bool IsShapeFontFamily(string family)
    {
        foreach (var f in ShapeFontFamilies)
            if (string.Equals(f, family, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    private static string InstalledShapeFont(params string[] candidates)
    {
        foreach (var c in candidates)
            if (FontCatalog.HasExactFamily(c)) return c;
        return DefaultCjkFamily();
    }

    private static string? MatchInstalledFamily(string fileName)
    {
        string stem = StripExt(fileName);
        string normalized = stem.Replace(" ", "").Replace("-", "");
        foreach (var f in FontCatalog.Families)
        {
            string fn = f.Replace(" ", "").Replace("-", "");
            if (fn.Equals(normalized, StringComparison.OrdinalIgnoreCase))
                return f;
        }
        foreach (var (alias, family) in AliasMap)
            if (stem.Equals(alias, StringComparison.OrdinalIgnoreCase) && FontCatalog.IsInstalled(family))
                return family;
        return null;
    }

    private static readonly (string Alias, string Family)[] AliasMap =
    [
        ("simsun", "SimSun"), ("nsimsun", "SimSun"), ("宋体", "SimSun"),
        ("simhei", "SimHei"), ("黑体", "SimHei"),
        ("simfang", "FangSong"), ("仿宋", "FangSong"), ("fangsong", "FangSong"),
        ("simkai", "KaiTi"), ("楷体", "KaiTi"), ("kaiti", "KaiTi"),
        ("msyh", "Microsoft YaHei"), ("微软雅黑", "Microsoft YaHei"),
        ("arial", "Arial"), ("times", "Times New Roman"), ("tahoma", "Tahoma"),
        ("calibri", "Calibri"), ("cour", "Courier New"), ("courier", "Courier New"),
    ];

    private static string StripExt(string file)
    {
        string name = file;
        int slash = name.LastIndexOfAny(['\\', '/']);
        if (slash >= 0) name = name[(slash + 1)..];
        int dot = name.LastIndexOf('.');
        return dot > 0 ? name[..dot] : name;
    }

    private static bool ContainsCjk(string text)
    {
        if (string.IsNullOrEmpty(text)) return false;
        foreach (char c in text)
        {
            int v = c;
            if (v is >= 0x4E00 and <= 0x9FFF or >= 0x3400 and <= 0x4DBF or >= 0xF900 and <= 0xFAFF)
                return true;
        }
        return false;
    }

    private static string DefaultCjkFamily()
    {
        foreach (var name in CjkPreferred)
            if (FontCatalog.IsInstalled(name)) return name;
        var fams = FontCatalog.Families;
        if (fams.Count == 0) return "Arial";
        foreach (var name in AnyPreferred)
            if (FontCatalog.IsInstalled(name)) return name;
        return fams.First();
    }
}
