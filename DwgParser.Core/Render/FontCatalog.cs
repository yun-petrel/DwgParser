namespace DwgParser.Core.Render;

public static class FontCatalog
{
    private static Func<IReadOnlyList<string>>? _provider;
    private static Func<string, string, string?>? _matcher;
    private static Func<string, bool>? _cjkProbe;
    private static HashSet<string>? _families;

    public static void Register(Func<IReadOnlyList<string>> provider)
    {
        _provider = provider;
        _families = null;
    }

    public static void RegisterMatcher(Func<string, string, string?> matcher)
    {
        _matcher = matcher;
        _families = null;
    }

    public static void RegisterCjkProbe(Func<string, bool> probe) => _cjkProbe = probe;

    public static bool CoversCjk(string family)
    {
        var probe = _cjkProbe;
        if (probe == null) return false;
        try { return probe(family); }
        catch { return false; }
    }

    public static string? ResolveFamilyFromFontFile(string stem, string fileName)
    {
        var matcher = _matcher;
        if (matcher == null) return null;
        try { return matcher(stem, fileName); }
        catch { return null; }
    }

    public static IReadOnlyCollection<string> Families => _families ??= Build();

    private static HashSet<string> Build()
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var provider = _provider;
        if (provider != null)
        {
            try
            {
                foreach (var name in provider())
                    if (!string.IsNullOrWhiteSpace(name)) set.Add(name);
            }
            catch { set.Clear(); }
        }
        return set;
    }

    public static bool IsInstalled(string family) => Families.Contains(family);

    public static bool HasExactFamily(string family)
        => _exact == null ? IsInstalled(family) : _exact.Contains(family);

    private static HashSet<string>? _exact;

    public static void RegisterExactFamilies(IEnumerable<string> names)
        => _exact = new HashSet<string>(names, StringComparer.OrdinalIgnoreCase);
}
