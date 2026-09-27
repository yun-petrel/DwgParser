using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.InteropServices;

using DwgParser.Core.Render;
namespace DwgParser.Rendering;

public static class GdiTextRenderer
{
    public static void DrawText(PixelBuffer buf, int x, int y,
        string text, byte red, byte grn, byte blu, int fontSizePx, double rotationRad = 0,
        string? fontFamily = null, double widthFactor = 1.0,
        int hAlign = 0, int vAlign = 0, double lineGapFactor = 1.0,
        double charHeightPx = 0)
    {
        if (string.IsNullOrEmpty(text)) return;
        fontSizePx = Math.Clamp(fontSizePx, TextRasterLimits.MinFontPx,
                                Math.Max(TextRasterLimits.MinFontPx, buf.Height));
        if (fontSizePx < TextRasterLimits.MinFontPx) return;

        byte[]? cov = buf.Coverage;
        byte[]? before = cov != null ? (byte[])buf.Pixels.Clone() : null;

        using var bmp = new Bitmap(buf.Width, buf.Height, PixelFormat.Format32bppArgb);
        var bmpData = bmp.LockBits(new Rectangle(0, 0, buf.Width, buf.Height),
            ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
        try
        {
            int rowBytes = buf.Width * 4;
            for (int row = 0; row < buf.Height; row++)
                Marshal.Copy(buf.Pixels, row * rowBytes,
                    bmpData.Scan0 + row * bmpData.Stride, rowBytes);
        }
        finally
        {
            bmp.UnlockBits(bmpData);
        }

        using var gfx = Graphics.FromImage(bmp);
        gfx.SmoothingMode = SmoothingMode.AntiAlias;
        gfx.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

        var color = Color.FromArgb(red, grn, blu);
        using var brush = new SolidBrush(color);
        var fontName = string.IsNullOrWhiteSpace(fontFamily) ? GetBestFontName() : fontFamily;
        using var font = new Font(fontName, fontSizePx, FontStyle.Regular, GraphicsUnit.Pixel);

        double ascentPx = fontSizePx;
        try
        {
            using var fam = new FontFamily(fontName);
            int emH = fam.GetEmHeight(FontStyle.Regular);
            int asc = fam.GetCellAscent(FontStyle.Regular);
            if (emH > 0 && asc > 0) ascentPx = fontSizePx * (double)asc / emH;
        }
        catch { }

        gfx.TranslateTransform(x, y);
        if (Math.Abs(rotationRad) > 1e-9)
            gfx.RotateTransform((float)(-rotationRad * 180.0 / Math.PI));
        if (Math.Abs(widthFactor - 1.0) > 1e-9)
            gfx.ScaleTransform((float)widthFactor, 1f);

        var lines = text.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
        DrawLines(gfx, font, brush, lines, fontSizePx, ascentPx, hAlign, vAlign, lineGapFactor,
            charHeightPx);
        gfx.ResetTransform();

        bmpData = bmp.LockBits(new Rectangle(0, 0, buf.Width, buf.Height),
            ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            int rowBytes = buf.Width * 4;
            for (int row = 0; row < buf.Height; row++)
                Marshal.Copy(bmpData.Scan0 + row * bmpData.Stride,
                    buf.Pixels, row * rowBytes, rowBytes);
        }
        finally
        {
            bmp.UnlockBits(bmpData);
        }

        MarkCoverageDiff(buf, before);
    }

    public static void DrawText(PixelBuffer buf, int x, int y,
        string text, byte red, byte grn, byte blu, int fontSizePx, double rotationRad)
        => DrawText(buf, x, y, text, red, grn, blu, fontSizePx, rotationRad, null, 1.0, 0, 0, 1.0);

    public static void DrawMTextRuns(PixelBuffer buf, int x, int y,
        IReadOnlyList<MTextParser.Line> lines, byte red, byte grn, byte blu,
        int baseFontPx, double rotationRad, string defaultFamily,
        double widthFactor, int hAlign, int vAlign, double lineGapFactor,
        double worldToPxScale = 1.0, double charHeightPx = 0)
    {
        if (lines.Count == 0) return;
        baseFontPx = Math.Clamp(baseFontPx, TextRasterLimits.MinFontPx,
                                Math.Max(TextRasterLimits.MinFontPx, buf.Height));

        byte[]? cov = buf.Coverage;
        byte[]? before = cov != null ? (byte[])buf.Pixels.Clone() : null;

        using var bmp = new Bitmap(buf.Width, buf.Height, PixelFormat.Format32bppArgb);
        var bmpData = bmp.LockBits(new Rectangle(0, 0, buf.Width, buf.Height),
            ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
        try
        {
            int rowBytes = buf.Width * 4;
            for (int row = 0; row < buf.Height; row++)
                Marshal.Copy(buf.Pixels, row * rowBytes,
                    bmpData.Scan0 + row * bmpData.Stride, rowBytes);
        }
        finally
        {
            bmp.UnlockBits(bmpData);
        }

        using var gfx = Graphics.FromImage(bmp);
        gfx.SmoothingMode = SmoothingMode.AntiAlias;
        gfx.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

        gfx.TranslateTransform(x, y);
        if (Math.Abs(rotationRad) > 1e-9)
            gfx.RotateTransform((float)(-rotationRad * 180.0 / Math.PI));

        double lineGap = ShxTextShapes.LinePitch(
            ShxTextShapes.PitchBase(charHeightPx, baseFontPx), lineGapFactor);
        double totalH = ShxTextShapes.BlockHeight(lineGap, baseFontPx, lines.Count);
        double topOffset = vAlign switch
        {
            1 => -totalH,
            2 => -totalH / 2,
            3 => 0,
            _ => 0,
        };

        using var fmt = StringFormat.GenericTypographic;
        using var baseBrush = new SolidBrush(Color.FromArgb(red, grn, blu));

        for (int li = 0; li < lines.Count; li++)
        {
            var runs = lines[li].Runs;
            if (runs.Count == 0) continue;

            var stops = lines[li].TabStops;
            bool useTabs = stops is { Count: > 0 };
            var segs = new List<(MTextParser.Run Run, string Text, bool Tab)>();
            foreach (var run in runs)
            {
                if (run.IsStack) { segs.Add((run, "", false)); continue; }
                if (run.Text.Length == 0) continue;
                if (!useTabs) { segs.Add((run, run.Text, false)); continue; }
                var parts = run.Text.Split('\t');
                for (int k = 0; k < parts.Length; k++)
                {
                    if (k == 0) { if (parts[0].Length > 0) segs.Add((run, parts[0], false)); }
                    else segs.Add((run, parts[k], true));
                }
            }

            float lineWidth = 0;
            var measured = new List<(MTextParser.Run Run, string Text, float X, float Width, float Px, FontStyle Style, Brush Brush)>();
            foreach (var (run, text, tab) in segs)
            {
                if (tab)
                {
                    var nx = MTextParser.NextTabStopPx(stops, worldToPxScale, lineWidth);
                    if (nx.HasValue) lineWidth = Math.Max(lineWidth, (float)nx.Value);
                }
                float px = run.AbsoluteHeight.HasValue
                    ? Math.Max((float)TextRasterLimits.MinFontPx, (float)(run.AbsoluteHeight.Value * worldToPxScale))
                    : Math.Max((float)TextRasterLimits.MinFontPx, baseFontPx * (float)run.HeightScale);
                string family = FontResolver.ResolveRunFamily(run.FontSpec, defaultFamily);
                var style = FontStyle.Regular |
                    (run.Bold ? FontStyle.Bold : 0) |
                    (run.Italic ? FontStyle.Italic : 0) |
                    (run.Underline ? FontStyle.Underline : 0) |
                    (run.StrikeThrough ? FontStyle.Strikeout : 0);
                using var f = new Font(family, px, style, GraphicsUnit.Pixel);
                double runWf = run.WidthFactor ?? widthFactor;
                float w;
                if (run.IsStack)
                {
                    float wt = gfx.MeasureString(run.StackTop, f, PointF.Empty, fmt).Width * (float)runWf;
                    float wb = gfx.MeasureString(run.StackBottom, f, PointF.Empty, fmt).Width * (float)runWf;
                    w = Math.Max(wt, wb);
                }
                else
                    w = gfx.MeasureString(text, f, PointF.Empty, fmt).Width * (float)runWf;
                measured.Add((run, text, lineWidth, w, px, style, ColorForRun(run, baseBrush)));
                lineWidth += w;
            }

            float cx0 = hAlign switch { 1 or 4 => -lineWidth / 2, 2 => -lineWidth, _ => 0 };
            float lineTop = (float)(topOffset + li * lineGap);

            foreach (var (run, text, segX, w, px, style, brush) in measured)
            {
                float cx = cx0 + segX;
                using var f = new Font(
                    FontResolver.ResolveRunFamily(run.FontSpec, defaultFamily), px, style, GraphicsUnit.Pixel);
                double runWf = run.WidthFactor ?? widthFactor;
                var state = gfx.Save();
                if (Math.Abs(runWf - 1.0) > 1e-6)
                    gfx.ScaleTransform((float)runWf, 1f);
                if (run.Oblique.HasValue && Math.Abs(run.Oblique.Value) > 1e-6)
                {
                    var m = new System.Drawing.Drawing2D.Matrix();
                    m.Shear((float)Math.Tan(-run.Oblique.Value), 0f);
                    gfx.MultiplyTransform(m);
                    m.Dispose();
                }
                if (run.IsStack)
                {
                    float centerY = lineTop + baseFontPx / 2f;
                    gfx.DrawString(run.StackTop, f, brush, cx / (float)runWf, centerY - px, fmt);
                    gfx.DrawString(run.StackBottom, f, brush, cx / (float)runWf, centerY, fmt);
                    var barColor = (brush as SolidBrush)?.Color ?? Color.FromArgb(red, grn, blu);
                    using var barPen = new Pen(barColor, Math.Max(1f, px * 0.06f));
                    if (run.StackKind == '/')
                        gfx.DrawLine(barPen,
                            cx / (float)runWf, centerY, (cx + w) / (float)runWf, centerY);
                    else if (run.StackKind == '#')
                        gfx.DrawLine(barPen,
                            cx / (float)runWf, centerY + px * 0.6f, (cx + w) / (float)runWf, centerY - px * 0.6f);
                    gfx.Restore(state);
                    continue;
                }
                gfx.DrawString(text, f, brush, cx / (float)runWf, lineTop, fmt);
                gfx.Restore(state);
            }

            foreach (var m in measured)
                if (!ReferenceEquals(m.Brush, baseBrush))
                    m.Brush.Dispose();
        }
        baseBrush.Dispose();
        gfx.ResetTransform();

        bmpData = bmp.LockBits(new Rectangle(0, 0, buf.Width, buf.Height),
            ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            int rowBytes = buf.Width * 4;
            for (int row = 0; row < buf.Height; row++)
                Marshal.Copy(bmpData.Scan0 + row * bmpData.Stride,
                    buf.Pixels, row * rowBytes, rowBytes);
        }
        finally
        {
            bmp.UnlockBits(bmpData);
        }

        MarkCoverageDiff(buf, before);
    }

    private static void MarkCoverageDiff(PixelBuffer buf, byte[]? before)
    {
        var cov = buf.Coverage;
        if (cov == null || before == null) return;
        var px = buf.Pixels;
        for (int i = 0; i < px.Length; i += 4)
        {
            if (px[i] == before[i] && px[i + 1] == before[i + 1] && px[i + 2] == before[i + 2])
                continue;
            cov[i / 4] = 1;
        }
    }

    private static Brush ColorForRun(MTextParser.Run run, Brush baseBrush)
    {
        if (run.AciColor.HasValue)
        {
            var c = FromHex(RenderContext.AciColor(run.AciColor.Value));
            var (mr, mg, mb) = RenderInk.Map(c.R, c.G, c.B);
            c = Color.FromArgb(mr, mg, mb);
            return new SolidBrush(c);
        }
        return baseBrush;
    }

    private static Color FromHex(string hex)
    {
        if (hex.Length < 7) return Color.Black;
        return Color.FromArgb(Convert.ToInt32(hex.Substring(1, 2), 16),
            Convert.ToInt32(hex.Substring(3, 2), 16), Convert.ToInt32(hex.Substring(5, 2), 16));
    }

    private static void DrawLines(Graphics gfx, Font font, Brush brush, string[] lines,
        int fontPx, double ascentPx, int hAlign, int vAlign, double lineGapFactor,
        double charHeightPx = 0)
    {
        double lineGap = ShxTextShapes.LinePitch(
            ShxTextShapes.PitchBase(charHeightPx, fontPx), lineGapFactor);
        double totalH = ShxTextShapes.BlockHeight(lineGap, fontPx, lines.Length);

        double topOffset = vAlign switch
        {
            0 => -ascentPx,
            1 => -totalH,
            2 => -totalH / 2,
            3 => 0,
            _ => -ascentPx,
        };

        using var fmt = StringFormat.GenericTypographic;
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            double y = topOffset + i * lineGap;
            double x = 0;
            if (hAlign == 1 || hAlign == 4)
            {
                float w = gfx.MeasureString(line, font, PointF.Empty, fmt).Width;
                x = -w / 2;
            }
            else if (hAlign == 2)
            {
                float w = gfx.MeasureString(line, font, PointF.Empty, fmt).Width;
                x = -w;
            }
            gfx.DrawString(line, font, brush, (float)x, (float)y, fmt);
        }
    }

    private static string GetBestFontName()
    {
        try
        {
            var families = FontFamily.Families;
            string[] preferred =
            [
                "Microsoft YaHei",
                "SimHei",
                "SimSun",
                "Source Han Sans SC",
                "Noto Sans CJK SC",
                "Arial",
            ];
            foreach (var name in preferred)
                foreach (var f in families)
                    if (f.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                        return f.Name;
            return families.Length > 0 ? families[0].Name : "Arial";
        }
        catch
        {
            return "Arial";
        }
    }
}

public interface ITextRendererBackend
{
    string Name { get; }

    void DrawText(PixelBuffer buf, int x, int y,
        string text, byte red, byte grn, byte blu, int fontSizePx, double rotationRad = 0,
        string? fontFamily = null, double widthFactor = 1.0,
        int hAlign = 0, int vAlign = 0, double lineGapFactor = 1.0,
        double charHeightPx = 0);

    void DrawMTextRuns(PixelBuffer buf, int x, int y,
        IReadOnlyList<MTextParser.Line> lines, byte red, byte grn, byte blu,
        int baseFontPx, double rotationRad, string defaultFamily,
        double widthFactor, int hAlign, int vAlign, double lineGapFactor,
        double worldToPxScale = 1.0, double charHeightPx = 0);

    double MeasureTextWidth(string text, string? fontFamily, int fontSizePx, double widthFactor,
        double worldToPxScale = 1.0);
}

public interface IGlyphOutlineSink
{
    void BeginFigure();
    void MoveTo(double x, double y);
    void LineTo(double x, double y);
    void QuadraticBezierTo(double cx, double cy, double x, double y);
    void CubicBezierTo(double c1x, double c1y, double c2x, double c2y, double x, double y);
    void EndFigure();
}

public interface ITextShapeBackend
{
    string Name { get; }

    double GetTextOutline(string text, string? fontFamily, int fontSizePx,
        double widthFactor, IGlyphOutlineSink sink, out string resolved);
}
