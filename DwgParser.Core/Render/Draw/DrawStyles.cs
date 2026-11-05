namespace DwgParser.Core.Render;

public static class Rgba
{
    public static uint FromHex(string hex)
    {
        if (string.IsNullOrEmpty(hex) || hex.Length < 7) return 0x000000FF;
        uint r = Convert.ToByte(hex.Substring(1, 2), 16);
        uint g = Convert.ToByte(hex.Substring(3, 2), 16);
        uint b = Convert.ToByte(hex.Substring(5, 2), 16);
        uint a = hex.Length >= 9 ? Convert.ToByte(hex.Substring(7, 2), 16) : (byte)0xFF;
        return (r << 24) | (g << 16) | (b << 8) | a;
    }

    public static string ToHex(uint v) => $"#{v >> 24 & 0xFF:X2}{v >> 16 & 0xFF:X2}{v >> 8 & 0xFF:X2}";
}

public readonly record struct StrokeStyle(uint Rgba, bool NonScaling, bool Dashed, double[]? DashPattern = null)
{
    public static StrokeStyle Solid(uint rgba) => new(rgba, true, false);

    public static StrokeStyle Hatched(uint rgba) => new(rgba, false, false);

    public bool HasDashPattern => DashPattern is { Length: > 0 };

    public byte R => (byte)(Rgba >> 24);
    public byte G => (byte)(Rgba >> 16);
    public byte B => (byte)(Rgba >> 8);
}

public readonly record struct FillStyle(uint Rgba, double Opacity, bool Stroked, byte? RasterAlpha = null)
{
    public static readonly FillStyle NoFill = new(0, 0, false);

    public bool IsFill => Opacity > 0;
    public byte R => (byte)(Rgba >> 24);
    public byte G => (byte)(Rgba >> 16);
    public byte B => (byte)(Rgba >> 8);

    public byte AlphaByte => RasterAlpha ?? (byte)(Opacity <= 0 ? 0 : Opacity >= 0.999 ? 255 : 60);
}

public enum TextAlignment { BaseLeft, BaseCenter, BaseRight, TopLeft, MiddleLeft, MiddleCenter, MiddleRight }

public readonly record struct TextStyle(string FontFamily, double Height, double Rotation,
                                        TextAlignment Align, uint Rgba);

public readonly record struct TextRequest(string Content, DwgParser.Core.Objects.Vec2 Position,
                                          TextStyle Style, RasterTextSpec? Raster = null);

public sealed record RasterTextSpec(
    int HAlign, int VAlign, int HorizMode, int VertMode,
    DwgParser.Core.Objects.Vec2 AlignmentPoint, double RawWidthFactor,
    double LineGap, bool IsMText,
    DwgParser.Core.Objects.DwgObjectReader.TextStyleInfo? Style,
    double EmFactor = ShxTextShapes.EmFactor, int MinFontPx = TextRasterLimits.MinFontPx, bool StyleWidthFactor = true,
    bool FamilyFromStyle = true, double BaselineNudge = 0, bool SkipVerticalShx = false);

public readonly record struct DrawViewport(double MinX, double MinY, double MaxX, double MaxY,
                                           int WidthPx, int HeightPx, double MarginPx, bool FlipY);
