using DwgParser.Core.Render;

namespace DwgParser.Rendering;

public static class ShxBypassFontProbe
{
    public static List<ShxFont> Of(byte[]? sourceData, string? drawingDir = null)
    {
        var fonts = new List<ShxFont>();
        foreach (var name in ShxFontResolver.ScanFontNames(sourceData))
            if (ShxFontResolver.Resolve(name, drawingDir) is { } f)
                fonts.Add(f);
        return fonts;
    }
}
