using System.Reflection;

namespace DwgParser.Core.Render;

public static class ShxBuiltinFonts
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, ShxFont?> Cache =
        new(StringComparer.OrdinalIgnoreCase);

    public static ShxFont? Load(string fontFileName)
    {
        if (string.IsNullOrWhiteSpace(fontFileName)) return null;
        string file = Path.GetFileName(fontFileName);
        return Cache.GetOrAdd(file, _ =>
        {
            var asm = typeof(ShxBuiltinFonts).Assembly;
            string suffix = ".fonts." + file;
            foreach (string res in asm.GetManifestResourceNames())
            {
                if (!res.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)) continue;
                try
                {
                    using var stream = asm.GetManifestResourceStream(res);
                    if (stream == null) continue;
                    var bytes = new byte[stream.Length];
                    stream.ReadExactly(bytes);
                    if (ShxFont.TryLoad(bytes, out var font)) return font;
                }
                catch {  }
            }
            return null;
        });
    }

    public static bool Contains(string fontFileName)
    {
        string file = Path.GetFileName(fontFileName ?? "");
        if (file.Length == 0) return false;
        string suffix = ".fonts." + file;
        return typeof(ShxBuiltinFonts).Assembly
            .GetManifestResourceNames().Any(n => n.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
    }
}
