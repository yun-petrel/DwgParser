using System.Drawing;
using System.Runtime.CompilerServices;
using System.Threading;
using DwgParser.Core.Render;

namespace DwgParser.Rendering;

public static class RenderingRuntime
{
    private static int _installed;

    public static void EnsureSystemFonts()
    {
        if (Interlocked.Exchange(ref _installed, 1) == 1) return;
        if (TextBackends.IsGdi)
        {
            FontCatalog.Register(() =>
            {
                try
                {
                    return FontFamily.Families.Select(f => f.Name).ToArray();
                }
                catch
                {
                    return Array.Empty<string>();
                }
            });
            FontCatalog.RegisterCjkProbe(CoversCjkGdi);
        }
        else
        {
            ManagedTextBackend.Instance.InstallFontCatalog();
        }
    }

    private static bool CoversCjkGdi(string family)
    {
        try
        {
            using var f = new Font(family, 12f);
            return f.GdiCharSet is 134 or 136;
        }
        catch { return false; }
    }

    [ModuleInitializer]
    internal static void AutoInstall() => EnsureSystemFonts();
}
