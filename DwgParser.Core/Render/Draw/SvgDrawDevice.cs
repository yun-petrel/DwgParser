using System.Globalization;
using System.Text;
using DwgParser.Core.Objects;

namespace DwgParser.Core.Render;

public sealed class SvgDrawDevice : IDrawDevice
{
    private const string Indent = "    ";
    private const string NonScaling = " vector-effect=\"non-scaling-stroke\"";

    private static string DashAttr(StrokeStyle stroke, double pathLen)
    {
        if (!stroke.Dashed) return "";
        if (!stroke.HasDashPattern) return " stroke-dasharray=\"4 4\"";
        string off = "";
        double phi = DashSampler.CenteredPhase(stroke.DashPattern, pathLen);
        if (phi > 1e-9) off = $" stroke-dashoffset=\"{F(phi)}\"";
        return $" stroke-dasharray=\"{DashList(stroke.DashPattern!)}\"" + off;
    }

    private static string DashList(double[] pat)
    {
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < pat.Length; i++)
        {
            if (i > 0) sb.Append(' ');
            sb.Append(F(Math.Abs(pat[i])));
        }
        return sb.ToString();
    }

    private readonly RenderContext? _ctx;
    private readonly StringBuilder _sb = new();
    private DrawViewport _view;

    public SvgDrawDevice(RenderContext? ctx = null) => _ctx = ctx;

    public string Body => _sb.ToString();

    public void BeginPass(DrawViewport view) => _view = view;
    public void EndPass() { }

    private static string F(double v) => RenderContext.F(v);
    private static string Hex(uint rgba) => Rgba.ToHex(rgba);

    private static string FillAttr(FillStyle f, uint strokeRgba)
        => !f.IsFill ? " fill=\"none\""
         : f.Opacity >= 0.999 ? $" fill=\"{Hex(f.Rgba)}\""
         : $" fill=\"{Hex(f.Rgba)}\" fill-opacity=\"{F(f.Opacity)}\"";

    private void Expand(double x, double y) => _ctx?.Expand(x, y);

    public void DrawLine(Vec2 a, Vec2 b, StrokeStyle stroke)
    {
        Expand(a.X, a.Y); Expand(b.X, b.Y);
        double dx = b.X - a.X, dy = b.Y - a.Y;
        _sb.Append($"{Indent}<line x1=\"{F(a.X)}\" y1=\"{F(a.Y)}\" x2=\"{F(b.X)}\" y2=\"{F(b.Y)}\" "
            + $"stroke=\"{Hex(stroke.Rgba)}\" stroke-width=\"1\""
            + DashAttr(stroke, Math.Sqrt(dx * dx + dy * dy))
            + (stroke.NonScaling ? NonScaling : "") + "/>\n");
    }

    public void DrawRect(Vec2 a, Vec2 b, StrokeStyle stroke)
    {
        Expand((a.X + b.X) / 2, (a.Y + b.Y) / 2);
        Expand(a.X, a.Y); Expand(b.X, b.Y);
        _sb.Append($"{Indent}<rect x=\"{F(a.X)}\" y=\"{F(a.Y)}\" width=\"{F(b.X - a.X)}\" "
            + $"height=\"{F(b.Y - a.Y)}\" stroke=\"{Hex(stroke.Rgba)}\" stroke-width=\"1\""
            + (stroke.NonScaling ? NonScaling : "") + " fill=\"none\"/>\n");
    }

    public void DrawArc(Vec2 c, double r, double a0, double a1, StrokeStyle stroke)
    {
        var p1 = new Vec2(c.X + r * Math.Cos(a0), c.Y + r * Math.Sin(a0));
        var p2 = new Vec2(c.X + r * Math.Cos(a1), c.Y + r * Math.Sin(a1));
        Expand(p1.X, p1.Y); Expand(p2.X, p2.Y);
        for (int k = 0; k < 8; k++)
        {
            double a = a0 + (a1 - a0) * k / 8.0;
            Expand(c.X + r * Math.Cos(a), c.Y + r * Math.Sin(a));
        }
        bool large = a1 - a0 > Math.PI;
        bool sweep = a1 > a0;
        _sb.Append($"{Indent}<path d=\"M {F(p1.X)} {F(p1.Y)} A {F(r)} {F(r)} 0 "
            + $"{(large ? 1 : 0)} {(sweep ? 1 : 0)} {F(p2.X)} {F(p2.Y)}\" "
            + $"stroke=\"{Hex(stroke.Rgba)}\" stroke-width=\"1\""
            + DashAttr(stroke, r * Math.Abs(a1 - a0))
            + (stroke.NonScaling ? NonScaling : "") + " fill=\"none\"/>\n");
    }

    public void DrawCircle(Vec2 center, double radius, StrokeStyle stroke, FillStyle fill)
    {
        if (!fill.IsFill || fill.Stroked)
        {
            Expand(center.X - radius, center.Y - radius);
            Expand(center.X + radius, center.Y + radius);
            _sb.Append($"{Indent}<circle cx=\"{F(center.X)}\" cy=\"{F(center.Y)}\" r=\"{F(radius)}\" "
                + $"stroke=\"{Hex(stroke.Rgba)}\" stroke-width=\"1\""
                + DashAttr(stroke, 2 * Math.PI * radius)
                + (stroke.NonScaling ? NonScaling : "")
                + FillAttr(fill, stroke.Rgba) + "/>\n");
            return;
        }
        Expand(center.X, center.Y);
        _sb.Append($"{Indent}<circle cx=\"{F(center.X)}\" cy=\"{F(center.Y)}\" "
            + $"r=\"{F(radius)}\" fill=\"{Hex(fill.Rgba)}\"/>\n");
    }

    public void DrawDot(Vec2 center, int radiusPx, FillStyle fill)
    {
        if (!fill.IsFill) return;
        Expand(center.X, center.Y);
        _sb.Append($"{Indent}<circle cx=\"{F(center.X)}\" cy=\"{F(center.Y)}\" "
            + $"r=\"{F(radiusPx)}\" fill=\"{Hex(fill.Rgba)}\"/>\n");
    }

    public void DrawPolyline(ReadOnlyMemory<Vec2> pts, bool closed, StrokeStyle stroke, FillStyle fill)
    {
        var p = pts.Span;
        if (p.Length == 0) return;
        var d = new StringBuilder($"M {F(p[0].X)} {F(p[0].Y)}");
        Expand(p[0].X, p[0].Y);
        for (int i = 1; i < p.Length; i++)
        {
            d.Append($" L {F(p[i].X)} {F(p[i].Y)}");
            Expand(p[i].X, p[i].Y);
        }
        if (closed) d.Append(" Z");
        _sb.Append($"{Indent}<path d=\"{d}\" stroke=\"{Hex(stroke.Rgba)}\" stroke-width=\"1\""
            + (stroke.NonScaling ? NonScaling : "")
            + FillAttr(fill, stroke.Rgba) + "/>\n");
    }

    public void FillPolygon(ReadOnlyMemory<Vec2> ring, FillStyle fill)
    {
        var p = ring.Span;
        if (p.Length == 0) return;
        var pts = new StringBuilder();
        for (int i = 0; i < p.Length; i++)
        {
            pts.Append(pts.Length == 0 ? $"{F(p[i].X)},{F(p[i].Y)}" : $" {F(p[i].X)},{F(p[i].Y)}");
            Expand(p[i].X, p[i].Y);
        }
        _sb.Append($"{Indent}<polygon points=\"{pts}\""
            + (fill.Stroked ? $" stroke=\"{Hex(fill.Rgba)}\" stroke-width=\"1\"" : "")
            + FillAttr(fill, fill.Rgba) + "/>\n");
    }

    public void DrawPath(ReadOnlyMemory<PathSeg> segs, bool closed, StrokeStyle stroke, FillStyle fill, PathLayout layout)
    {
        var s = segs.Span;
        var d = new StringBuilder();
        foreach (var seg in s)
        {
            string cmd = seg.Kind switch
            {
                PathSegKind.Move => $"M {F(seg.P1.X)} {F(seg.P1.Y)}",
                PathSegKind.Line => $"L {F(seg.P2.X)} {F(seg.P2.Y)}",
                _ => BulgeCommand(seg),
            };
            var landing = seg.Kind == PathSegKind.Move ? seg.P1 : seg.P2;
            if (seg.InBounds) Expand(landing.X, landing.Y);
            if (cmd.Length == 0) continue;
            d.Append(d.Length == 0
                ? (layout == PathLayout.StrokeFirstSpaced ? " " + cmd : cmd)
                : " " + cmd);
        }
        if (closed) d.Append(" Z");
        if (d.Length == 0) return;
        string attrs = layout switch
        {
            PathLayout.FillFirst => $" fill=\"{Hex(fill.Rgba)}\" stroke=\"{Hex(stroke.Rgba)}\" stroke-width=\"1\"",
            PathLayout.FillNoStroke => $" fill=\"{Hex(fill.Rgba)}\" fill-opacity=\"{F(fill.Opacity)}\" stroke=\"none\"",
            _ => $" stroke=\"{Hex(stroke.Rgba)}\" stroke-width=\"1\"" + DashAttr(stroke, DashSampler.PathLength(s))
                 + (stroke.NonScaling ? NonScaling : "") + FillAttr(fill, stroke.Rgba),
        };
        _sb.Append($"{Indent}<path d=\"{d}\"{attrs}/>\n");
    }

    private static string BulgeCommand(PathSeg seg)
    {
        double x1 = seg.P1.X, y1 = seg.P1.Y, x2 = seg.P2.X, y2 = seg.P2.Y, bulge = seg.Bulge;
        double chord = Math.Sqrt((x2 - x1) * (x2 - x1) + (y2 - y1) * (y2 - y1));
        if (chord < 1e-9) return "";
        double radius = chord * (1 + bulge * bulge) / (4 * Math.Abs(bulge));
        double theta = 4 * Math.Atan(bulge);
        bool large = Math.Abs(theta) > Math.PI;
        bool sweep = bulge > 0;
        return $"A {F(radius)} {F(radius)} 0 {(large ? 1 : 0)} {(sweep ? 1 : 0)} {F(x2)} {F(y2)}";
    }

    public void DrawText(TextRequest t)
    {
        var s = t.Style;
        double h = s.Height > 0 ? s.Height : 2.5;
        Expand(t.Position.X, t.Position.Y);

        if (t.Raster is { } spec && TryShxPaths(t, spec, h, out string shx))
        {
            _sb.Append(shx);
            return;
        }

        string rot = Math.Abs(s.Rotation) > 1e-9
            ? $" rotate({F(-s.Rotation * 180 / Math.PI)})" : "";
        string anchor = s.Align switch
        {
            TextAlignment.BaseCenter or TextAlignment.MiddleCenter or TextAlignment.TopLeft
                => " text-anchor=\"middle\"",
            TextAlignment.BaseRight or TextAlignment.MiddleRight => " text-anchor=\"end\"",
            _ => "",
        };
        var sb = new StringBuilder();
        sb.Append($"{Indent}<g transform=\"translate({F(t.Position.X)},{F(t.Position.Y)}) "
            + $"scale(1,-1){rot}\">");
        var markups = LineMarkups(t, h);
        double pitch = ShxTextShapes.LinePitch(h, t.Raster?.LineGap ?? 1.0);
        double fontSize = h * ShxTextShapes.EmFactor;
        for (int i = 0; i < markups.Length; i++)
            sb.Append($"<text x=\"0\" y=\"{F(i * pitch)}\" font-size=\"{F(fontSize)}\" "
                + $"font-family=\"{s.FontFamily}\"{anchor} fill=\"{Hex(s.Rgba)}\">"
                + $"{markups[i]}</text>");
        sb.Append("</g>\n");
        _sb.Append(sb);
    }

    private static string[] LineMarkups(TextRequest t, double h)
    {
        var raw = t.Content;
        bool hasStack = raw.Contains("\\S", StringComparison.Ordinal);
        List<MTextParser.Line>? parsed = null;
        if (hasStack)
        {
            var p = MTextParser.Parse(raw);
            if (p.Any(l => l.Runs.Any(r => r.IsStack))) parsed = p;
        }
        if (parsed is null)
            return RenderContext.SanitizeText(hasStack ? MTextParser.InlineStacks(raw) : raw)
                .Split('\n').Select(RenderContext.Escape).ToArray();

        var markups = new List<string>(parsed.Count);
        foreach (var line in parsed)
        {
            var m = new StringBuilder();
            double tailDx = 0;
            foreach (var run in line.Runs)
            {
                double sc = StackScale(run, h);
                bool stackable = run is { IsStack: true, StackKind: '^' }
                                 && run.StackTop.Length > 0 && run.StackBottom.Length > 0;
                if (!stackable)
                {
                    string text = run.IsStack ? run.StackTop + "/" + run.StackBottom : run.Text;
                    if (text.Length > 0)
                    {
                        AppendPlain(m, tailDx, text);
                        tailDx = 0;
                    }
                }
                else
                {
                    double wTop = EstAdvanceEm(run.StackTop), wBot = EstAdvanceEm(run.StackBottom);
                    double stackFs = h * sc * ShxTextShapes.EmFactor;
                    m.Append($"<tspan font-size=\"{F(stackFs)}\" dy=\"-1em\">"
                        + $"{RenderContext.Escape(run.StackTop)}</tspan>")
                     .Append($"<tspan font-size=\"{F(stackFs)}\" dy=\"1em\" dx=\"-{F(wTop)}em\">"
                        + $"{RenderContext.Escape(run.StackBottom)}</tspan>");
                    tailDx = Math.Max(0, (wTop - wBot) * sc);
                }
            }
            markups.Add(m.ToString());
        }
        return markups.ToArray();
    }

    private static double StackScale(MTextParser.Run run, double h)
    {
        double sc = run.AbsoluteHeight is { } abs && h > 0 ? abs / h : run.HeightScale;
        return sc > 0 ? sc : 1;
    }

    private static void AppendPlain(StringBuilder m, double dxEm, string text)
    {
        if (text.Length == 0) return;
        string esc = RenderContext.Escape(text);
        m.Append(dxEm > 1e-6
            ? $"<tspan dx=\"{F(dxEm)}em\">{esc}</tspan>"
            : esc);
    }

    private static bool IsCaretStack(MTextParser.Run r) =>
        r is { IsStack: true, StackKind: '^' } && r.StackTop.Length > 0 && r.StackBottom.Length > 0;

    private void AppendShxStrokes(StringBuilder sb, ShxTextShapes.Seg seg, double cursor, double baselineLy,
                                  double cos, double sin, string strokeHex)
    {
        foreach (var pts in ShxTextShapes.Strokes(seg))
        {
            var d = new StringBuilder();
            for (int k = 0; k < pts.Count / 2; k++)
            {
                double lx = cursor + pts[2 * k];
                double ly = baselineLy + pts[2 * k + 1];
                double x = lx * cos - ly * sin;
                double y = -(lx * sin + ly * cos);
                d.Append(k == 0 ? "M" : "L").Append(F(x)).Append(' ').Append(F(y)).Append(' ');
            }
            if (d.Length == 0) continue;
            sb.Append($"<path d=\"{d.ToString().TrimEnd()}\" fill=\"none\" "
                + $"stroke=\"{strokeHex}\" stroke-width=\"1\"{NonScaling}/>");
        }
    }

    private string ShxStackSvg(TextRequest t, RasterTextSpec spec, double h,
                               ShxFont? main, ShxFont? big, IReadOnlyList<MTextParser.Line> rows)
    {
        var st = spec.Style!;
        double wf = spec.RawWidthFactor > 0 && Math.Abs(spec.RawWidthFactor - 1.0) > 1e-9
            ? spec.RawWidthFactor
            : spec.StyleWidthFactor ? (st.WidthFactor > 0 ? st.WidthFactor : 1.0) : 1.0;
        double emPx = h * ShxTextShapes.EmFactor;
        double pitch = ShxTextShapes.LinePitch(h, spec.LineGap);
        double totalH = ShxTextShapes.BlockHeight(pitch, emPx, rows.Count);
        double topOffset = spec.VAlign switch { 1 => -totalH, 2 => -totalH / 2, _ => 0 };
        double cos = Math.Cos(t.Style.Rotation), sin = Math.Sin(t.Style.Rotation);
        string hex = Hex(t.Style.Rgba);

        var plan = new List<(ShxTextShapes.Seg Seg, double Raise, double Adv)>[rows.Count];
        var lineWidths = new double[rows.Count];
        for (int i = 0; i < rows.Count; i++)
        {
            var segs = new List<(ShxTextShapes.Seg, double, double)>();
            double width = 0;
            foreach (var r in rows[i].Runs)
            {
                double sc = StackScale(r, h);
                if (IsCaretStack(r))
                {
                    if (!ShxTextShapes.Covers(main, big, r.StackTop) || !ShxTextShapes.Covers(main, big, r.StackBottom))
                        return "";
                    var top = new ShxTextShapes.Seg(r.StackTop, main, big, h * sc, wf);
                    var bot = new ShxTextShapes.Seg(r.StackBottom, main, big, h * sc, wf);
                    double block = Math.Max(ShxTextShapes.Measure(top), ShxTextShapes.Measure(bot));
                    segs.Add((top, sc, 0));
                    segs.Add((bot, 0, block));
                    width += block;
                }
                else
                {
                    string text = r.IsStack ? r.StackTop + "/" + r.StackBottom : r.Text;
                    if (text.Length == 0) continue;
                    if (!ShxTextShapes.Covers(main, big, text)) return "";
                    var seg = new ShxTextShapes.Seg(text, main, big, h * sc, wf);
                    segs.Add((seg, 0, ShxTextShapes.Measure(seg)));
                    width += ShxTextShapes.Measure(seg);
                }
            }
            plan[i] = segs;
            lineWidths[i] = width;
        }

        var sb = new StringBuilder();
        sb.Append($"{Indent}<g transform=\"translate({F(t.Position.X)},{F(t.Position.Y)}) scale(1,-1)\">");
        for (int i = 0; i < plan.Length; i++)
        {
            double cursor = spec.HAlign switch { 1 or 4 => -lineWidths[i] / 2, 2 => -lineWidths[i], _ => 0 };
            double baselineLy = spec.VAlign == 0
                ? -(i * pitch)
                : -(topOffset + i * pitch + emPx);
            foreach (var (seg, raise, adv) in plan[i])
            {
                AppendShxStrokes(sb, seg, cursor, baselineLy + raise * h, cos, sin, hex);
                cursor += adv;
            }
        }
        sb.Append("</g>\n");
        return sb.ToString();
    }

    private static double EstAdvanceEm(string s)
    {
        double w = 0;
        foreach (char c in s)
            w += c switch
            {
                >= '\u2E80' => 1.0,
                '.' or ',' or ':' or ';' or '\'' or ' ' => 0.278,
                '-' or '(' or ')' => 0.333,
                '/' => 0.26,
                '%' => 0.889,
                >= '0' and <= '9' => 0.556,
                _ => 0.584,
            };
        return w;
    }

    private static ShxFont? ShxOf(string fontFile)
        => !string.IsNullOrWhiteSpace(fontFile)
           && fontFile.EndsWith(".shx", StringComparison.OrdinalIgnoreCase)
            ? ShxBuiltinFonts.Load(fontFile)
            : null;

    private bool TryShxPaths(TextRequest t, RasterTextSpec spec, double h, out string svg)
    {
        svg = "";
        var st = spec.Style;
        if (st is null) return false;

        ShxFont? main = ShxOf(st.FontFile), big = ShxOf(st.BigfontFile);
        if (main is null && big is null) return false;

        if (MTextParser.HasNonShxRunFont(t.Content)) return false;

        if (t.Content.Contains("\\S", StringComparison.Ordinal))
        {
            var rows = MTextParser.Parse(t.Content);
            if (rows.Any(l => l.Runs.Any(IsCaretStack)))
            {
                string stacked = ShxStackSvg(t, spec, h, main, big, rows);
                if (stacked.Length > 0)
                {
                    svg = stacked;
                    return true;
                }
            }
        }

        var body = t.Content.Contains("\\S", StringComparison.Ordinal)
            ? MTextParser.InlineStacks(t.Content) : t.Content;
        var lines = RenderContext.SanitizeText(body).Split('\n');
        foreach (var line in lines)
            if (line.Trim().Length > 0 && !ShxTextShapes.Covers(main, big, line)) return false;

        double wf = spec.RawWidthFactor > 0 && Math.Abs(spec.RawWidthFactor - 1.0) > 1e-9
            ? spec.RawWidthFactor
            : spec.StyleWidthFactor ? (st.WidthFactor > 0 ? st.WidthFactor : 1.0) : 1.0;

        double emPx = h * ShxTextShapes.EmFactor;
        double pitch = ShxTextShapes.LinePitch(h, spec.LineGap);
        double totalH = ShxTextShapes.BlockHeight(pitch, emPx, lines.Length);
        double topOffset = spec.VAlign switch { 1 => -totalH, 2 => -totalH / 2, _ => 0 };
        double cos = Math.Cos(t.Style.Rotation), sin = Math.Sin(t.Style.Rotation);

        var sb = new StringBuilder();
        sb.Append($"{Indent}<g transform=\"translate({F(t.Position.X)},{F(t.Position.Y)}) scale(1,-1)\">");
        string strokeHex = Hex(t.Style.Rgba);
        for (int i = 0; i < lines.Length; i++)
        {
            var seg = new ShxTextShapes.Seg(lines[i], main, big, h, wf);
            double lineWidth = ShxTextShapes.Measure(seg);
            double cursor = spec.HAlign switch { 1 or 4 => -lineWidth / 2, 2 => -lineWidth, _ => 0 };
            double baselineLy = spec.VAlign == 0
                ? -(i * pitch)
                : -(topOffset + i * pitch + emPx);
            AppendShxStrokes(sb, seg, cursor, baselineLy, cos, sin, strokeHex);
        }
        sb.Append("</g>\n");
        svg = sb.ToString();
        return true;
    }
}
