using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;

using DwgParser.Core.Render;
namespace DwgParser.Rendering;

public sealed class GdiTextBackend : ITextRendererBackend, ITextShapeBackend
{
    public static readonly GdiTextBackend Instance = new();

    public string Name => "gdi+";

    public void DrawText(PixelBuffer buf, int x, int y,
        string text, byte red, byte grn, byte blu, int fontSizePx, double rotationRad = 0,
        string? fontFamily = null, double widthFactor = 1.0,
        int hAlign = 0, int vAlign = 0, double lineGapFactor = 1.0,
        double charHeightPx = 0)
        => GdiTextRenderer.DrawText(buf, x, y, text, red, grn, blu, fontSizePx, rotationRad,
            fontFamily, widthFactor, hAlign, vAlign, lineGapFactor, charHeightPx);

    public void DrawMTextRuns(PixelBuffer buf, int x, int y,
        IReadOnlyList<MTextParser.Line> lines, byte red, byte grn, byte blu,
        int baseFontPx, double rotationRad, string defaultFamily,
        double widthFactor, int hAlign, int vAlign, double lineGapFactor,
        double worldToPxScale = 1.0, double charHeightPx = 0)
        => GdiTextRenderer.DrawMTextRuns(buf, x, y, lines, red, grn, blu,
            baseFontPx, rotationRad, defaultFamily, widthFactor, hAlign, vAlign, lineGapFactor,
            worldToPxScale, charHeightPx);

    public double MeasureTextWidth(string text, string? fontFamily, int fontSizePx,
        double widthFactor, double worldToPxScale = 1.0)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        try
        {
            using var bmp = new Bitmap(1, 1);
            using var gfx = Graphics.FromImage(bmp);
            using var font = new Font(
                string.IsNullOrWhiteSpace(fontFamily) ? "Arial" : fontFamily,
                Math.Clamp(fontSizePx, TextRasterLimits.MinFontPx, 200), FontStyle.Regular, GraphicsUnit.Pixel);
            using var fmt = StringFormat.GenericTypographic;
            return gfx.MeasureString(text, font, PointF.Empty, fmt).Width * widthFactor;
        }
        catch { return 0; }
    }

    public double GetTextOutline(string text, string? fontFamily, int fontSizePx,
        double widthFactor, IGlyphOutlineSink sink, out string resolved)
    {
        resolved = string.IsNullOrWhiteSpace(fontFamily) ? "Arial" : fontFamily!;
        if (string.IsNullOrEmpty(text)) return 0;

        using var path = new GraphicsPath();
        using var bmp = new Bitmap(1, 1);
        using var gfx = Graphics.FromImage(bmp);
        using var font = new Font(resolved, Math.Clamp(fontSizePx, TextRasterLimits.MinFontPx, 200),
            FontStyle.Regular, GraphicsUnit.Pixel);
        using var fmt = StringFormat.GenericTypographic;
        path.AddString(text, font.FontFamily, 0, font.Size, new PointF(0, 0), fmt);
        double width = path.GetBounds().Width * widthFactor;

        foreach (var (pt, kind) in Flatten(path))
        {
            switch (kind)
            {
                case 'M': sink.BeginFigure(); sink.MoveTo(pt.X, -pt.Y); break;
                case 'L': sink.LineTo(pt.X, -pt.Y); break;
            }
        }
        sink.EndFigure();
        return width;
    }

    private static IEnumerable<(PointF Pt, char Kind)> Flatten(GraphicsPath path)
    {
        var types = path.PathTypes;
        var pts = path.PathPoints;
        for (int i = 0; i < pts.Length; i++)
            yield return (pts[i], (types[i] & 0x07) == 0 ? 'M' : 'L');
    }
}
