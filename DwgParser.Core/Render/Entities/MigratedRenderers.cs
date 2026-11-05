using DwgParser.Core.Objects;

namespace DwgParser.Core.Render;

internal sealed class PointRenderer : IEntityRenderer
{
    public static readonly PointRenderer Instance = new();

    public void Render(DwgEntity e, RenderContext ctx, Matrix m, IDrawDevice device)
        => device.DrawDot(LineFamily.World(e.Start, m), 3,
            new FillStyle(Rgba.FromHex(ctx.EntityColor(e)), 1.0, false));
}

internal sealed class EllipseRenderer : IEntityRenderer
{
    public static readonly EllipseRenderer Instance = new();

    public void Render(DwgEntity e, RenderContext ctx, Matrix m, IDrawDevice device)
    {
        var stroke = StrokeStyle.Solid(Rgba.FromHex(ctx.EntityColor(e)));
        double a = Math.Max(0.001, e.Radius);
        double b = Math.Max(0.001, a * e.WidthFactor);
        double theta = Math.Atan2(e.Start.Y, e.Start.X);
        double start = e.StartAngle, end = e.EndAngle;
        if (end < start) end += Math.PI * 2;
        if (end - start < 1e-6) { start = 0; end = Math.PI * 2; }
        bool mirror = e.ExtrusionZ < 0;
        int n = Math.Max(16, (int)((end - start) / (Math.PI / 48)));
        double cosT = Math.Cos(theta), sinT = Math.Sin(theta);
        Vec2 prev = default;
        for (int i = 0; i <= n; i++)
        {
            double t = start + (end - start) * i / n;
            double ct = Math.Cos(t), st = Math.Sin(t);
            double x = e.Center.X + a * ct * cosT - b * st * sinT;
            double y = e.Center.Y + a * ct * sinT + b * st * cosT;
            if (mirror) y = 2 * e.Center.Y - y;
            var cur = LineFamily.World(new Vec2(x, y), m);
            if (i > 0) device.DrawLine(prev, cur, stroke);
            prev = cur;
        }
    }
}

internal sealed class HatchRenderer : IEntityRenderer
{
    public static readonly HatchRenderer Instance = new();

    public void Render(DwgEntity e, RenderContext ctx, Matrix m, IDrawDevice device)
    {
        var rgba = Rgba.FromHex(ctx.EntityColor(e));
        var stroke = StrokeStyle.Solid(rgba);
        var fill = new FillStyle(rgba, 1.0, false);
        if (e is not HatchEntity he || HatchGeometry.BoundaryRings(he) is not { Count: > 0 } rings)
        {
            if (e.SolidFill && e.Points.Count >= 3)
                device.FillPolygon(Ring(e.Points, m), fill);
            var pts = e.Points;
            for (int i = 1; i < pts.Count; i++)
                device.DrawLine(LineFamily.World(pts[i - 1], m), LineFamily.World(pts[i], m), stroke);
            return;
        }

        foreach (var ring in rings)
        {
            if (e.SolidFill && ring.Length >= 3)
                device.FillPolygon(Ring(ring, m), fill);
            for (int i = 0; i < ring.Length; i++)
            {
                var a = ring[i];
                var b = ring[(i + 1) % ring.Length];
                device.DrawLine(LineFamily.World(a, m), LineFamily.World(b, m), stroke);
            }
        }
        foreach (var (a, b) in HatchGeometry.PatternSegments(he))
            device.DrawLine(LineFamily.World(a, m), LineFamily.World(b, m), stroke);
    }

    private static Vec2[] Ring(IReadOnlyList<Vec2> src, Matrix m)
    {
        var dst = new Vec2[src.Count];
        for (int i = 0; i < src.Count; i++) dst[i] = LineFamily.World(src[i], m);
        return dst;
    }
}

internal sealed class TableRenderer : IEntityRenderer
{
    public static readonly TableRenderer Instance = new();

    internal static Action<string, DwgObjectReader.TextStyleInfo?>? ResolvedCellStyle;

