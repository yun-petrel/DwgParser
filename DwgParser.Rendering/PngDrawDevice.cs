using System.Text;
using DwgParser.Core.Objects;
using DwgParser.Core.Render;

namespace DwgParser.Rendering;

public sealed class PngDrawDevice : IDrawDevice
{
    private readonly PixelBuffer _buf;
    private readonly int _thick;
    private readonly ITextRendererBackend _text;
    private readonly DwgObjectReader.WalkResult? _walk;
    private Func<double, double> _tx = x => x, _ty = y => y;
    private double _scale = 1;

    private List<ShxFont>? _shxBypassFonts;
    private DwgObjectReader.WalkResult? _shxBypassWalk;

    public PngDrawDevice(PixelBuffer buf, DrawViewport view, int lineThickness,
                         ITextRendererBackend? textBackend = null,
                         DwgObjectReader.WalkResult? walk = null)
    {
        _buf = buf;
        _thick = Math.Max(1, lineThickness);
        _text = textBackend ?? TextBackends.Current;
        _walk = walk;
        BeginPass(view);
    }

    public void BeginPass(DrawViewport view)
    {
        var (tx, ty, scale) = FrameRenderer.MakeTransform(view.MinX, view.MinY, view.MaxX, view.MaxY,
                                                          view.WidthPx, view.HeightPx, (int)view.MarginPx);
        _tx = tx; _ty = ty; _scale = scale;
    }

    public void EndPass() { }

    internal int Px(double x) => FrameRenderer.ClampPx(_tx(x));
    internal int Py(double y) => FrameRenderer.ClampPx(_ty(y));
    internal double ScalePx => _scale;
    private int RadiusPx(double r) => Math.Max(1, FrameRenderer.ClampPx(r * _scale));

    public void DrawLine(Vec2 a, Vec2 b, StrokeStyle stroke)
    {
        var (x0, y0, x1, y1) = (Px(a.X), Py(a.Y), Px(b.X), Py(b.Y));
        if (stroke.Dashed) StrokeDashedWorld(new[] { a, b }, stroke);
        else _buf.DrawLine(x0, y0, x1, y1, stroke.R, stroke.G, stroke.B, _thick);
    }

    private readonly List<(Vec2 A, Vec2 B)> _dashBuf = new();

    private void StrokeDashedWorld(ReadOnlySpan<Vec2> pts, StrokeStyle s)
    {
        if (!s.HasDashPattern) return;
        var ph = DashSampler.Phase.Start(s.DashPattern!,
                DashSampler.CenteredPhase(s.DashPattern, DashSampler.PolylineLength(pts)));
        for (int i = 1; i < pts.Length; i++)
        {
            _dashBuf.Clear();
            DashSampler.Split(pts[i - 1], pts[i], s.DashPattern!, ref ph, _dashBuf);
            foreach (var (p, q) in _dashBuf)
                _buf.DrawLine(Px(p.X), Py(p.Y), Px(q.X), Py(q.Y), s.R, s.G, s.B, _thick);
        }
    }

    public void DrawRect(Vec2 cornerA, Vec2 cornerB, StrokeStyle stroke)
    {
        var (x0, y0) = (Px(cornerA.X), Py(cornerA.Y));
        var (x1, y1) = (Px(cornerB.X), Py(cornerB.Y));
        _buf.DrawLine(x0, y0, x1, y0, stroke.R, stroke.G, stroke.B, _thick);
        _buf.DrawLine(x1, y0, x1, y1, stroke.R, stroke.G, stroke.B, _thick);
        _buf.DrawLine(x1, y1, x0, y1, stroke.R, stroke.G, stroke.B, _thick);
        _buf.DrawLine(x0, y1, x0, y0, stroke.R, stroke.G, stroke.B, _thick);
    }

    public void DrawArc(Vec2 center, double radius, double a0, double a1, StrokeStyle stroke)
    {
        if (!stroke.Dashed)
        {
            _buf.DrawArc(Px(center.X), Py(center.Y), RadiusPx(radius), a0, a1,
                         stroke.R, stroke.G, stroke.B, _thick);
            return;
        }
        if (a1 < a0) a1 += Math.PI * 2;
        int steps = Math.Max(8, (int)Math.Ceiling((a1 - a0) / (Math.PI / 90)));
        var wpts = new Vec2[steps + 1];
        for (int i = 0; i <= steps; i++)
        {
            double ang = a0 + (a1 - a0) * i / steps;
            wpts[i] = new Vec2(center.X + radius * Math.Cos(ang), center.Y + radius * Math.Sin(ang));
        }
        StrokeDashedWorld(wpts, stroke);
    }

