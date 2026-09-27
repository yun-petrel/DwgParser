using System;

namespace DwgParser.Rendering;

internal static class RenderInk
{
    public static bool DarkBackground { get; set; }

    public static (byte R, byte G, byte B) Map(byte r, byte g, byte b)
    {
        if (!DarkBackground)
            return r == 255 && g == 255 && b == 255 ? ((byte)0, (byte)0, (byte)0) : (r, g, b);
        return r == 0 && g == 0 && b == 0 ? ((byte)255, (byte)255, (byte)255) : (r, g, b);
    }
}