    public void Render(DwgEntity e, RenderContext ctx, Matrix m, IDrawDevice device)
    {
        if (e is not TableEntity t) return;
        var lo = TableLayout.Compute(t);
        if (lo is null) return;

        var rgba = Rgba.FromHex(ctx.EntityColor(e));
        var stroke = StrokeStyle.Solid(rgba);
        var fill = new FillStyle(rgba, 1.0, false);

        double rot = t.Rotation;
        double cos = Math.Cos(rot), sin = Math.Sin(rot);
        Vec2 W(double lx, double ly)
        {
            double wx = t.Start.X + lx * cos * t.ScaleX - ly * sin * t.ScaleY;
            double wy = t.Start.Y + lx * sin * t.ScaleX + ly * cos * t.ScaleY;
            return LineFamily.World(new Vec2(wx, wy), m);
        }

        foreach (var cp in lo.Cells)
        {
            if (cp.Background is not { } bg) continue;
            if (!AciPalette.TryCmcToRgb(bg.Rgb, bg.Index, out byte br, out byte bgr, out byte bb)) continue;
            Vec2[] cellRing =
            [
                W(cp.Rect.Left, cp.Rect.Bottom),
                W(cp.Rect.Right, cp.Rect.Bottom),
                W(cp.Rect.Right, cp.Rect.Top),
                W(cp.Rect.Left, cp.Rect.Top),
            ];
            device.FillPolygon(cellRing, new FillStyle(Pack(br, bgr, bb), 1.0, false));
        }

        for (int col = 0; col <= lo.Columns; col++)
        {
            double lx = lo.ColBoundaries[col];
            for (int r = 0; r < lo.Rows; r++)
            {
                if (col > 0 && col < lo.Columns && lo.SameMerge(r, col - 1, r, col)) continue;
                device.DrawLine(W(lx, lo.RowBoundaries[r]), W(lx, lo.RowBoundaries[r + 1]), stroke);
            }
        }
        for (int r = 0; r <= lo.Rows; r++)
        {
            double ly = lo.RowBoundaries[r];
            for (int col = 0; col < lo.Columns; col++)
            {
                if (r > 0 && r < lo.Rows && lo.SameMerge(r - 1, col, r, col)) continue;
                device.DrawLine(W(lo.ColBoundaries[col], ly), W(lo.ColBoundaries[col + 1], ly), stroke);
            }
        }

        var style = ctx.StyleFonts.TryGetValue(t.TextStyleHandle, out var st) ? st : FallbackStyle(ctx);
        foreach (var cp in lo.Cells)
        {
            if (!cp.HasText) continue;
            string text = RenderContext.SanitizeText(cp.Text);
            if (text.Trim().Length == 0) continue;

            uint ink = rgba;
            if (cp.ContentColor is { } cc && AciPalette.TryCmcToRgb(cc.Rgb, cc.Index, out byte cr2, out byte cg2, out byte cb2))
                ink = Pack(cr2, cg2, cb2);

            int a = cp.Alignment is >= 1 and <= 9 ? cp.Alignment : 5;
            int hAlign = ((a - 1) % 3) switch { 0 => 0, 1 => 1, _ => 2 };
            int vAlign = ((a - 1) / 3) switch { 0 => 3, 1 => 2, _ => 1 };
            double margin = Math.Min(cp.Rect.W, cp.Rect.H) * 0.15;
            double anchX = hAlign switch { 0 => cp.Rect.Left + margin, 1 => cp.Rect.CenterX, _ => cp.Rect.Right - margin };
            double anchY = vAlign switch { 3 => cp.Rect.Top, 2 => cp.Rect.CenterY, _ => cp.Rect.Bottom };

            var (px, py) = W(anchX, anchY);
            double familyRot = t.Rotation + cp.Rotation;
            var cellStyle = cp.StyleHandle != 0 && ctx.StyleFonts.TryGetValue(cp.StyleHandle, out var s2)
                ? s2 : style;
            ResolvedCellStyle?.Invoke(cp.Text, cellStyle);
            device.DrawText(new TextRequest(cp.Text, new Vec2(px, py),
                new TextStyle("", cp.TextHeight, familyRot, TextAlignment.BaseLeft, ink),
                new RasterTextSpec(HAlign: hAlign, VAlign: vAlign, HorizMode: 0, VertMode: 0,
                    AlignmentPoint: default, RawWidthFactor: 0, LineGap: 1.0, IsMText: true,
                    Style: cellStyle, StyleWidthFactor: false, SkipVerticalShx: true)));
        }
    }

    private static DwgObjectReader.TextStyleInfo? FallbackStyle(RenderContext ctx)
    {
        DwgObjectReader.TextStyleInfo? best = null;
        ulong bestHandle = ulong.MaxValue;
        foreach (var (handle, st) in ctx.StyleFonts)
        {
            string main = st.FontFile ?? "";
            if (main.Length == 0) continue;
            if (main.EndsWith(".shx", StringComparison.OrdinalIgnoreCase)) continue;
            if (handle < bestHandle) { bestHandle = handle; best = st; }
        }
        return best;
    }

    internal static uint Pack(byte r, byte g, byte b) => (uint)(r << 24) | (uint)(g << 16) | (uint)(b << 8) | 0xFFu;
}

internal sealed class MLeaderRenderer : IEntityRenderer
{
    public static readonly MLeaderRenderer Instance = new();