    public void DrawCircle(Vec2 center, double radius, StrokeStyle stroke, FillStyle fill)
    {
        var (cx, cy, r) = (Px(center.X), Py(center.Y), RadiusPx(radius));
        if (!fill.IsFill || fill.Stroked)
        {
            if (stroke.Dashed)
            {
                int steps = 360;
                var wpts = new Vec2[steps + 1];
                for (int i = 0; i <= steps; i++)
                {
                    double ang = 2 * Math.PI * i / steps;
                    wpts[i] = new Vec2(center.X + radius * Math.Cos(ang), center.Y + radius * Math.Sin(ang));
                }
                StrokeDashedWorld(wpts, stroke);
            }
            else _buf.DrawCircle(cx, cy, r, stroke.R, stroke.G, stroke.B, _thick);
        }
        else _buf.FillCircle(cx, cy, r, fill.R, fill.G, fill.B);
    }

    public void DrawPolyline(ReadOnlyMemory<Vec2> pts, bool closed, StrokeStyle stroke, FillStyle fill)
    {
        var p = pts.Span;
        if (p.Length == 0) return;
        if (fill.IsFill) _buf.FillPolygon(ToPxList(p), fill.R, fill.G, fill.B, fill.AlphaByte);
        for (int i = 1; i < p.Length; i++)
            _buf.DrawLine(Px(p[i - 1].X), Py(p[i - 1].Y), Px(p[i].X), Py(p[i].Y),
                          stroke.R, stroke.G, stroke.B, _thick);
        if (closed && p.Length > 2)
            _buf.DrawLine(Px(p[^1].X), Py(p[^1].Y), Px(p[0].X), Py(p[0].Y),
                          stroke.R, stroke.G, stroke.B, _thick);
    }

    public void DrawPath(ReadOnlyMemory<PathSeg> segs, bool closed, StrokeStyle stroke, FillStyle fill, PathLayout layout)
    {
        var s = segs.Span;
        if (s.Length == 0) return;

        var subpaths = new List<List<Vec2>>();
        List<Vec2>? cur = null;
        foreach (var seg in s)
        {
            if (seg.Kind == PathSegKind.Move || cur is null)
            {
                cur = new List<Vec2> { seg.P1 };
                subpaths.Add(cur);
                if (seg.Kind != PathSegKind.Move) AppendSeg(cur, seg.P1, seg);
                continue;
            }
            AppendSeg(cur, seg.P1, seg);
        }
        if (closed && cur is { Count: > 2 }) cur.Add(cur[0]);

        if (fill.IsFill)
            foreach (var sp in subpaths)
                if (sp.Count > 2)
                    _buf.FillPolygon(ToPxList(sp.ToArray()), fill.R, fill.G, fill.B, fill.AlphaByte);
        foreach (var sp in subpaths)
        {
            if (stroke.Dashed)
            {
                StrokeDashedWorld(sp.ToArray(), stroke);
                continue;
            }
            for (int i = 1; i < sp.Count; i++)
                _buf.DrawLine(Px(sp[i - 1].X), Py(sp[i - 1].Y), Px(sp[i].X), Py(sp[i].Y),
                              stroke.R, stroke.G, stroke.B, _thick);
        }
    }

    private static void AppendSeg(List<Vec2> into, Vec2 from, in PathSeg seg)
    {
        if (seg.Kind == PathSegKind.Arc && Math.Abs(seg.Bulge) > 1e-6)
        {
            var pts = BulgeSampler.Sample(from, seg.P2, seg.Bulge);
            for (int i = 1; i < pts.Count; i++) into.Add(pts[i]);
        }
        else into.Add(seg.P2);
    }

    public void FillPolygon(ReadOnlyMemory<Vec2> ring, FillStyle fill)
    {
        var p = ring.Span;
        if (p.Length == 0) return;
        if (fill.IsFill) _buf.FillPolygon(ToPxList(p), fill.R, fill.G, fill.B, fill.AlphaByte);
    }

    public void DrawText(TextRequest t)
    {
        if (t.Raster is null)
        {
            var s = t.Style;
            double h = s.Height > 0 ? s.Height : 2.5;
            var (ha, va) = AlignOf(s.Align);
            _text.DrawText(_buf, Px(t.Position.X), Py(t.Position.Y), t.Content,
                           (byte)(s.Rgba >> 24), (byte)(s.Rgba >> 16), (byte)(s.Rgba >> 8),
                           Math.Max(1, (int)Math.Round(h * _scale)), s.Rotation, s.FontFamily,
                           1.0, ha, va, 1.0, h * _scale);
            return;
        }
        PaintText(t, t.Raster);
    }

