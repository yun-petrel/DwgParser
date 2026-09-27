using DwgParser.Core.Render;

namespace DwgParser.Rendering;

internal static class ShxTextLayout
{
    private const double EmFactor = DwgParser.Core.Render.ShxTextShapes.EmFactor;

    internal sealed record Seg(string Text, ShxFont? Main, ShxFont? Big, double CapPx, double Wf,
        byte R, byte G, byte B);

    private static ShxFont? Pick(ShxFont? main, ShxFont? big, char ch)
        => ShxTextShapes.Pick(main, big, ch);

    private static ShxTextShapes.Seg Core(Seg s, double wf) => new(s.Text, s.Main, s.Big, s.CapPx, wf);

    public static bool Covers(ShxFont? main, ShxFont? big, string text)
        => ShxTextShapes.Covers(main, big, text);

    public static void DrawPlain(PixelBuffer buf, ShxFont? main, ShxFont? big, string text,
        int x, int y, double capPx, double rotationRad, byte r, byte g, byte b,
        double wf, int hAlign, int vAlign, double lineGapFactor)
    {
        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n')
            .Replace("\\P", "\n").Split('\n');
        var segLines = new List<Seg[]>();
        foreach (var l in lines)
            segLines.Add(new[] { new Seg(l, main, big, capPx, wf, r, g, b) });
        DrawLines(buf, segLines, x, y, capPx, rotationRad, hAlign, vAlign, lineGapFactor);
    }

    public static void DrawLines(PixelBuffer buf, IReadOnlyList<Seg[]> lines,
        int x, int y, double baseCapPx, double rotationRad,
        int hAlign, int vAlign, double lineGapFactor)
    {
        if (lines.Count == 0) return;
        baseCapPx = Math.Clamp(baseCapPx, TextRasterLimits.MinFontPx,
                               Math.Max(TextRasterLimits.MinFontPx, buf.Height));
        double emPx = baseCapPx * EmFactor;
        double pitch = ShxTextShapes.LinePitch(baseCapPx, lineGapFactor);
        double totalH = ShxTextShapes.BlockHeight(pitch, emPx, lines.Count);
        double topOffset = vAlign switch { 1 => -totalH, 2 => -totalH / 2, _ => 0 };
        int pen = 1;

        for (int li = 0; li < lines.Count; li++)
        {
            var segs = lines[li];
            double lineWidth = MeasureLine(segs);
            double cursor = hAlign switch { 1 or 4 => -lineWidth / 2, 2 => -lineWidth, _ => 0 };
            double baselineLy = vAlign == 0
                ? -(li * pitch)
                : -(topOffset + li * pitch + emPx);
            foreach (var s in segs)
                cursor += DrawSegment(buf, s, x, y, cursor, baselineLy, rotationRad, pen);
        }
    }

    private static double Measure(Seg s, double wf) => ShxTextShapes.Measure(Core(s, wf));

    internal static double MeasureLine(IReadOnlyList<Seg> segs)
    {
        double w = 0;
        foreach (var s in segs) w += ShxTextShapes.Measure(Core(s, s.Wf));
        return w;
    }

    internal static double MeasureLines(IReadOnlyList<Seg[]> lines)
    {
        double max = 0;
        foreach (var segs in lines)
        {
            double w = MeasureLine(segs);
            if (w > max) max = w;
        }
        return max;
    }

    private static double MissingAdvance(ShxFont font) => ShxTextShapes.MissingAdvance(font);

    private static double DrawSegment(PixelBuffer buf, Seg s, int ax, int ay, double originLx,
        double baselineLy, double rotationRad, int pen)
    {
        double cos = Math.Cos(rotationRad), sin = Math.Sin(rotationRad);
        foreach (var pts in ShxTextShapes.Strokes(Core(s, s.Wf)))
        {
            double px = 0, py = 0;
            for (int i = 0; i < pts.Count / 2; i++)
            {
                double lx = originLx + pts[2 * i];
                double ly = baselineLy + pts[2 * i + 1];
                double sx = ax + lx * cos - ly * sin;
                double sy = ay - (lx * sin + ly * cos);
                if (i > 0) buf.DrawLine((int)Math.Round(px), (int)Math.Round(py),
                    (int)Math.Round(sx), (int)Math.Round(sy), s.R, s.G, s.B, pen);
                px = sx; py = sy;
            }
        }
        return ShxTextShapes.Measure(Core(s, s.Wf));
    }