    public void Render(DwgEntity e, RenderContext ctx, Matrix m, IDrawDevice device)
    {
        if (e is not MLeaderEntity ml || !ml.Captured) return;
        var rgba = Rgba.FromHex(ctx.EntityColor(e));
        var stroke = StrokeStyle.Solid(rgba);
        bool anyLine = false;
        double? styleArrow = ctx.ResolveMLeaderStyle(ml.MLeaderStyleHandle)?.ArrowHeadSize;

        foreach (var node in ml.Leaders)
        {
            foreach (var line in node.Lines)
            {
                if (line.Points3D.Count == 0) continue;
                double arrow = ml.ResolveArrowSize(line, styleArrow);
                for (int i = 1; i < line.Points3D.Count; i++)
                    device.DrawLine(P(line.Points3D[i - 1], m), P(line.Points3D[i], m), stroke);
                if (line.Points3D.Count >= 2)
                    DrawArrow(device, m, rgba, line.Points3D[0], line.Points3D[1], arrow);
                else
                {
                    var p0 = line.Points3D[0];
                    double nx = p0.X - node.DoglegVector.X, ny = p0.Y - node.DoglegVector.Y;
                    if (node.HasLastLeaderLinePoint)
                    {
                        var lp = node.LastLeaderLinePoint;
                        nx = lp.X; ny = lp.Y;
                        device.DrawLine(P(p0, m), P(lp, m), stroke);
                    }
                    DrawArrow(device, m, rgba, p0, (nx, ny, p0.Z), arrow);
                }
                anyLine = true;
            }
            if (node.HasLastLeaderLinePoint && node.HasDogleg)
            {
                double len = Finite(node.DoglegLength) && node.DoglegLength > 0 ? node.DoglegLength : ml.DoglegLength;
                if (Finite(len) && len > 1e-9)
                {
                    double dx = node.DoglegVector.X, dy = node.DoglegVector.Y;
                    double n = Math.Sqrt(dx * dx + dy * dy);
                    if (n < 1e-9) { dx = 1; dy = 0; } else { dx /= n; dy /= n; }
                    var from = node.LastLeaderLinePoint;
                    device.DrawLine(P(from, m),
                        P((from.X + dx * len, from.Y + dy * len, from.Z), m), stroke);
                }
            }
        }
        if (!anyLine && e.Points.Count >= 2)
        {
            var pts = new Vec2[e.Points.Count];
            for (int i = 0; i < e.Points.Count; i++) pts[i] = LineFamily.World(e.Points[i], m);
            device.DrawPolyline(pts, false, stroke, FillStyle.NoFill);
        }

        string text = RenderContext.SanitizeText(ml.TextValue);
        if (string.IsNullOrWhiteSpace(text) || !Finite(ml.MTextHeight) || ml.MTextHeight <= 0) return;
        double wx = ml.ContentLocation.X, wy = ml.ContentLocation.Y;
        if (!Finite(wx) || !Finite(wy) || (wx == 0 && wy == 0)) { wx = ml.Base.X; wy = ml.Base.Y; }
        if (!Finite(wx) || !Finite(wy)) return;
        var style = ctx.StyleFonts.TryGetValue(ml.ContentStyleHandle, out var st) ? st : null;
        device.DrawText(new TextRequest(ml.TextValue, new Vec2(wx, wy),
            new TextStyle("", ml.MTextHeight, ml.ContentRotation, TextAlignment.BaseLeft, rgba),
            new RasterTextSpec(HAlign: 0, VAlign: 0, HorizMode: 0, VertMode: 0,
                AlignmentPoint: default, RawWidthFactor: 0, LineGap: 1.0, IsMText: false,
                Style: style, StyleWidthFactor: false, SkipVerticalShx: true)));
    }

    private static void DrawArrow(IDrawDevice device, Matrix m, uint rgba,
        (double X, double Y, double Z) tip, (double X, double Y, double Z) next, double size)
    {
        if (!Finite(size) || size <= 0) return;
        double vx = tip.X - next.X, vy = tip.Y - next.Y;
        double n = Math.Sqrt(vx * vx + vy * vy);
        if (n < 1e-9) return;
        double w = size * DimensionRenderer.ArrowWidthRatio / 2;
        double bx = tip.X - vx / n * size, by = tip.Y - vy / n * size;
        double px = -vy / n * w, py = vx / n * w;
        Vec2[] tri =
        [
            P(tip, m),
            P((bx + px, by + py, tip.Z), m),
            P((bx - px, by - py, tip.Z), m),
        ];
        device.FillPolygon(tri, new FillStyle(rgba, 1.0, false));
    }

    private static Vec2 P((double X, double Y, double Z) p, Matrix m)
        => LineFamily.World(new Vec2(p.X, p.Y), m);

    private static bool Finite(double v) => !double.IsNaN(v) && !double.IsInfinity(v);
}