    private void PaintText(TextRequest t, RasterTextSpec sp)
    {
        byte cr = (byte)(t.Style.Rgba >> 24), cg = (byte)(t.Style.Rgba >> 16), cb = (byte)(t.Style.Rgba >> 8);
        var style = sp.Style;
        double rot = t.Style.Rotation;

        (cr, cg, cb) = RenderInk.Map(cr, cg, cb);
        string text = RenderContext.SanitizeText(t.Content);
        if (string.IsNullOrWhiteSpace(text)) return;

        double wf = sp.RawWidthFactor > 0 && Math.Abs(sp.RawWidthFactor - 1.0) > 1e-9
            ? sp.RawWidthFactor
            : sp.StyleWidthFactor ? style?.WidthFactor ?? 1.0 : 1.0;

        int fontPx = Math.Max(sp.MinFontPx, (int)(t.Style.Height * _scale * sp.EmFactor));
        int ax = Px(t.Position.X), ay = Py(t.Position.Y);
        if (sp.BaselineNudge != 0)
            ay += (int)(sp.BaselineNudge * fontPx);
        int hAlign = sp.HAlign, vAlign = sp.VAlign;
        double lineGap = sp.LineGap;

        if (sp.IsMText)
        {
            var parsed = MTextParser.Parse(t.Content);
            if (MTextParser.HasFormatting(parsed))
            {
                if (TryDrawShxRuns(parsed, style, ax, ay, t.Style.Height * _scale, rot,
                        wf, hAlign, vAlign, lineGap, _scale, cr, cg, cb))
                    return;
                string family = FontResolver.Resolve(style, text).Family;
                _text.DrawMTextRuns(_buf, ax, ay, parsed,
                    cr, cg, cb, fontPx, rot, family, wf, hAlign, vAlign, lineGap, _scale,
                    t.Style.Height * _scale);
                return;
            }
        }
        else
        {
            if (sp.HorizMode != 0)
                ax = Px(sp.AlignmentPoint.X);
            if (sp.HorizMode != 0 || sp.VertMode != 0)
                ay = Py(sp.AlignmentPoint.Y);
        }

        double shxHeightPx = t.Style.Height * _scale;
        if (style?.IsVertical == true)
        {
            var (vm, vb) = ResolveShxFonts(style, text);
            if (vm is not null || vb is not null)
            {
                ShxTextLayout.DrawVertical(_buf, vm, vb, text, ax, ay, shxHeightPx, rot,
                    cr, cg, cb, wf, lineGap);
                return;
            }
        }
        if (TryDrawShx(style, text, ax, ay, shxHeightPx, rot, wf, hAlign, vAlign,
                lineGap, cr, cg, cb))
            return;

        var font = FontResolver.Resolve(style, text);
        _text.DrawText(_buf, ax, ay,
            text, cr, cg, cb, fontPx, rot,
            sp.FamilyFromStyle ? font.Family : null, wf, hAlign, vAlign, lineGap,
            t.Style.Height * _scale);
    }


    internal bool TryDrawShx(DwgObjectReader.TextStyleInfo? style, string text,
        int ax, int ay, double heightPx, double rot, double wf, int hAlign, int vAlign,
        double lineGap, byte r, byte g, byte b)
    {
        var (main, big) = ResolveShxFonts(style, text);
        if (main is null && big is null) return false;
        ShxTextLayout.DrawPlain(_buf, main, big, text, ax, ay, heightPx, rot, r, g, b,
            wf, hAlign, vAlign, lineGap);
        return true;
    }

    internal static (ShxFont? main, ShxFont? big) ResolveShxFonts(
        DwgObjectReader.TextStyleInfo? style, string text, IReadOnlyList<ShxFont>? bypass)
    {
        ShxFont? main = null, big = null;
        if (style?.FontFile is { Length: > 0 } ff && ff.EndsWith(".shx", StringComparison.OrdinalIgnoreCase))
            main = ShxFontResolver.Resolve(ff);
        if (style?.BigfontFile is { Length: > 0 } bf && bf.EndsWith(".shx", StringComparison.OrdinalIgnoreCase))
            big = ShxFontResolver.Resolve(bf);
        if (main is null && big is null || !ShxTextLayout.Covers(main, big, text))
        {
            bool ok = false;
            if (bypass is not null)
                foreach (var pf in bypass)
                {
                    if (main is null && ShxTextLayout.Covers(pf, big, text)) { main = pf; ok = true; break; }
                    if (big is null && ShxTextLayout.Covers(main, pf, text)) { big = pf; ok = true; break; }
                }
            if (!ok) return (null, null);
        }
        return (main, big);
    }

    private List<ShxFont>? ProbeBypassFont(DwgObjectReader.TextStyleInfo? style)
    {
        if (_walk is null || StyleNamesUsableTtf(style)) return null;
        if (!ReferenceEquals(_shxBypassWalk, _walk))
        {
            _shxBypassWalk = _walk;
            _shxBypassFonts = null;
        }
        _shxBypassFonts ??= ShxBypassFontProbe.Of(_walk.SourceData);
        return _shxBypassFonts.Count == 0 ? null : _shxBypassFonts;
    }