    public static void DrawVertical(PixelBuffer buf, ShxFont? main, ShxFont? big, string text,
        int x, int y, double capPx, double rotationRad, byte r, byte g, byte b,
        double wf, double colGapFactor)
    {
        capPx = Math.Clamp(capPx, TextRasterLimits.MinFontPx,
                           Math.Max(TextRasterLimits.MinFontPx, buf.Height));
        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n')
            .Replace("\\P", "\n").Split('\n');
        double emPx = capPx * EmFactor;
        double colPitch = emPx * 1.66 * (colGapFactor > 0 ? colGapFactor : 1.0);
        int pen = Math.Max(1, (int)Math.Round(capPx * 0.06));
        double cos = Math.Cos(rotationRad), sin = Math.Sin(rotationRad);

        for (int ci = 0; ci < lines.Length; ci++)
        {
            double colLx = -ci * colPitch;
            double curX = colLx, curY = 0;
            foreach (char ch in lines[ci])
            {
                ShxFont? font = Pick(main, big, ch);
                if (font is null) { curY -= capPx * 0.5; continue; }
                bool cjk = font.IsBigFont;
                ShxGlyph? gly = font.DecodeChar(ch, vertical: cjk);
                double fScale = capPx / Math.Max(1, font.Above);
                if (gly is null) { curY -= cjk ? capPx : capPx * 0.5; continue; }
                if (cjk)
                {
                    foreach (var stroke in gly.Strokes)
                        StrokeTransformed(buf, stroke, ax: x, ay: y, cursorLx: curX, baselineLy: curY,
                            fScale, wf, cos, sin, r, g, b, pen, rotate90Cw: false);
                    double advY = gly.AdvanceY != 0 ? -gly.AdvanceY * fScale : capPx;
                    curX += gly.Advance * fScale * wf;
                    curY -= advY;
                }
                else
                {
                    foreach (var stroke in gly.Strokes)
                        StrokeTransformed(buf, stroke, ax: x, ay: y, cursorLx: curX, baselineLy: curY,
                            fScale, wf, cos, sin, r, g, b, pen, rotate90Cw: true);
                    curY -= gly.Advance * fScale * wf;
                }
            }
        }
    }

    internal static (double Width, double Height) MeasureVertical(ShxFont? main, ShxFont? big,
        string text, double capPx, double wf, double colGapFactor)
    {
        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n')
            .Replace("\\P", "\n").Split('\n');
        double emPx = capPx * EmFactor;
        double colPitch = emPx * 1.66 * (colGapFactor > 0 ? colGapFactor : 1.0);
        double maxCol = 0;
        foreach (var line in lines)
        {
            double run = 0;
            foreach (char ch in line)
            {
                ShxFont? font = Pick(main, big, ch);
                if (font is null) { run += capPx * 0.5; continue; }
                bool cjk = font.IsBigFont;
                ShxGlyph? gly = font.DecodeChar(ch, vertical: cjk);
                double fScale = capPx / Math.Max(1, font.Above);
                if (gly is null) { run += cjk ? capPx : capPx * 0.5; continue; }
                run += cjk
                    ? (gly.AdvanceY != 0 ? gly.AdvanceY * fScale : capPx)
                    : gly.Advance * fScale * wf;
            }
            if (run > maxCol) maxCol = run;
        }
        double width = lines.Length <= 1 ? emPx : (lines.Length - 1) * colPitch + emPx;
        return (width, maxCol);
    }

    private static void StrokeTransformed(PixelBuffer buf, List<double> pts, int ax, int ay,
        double cursorLx, double baselineLy, double fScale, double wf, double cos, double sin,
        byte r, byte g, byte b, int pen, bool rotate90Cw)
    {
        int n = pts.Count / 2;
        if (n < 2) return;
        double px = 0, py = 0;
        for (int i = 0; i < n; i++)
        {
            double gx = pts[2 * i] * fScale * wf, gy = pts[2 * i + 1] * fScale;
            if (rotate90Cw) (gx, gy) = (gy, -gx);
            double lx = cursorLx + gx;
            double ly = baselineLy + gy;
            double sx = ax + lx * cos - ly * sin;
            double sy = ay - (lx * sin + ly * cos);
            if (i > 0) buf.DrawLine((int)Math.Round(px), (int)Math.Round(py),
                (int)Math.Round(sx), (int)Math.Round(sy), r, g, b, pen);
            px = sx; py = sy;
        }
    }

}
