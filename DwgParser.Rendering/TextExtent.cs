using DwgParser.Core.Objects;
using DwgParser.Core.Render;

namespace DwgParser.Rendering;

public static class TextExtent
{
    private const double ProbeEm = 100.0;

    private static readonly (double ShiftEm, double AscentEm, double DescentEm) Metrics = Calibrate();

    private const double PadEm = 0.10;

    private const double BoldWidthFactor = 1.08;

    private const double FallbackAscentEm = 0.90, FallbackDescentEm = 0.21;

    private const double FallbackHeight = 2.5;

    private static (double ShiftEm, double AscentEm, double DescentEm) Calibrate()
    {
        string[] probes = ["Hg汉", "Agq汉", "1g汉", "(g汉", "jg汉", "/g汉", "汉"];
        try
        {
            const int fontPx = 64;
            int w = 512, h = 256, penX = 48, penY = 160;
            double shift = double.MaxValue, ascent = 0, descent = 0;
            bool any = false;
            foreach (string probe in probes)
            {
                var buf = new PixelBuffer(w, h);
                buf.Clear(255, 255, 255);
                TextBackends.Current.DrawText(buf, penX, penY, probe, 0, 0, 0, fontPx,
                    0, "Arial", 1.0, 0, 0, 1.0, fontPx);
                var p = buf.Pixels;
                int minX = int.MaxValue, minY = int.MaxValue, maxY = int.MinValue;
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        int o = (y * w + x) * 4;
                        if (p[o] < 128 && p[o + 1] < 128 && p[o + 2] < 128)
                        {
                            if (x < minX) minX = x;
                            if (y < minY) minY = y;
                            if (y > maxY) maxY = y;
                        }
                    }
                if (minX == int.MaxValue) continue;
                any = true;
                shift = Math.Min(shift, (minX - penX) / (double)fontPx);
                ascent = Math.Max(ascent, (penY - minY) / (double)fontPx);
                descent = Math.Max(descent, (maxY - penY) / (double)fontPx);
            }
            if (!any) return (0, FallbackAscentEm, FallbackDescentEm);

            if (!(shift is > -0.2 and < 0.5)) shift = 0;
            if (!(ascent is > 0.3 and < 2.0)) ascent = FallbackAscentEm;
            if (!(descent is > 0 and < 1.0)) descent = FallbackDescentEm;
            return (shift, ascent, descent);
        }
        catch { return (0, FallbackAscentEm, FallbackDescentEm); }
    }

    public static bool IsTextFamily(DwgEntityType kind)
        => kind is DwgEntityType.Text or DwgEntityType.MText
                or DwgEntityType.AttDef or DwgEntityType.Attrib;

    public static (double MinX, double MinY, double MaxX, double MaxY)? BoxOf(
        DwgEntity e, RenderContext ctx, ITextRendererBackend text,
        IReadOnlyList<ShxFont>? bypass = null)
    {
        if (string.IsNullOrWhiteSpace(e.TextValue)) return null;

        string body = RenderContext.SanitizeText(e.TextValue)
                                    .Replace("\r\n", "\n").Replace("\r", "\n");
        var lines = body.Split('\n');
        if (lines.Length == 0) return null;

        double h = e.Height > 0 && double.IsFinite(e.Height) ? e.Height : FallbackHeight;
        double em = h * ShxTextShapes.EmFactor;

        bool mt = e.Kind == DwgEntityType.MText;
        var al = Align(e);

        var style = ctx.StyleFonts.TryGetValue(e.TextStyleHandle, out var s) ? s : null;
        string family = ctx.ResolveFontFamily(e.TextStyleHandle, body);
        double wf = e.WidthFactor > 0 && Math.Abs(e.WidthFactor - 1.0) > 1e-9
            ? e.WidthFactor
            : style is { WidthFactor: > 0 } ? style.WidthFactor : 1.0;

        var parsed = mt ? MTextParser.Parse(e.TextValue) : null;
        if (parsed is { Count: > 0 } && MTextParser.HasFormatting(parsed))
        {
            var segs = PngDrawDevice.BuildShxRunSegs(parsed, style, h, wf, 1.0, 0, 0, 0, 0);
            if (segs is not null)
            {
                double below = 0;
                foreach (var line in segs)
                    foreach (var sg in line)
                        below = Math.Max(below, ShxTextShapes.Below(sg.Main, sg.Big, sg.CapPx));
                return Horiz(new Measured(ShxTextLayout.MeasureLines(segs), segs.Count, below), e, al, ttf: false);
            }
            return Horiz(MeasureTtfRuns(parsed, text, family, wf, em, h), e, al, ttf: true);
        }

        var (main, big) = PngDrawDevice.ResolveShxFonts(style, body, bypass);
        if (style?.IsVertical == true && (main is not null || big is not null))
        {
            var (cw, chh) = ShxTextLayout.MeasureVertical(main, big, body, h, wf, al.Gap);
            return Vert(cw, chh, em, e);
        }
        if (main is not null || big is not null)
        {
            double maxW = 0;
            foreach (var line in lines)
                maxW = Math.Max(maxW, ShxTextShapes.Measure(
                    new ShxTextShapes.Seg(line, main, big, h, wf)));
            return Horiz(new Measured(maxW, lines.Length, ShxTextShapes.Below(main, big, h)), e, al, ttf: false);
        }

        double tw = 0;
        foreach (var line in lines) tw = Math.Max(tw, WidthOf(text, line, family, wf, em));
        return Horiz(new Measured(tw, lines.Length, Metrics.DescentEm * em), e, al, ttf: true);
    }

    private readonly record struct Measured(double Width, int Lines, double Below);

    private static Measured MeasureTtfRuns(IReadOnlyList<MTextParser.Line> parsed,
        ITextRendererBackend text, string family, double wf, double em, double h)
    {
        double maxW = 0;
        foreach (var line in parsed)
        {
            double lw = 0;
            foreach (var run in line.Runs)
            {
                double emRun = run.AbsoluteHeight.HasValue
                    ? run.AbsoluteHeight.Value * h
                    : em * run.HeightScale;
                if (!(emRun > 0) || !double.IsFinite(emRun)) emRun = em;
                string rf = FontResolver.ResolveRunFamily(run.FontSpec, family);
                double styleFactor = run.Bold ? BoldWidthFactor : 1.0;
                if (run.IsStack)
                {
                    lw += styleFactor * Math.Max(WidthOf(text, run.StackTop, rf, wf, emRun),
                                                 WidthOf(text, run.StackBottom, rf, wf, emRun));
                    continue;
                }
                lw += styleFactor * WidthOf(text, run.Text, rf, wf, emRun);
            }
            if (lw > maxW) maxW = lw;
        }
        return new Measured(maxW, parsed.Count, 0);
    }

    private static (int H, int V, double Gap) Align(DwgEntity e)
    {
        if (e.Kind != DwgEntityType.MText)
            return (e.HorizMode switch { 1 => 1, 2 => 2, 4 => 1, _ => 0 },
                    e.VertMode switch { 1 => 1, 2 => 2, 3 => 3, _ => 0 }, 1.0);

        int att = e.TextAttachment is >= 1 and <= 9 ? e.TextAttachment : 1;
        int hAlign = (att - 1) % 3;
        int vAlign = ((att - 1) / 3) switch { 0 => 3, 1 => 2, _ => 1 };
        return (hAlign, vAlign, e.LineSpacingFactor > 0 ? e.LineSpacingFactor : 1.0);
    }

    private static (double MinX, double MinY, double MaxX, double MaxY)? Horiz(
        Measured m, DwgEntity e, (int H, int V, double Gap) al, bool ttf)
    {
        bool shxPath = !ttf;
        double h = e.Height > 0 && double.IsFinite(e.Height) ? e.Height : FallbackHeight;
        double em = h * ShxTextShapes.EmFactor;
        double w = m.Width;
        if (!(w > 0) || !double.IsFinite(w)) w = h;

        double pitch = h * ShxTextShapes.PitchFactor * ShxTextShapes.ViewerAtLeastK * al.Gap;
        double totalH = ShxTextShapes.BlockHeight(pitch, em, m.Lines);

        double pad = PadEm * em;
        double shift = ttf ? Metrics.ShiftEm * em : 0;
        double lx0 = al.H switch { 1 => -w / 2, 2 => -w, _ => 0 } + shift - pad;
        double lx1 = al.H switch { 1 => w / 2, 2 => 0, _ => w } + shift + pad;

        double lyTop, lyBottom;
        if (shxPath)
        {
            double cap = h;
            double desc = m.Below;
            double baseLy = al.V switch
            {
                1 => totalH - em,
                2 => totalH / 2 - em,
                3 => -em,
                _ => 0,
            };
            lyTop = baseLy + cap;
            lyBottom = baseLy - (m.Lines - 1) * pitch - desc;
        }
        else
        {
            double ascent = Metrics.AscentEm * em;
            double descent = Metrics.DescentEm * em;
            double inkSpan = ascent + (m.Lines - 1) * pitch + descent;
            lyTop = al.V switch
            {
                1 => totalH,
                2 => totalH / 2,
                3 => 0,
                _ => ascent,
            };
            lyBottom = lyTop - inkSpan;
        }
        return Quad(e, lx0, lx1, lyTop, lyBottom);
    }

    private static (double MinX, double MinY, double MaxX, double MaxY)? Vert(double colBlockW, double colAdvance, double em, DwgEntity e)
    {
        double h = e.Height > 0 && double.IsFinite(e.Height) ? e.Height : FallbackHeight;
        return Quad(e, -colBlockW + em, em, h, -colAdvance - Metrics.DescentEm * em);
    }

    private static (double MinX, double MinY, double MaxX, double MaxY)? Quad(DwgEntity e, double lx0, double lx1, double lyTop, double lyBottom)
    {
        double ax0 = e.Start.X, ay0 = e.Start.Y;
        if (e.Kind != DwgEntityType.MText)
        {
            if (e.HorizMode != 0) ax0 = e.AlignmentPoint.X;
            if (e.HorizMode != 0 || e.VertMode != 0) ay0 = e.AlignmentPoint.Y;
        }

        double cos = Math.Cos(e.Rotation), sin = Math.Sin(e.Rotation);
        double minX = double.MaxValue, minY = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue;
        foreach (var (lx, ly) in new[] { (lx0, lyTop), (lx1, lyTop), (lx1, lyBottom), (lx0, lyBottom) })
        {
            double wx = ax0 + lx * cos - ly * sin;
            double wy = ay0 + lx * sin + ly * cos;
            if (wx < minX) minX = wx;
            if (wy < minY) minY = wy;
            if (wx > maxX) maxX = wx;
            if (wy > maxY) maxY = wy;
        }
        return (minX, minY, maxX, maxY);
    }

    private static double WidthOf(ITextRendererBackend text, string s,
        string family, double wf, double emWorld)
    {
        if (string.IsNullOrEmpty(s)) return 0;
        double px = text.MeasureTextWidth(s, family, (int)ProbeEm, wf);
        return px <= 0 ? 0 : px * (emWorld / ProbeEm);
    }
}