    private (ShxFont? main, ShxFont? big) ResolveShxFonts(
        DwgObjectReader.TextStyleInfo? style, string text)
        => ResolveShxFonts(style, text, ProbeBypassFont(style));

    private static bool StyleNamesUsableTtf(DwgObjectReader.TextStyleInfo? style)
    {
        if (style is null) return false;
        string main = style.FontFile ?? "";
        string big = style.BigfontFile ?? "";
        if (main.Length == 0 && big.Length == 0) return false;
        static bool IsShx(string f) => f.EndsWith(".shx", StringComparison.OrdinalIgnoreCase);
        if (main.Length > 0 && IsShx(main)) return false;
        if (big.Length > 0 && IsShx(big)) return false;
        return true;
    }
    internal bool TryDrawShxRuns(IReadOnlyList<MTextParser.Line> lines,
        DwgObjectReader.TextStyleInfo? style, int ax, int ay, double capPx, double rot,
        double wf, int hAlign, int vAlign, double lineGap, double worldToPx,
        byte r, byte g, byte b)
    {
        var segLines = BuildShxRunSegs(lines, style, capPx, wf, worldToPx, TextRasterLimits.MinFontPx, r, g, b);
        if (segLines is null) return false;
        ShxTextLayout.DrawLines(_buf, segLines, ax, ay, capPx, rot, hAlign, vAlign, lineGap);
        return true;
    }

    internal static List<ShxTextLayout.Seg[]>? BuildShxRunSegs(
        IReadOnlyList<MTextParser.Line> lines, DwgObjectReader.TextStyleInfo? style,
        double capPx, double wf, double worldToPx, double minCap, byte r, byte g, byte b)
    {
        ShxFont? styleMain = null, styleBig = null;
        if (style?.FontFile is { Length: > 0 } ff && ff.EndsWith(".shx", StringComparison.OrdinalIgnoreCase))
            styleMain = ShxFontResolver.Resolve(ff);
        if (style?.BigfontFile is { Length: > 0 } bf && bf.EndsWith(".shx", StringComparison.OrdinalIgnoreCase))
            styleBig = ShxFontResolver.Resolve(bf);
        if (styleMain is null && styleBig is null) return null;

        var segLines = new List<ShxTextLayout.Seg[]>();
        var allText = new StringBuilder();
        foreach (var line in lines)
        {
            var segs = new List<ShxTextLayout.Seg>();
            foreach (var run in line.Runs)
            {
                if (run.IsStack) return null;
                if (run.Text.Length == 0) continue;
                if (MTextParser.IsNonShxRunFont(run.FontSpec)) return null;
                ShxFont? main = styleMain, big = styleBig;
                if (run.FontSpec is { Length: > 0 } fs && fs.EndsWith(".shx", StringComparison.OrdinalIgnoreCase))
                    main = ShxFontResolver.Resolve(fs) ?? styleMain;
                double cap = run.AbsoluteHeight.HasValue
                    ? run.AbsoluteHeight.Value * worldToPx
                    : capPx * run.HeightScale;
                var (rr, gg, bb) = run.AciColor.HasValue ? RunInk(run.AciColor.Value) : (r, g, b);
                segs.Add(new ShxTextLayout.Seg(run.Text, main, big,
                    Math.Max(minCap, cap), run.WidthFactor ?? wf, rr, gg, bb));
                allText.Append(run.Text);
            }
            if (segs.Count > 0) segLines.Add(segs.ToArray());
        }
        if (segLines.Count == 0 || !ShxTextLayout.Covers(styleMain, styleBig, allText.ToString()))
            return null;
        return segLines;
    }

    internal static (byte, byte, byte) RunInk(int aci)
    {
        var (rr, gg, bb) = FrameRenderer.ParseColor(RenderContext.AciColor(aci));
        return RenderInk.Map(rr, gg, bb);
    }

    private static (int H, int V) AlignOf(TextAlignment align) => align switch
    {
        TextAlignment.BaseLeft => (0, 0),
        TextAlignment.BaseCenter => (1, 0),
        TextAlignment.BaseRight => (2, 0),
        TextAlignment.TopLeft => (0, 3),
        TextAlignment.MiddleLeft => (0, 2),
        TextAlignment.MiddleCenter => (1, 2),
        TextAlignment.MiddleRight => (2, 2),
        _ => (0, 0),
    };

    private List<(int X, int Y)> ToPxList(ReadOnlySpan<Vec2> pts)
    {
        var list = new List<(int X, int Y)>(pts.Length);
        foreach (var v in pts) list.Add((Px(v.X), Py(v.Y)));
        return list;
    }
}
