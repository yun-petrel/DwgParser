using DwgParser.Core.Objects;

namespace DwgParser.Core.Render;

internal sealed class LeaderRenderer : IEntityRenderer
{
    public static readonly LeaderRenderer Instance = new();

    public void Render(DwgEntity e, RenderContext ctx, Matrix m, IDrawDevice device)
    {
        var pts = e.Points;
        if (pts.Count == 0) return;

        if (pts.Count >= 2)
        {
            var segs = new List<PathSeg>(pts.Count) { PathSeg.Move(LineFamily.World(pts[0], m)) };
            for (int i = 1; i < pts.Count; i++)
                segs.Add(PathSeg.Line(LineFamily.World(pts[i - 1], m), LineFamily.World(pts[i], m)));
            device.DrawPath(segs.ToArray(), false, LineFamily.Pen(e, ctx, dashed: false),
                            FillStyle.NoFill, PathLayout.StrokeFirst);
        }

        if (!e.ArrowheadOn || pts.Count < 2) return;

        double len = ArrowLength(e, ctx);
        if (!double.IsFinite(len) || len <= 0) return;
        double w = len * DimensionRenderer.ArrowWidthRatio;

        var p0 = pts[0];
        var p1 = pts[1];
        double vx = p1.X - p0.X, vy = p1.Y - p0.Y;
        double n = Math.Sqrt(vx * vx + vy * vy);
        if (n < 1e-12) return;
        vx /= n; vy /= n;

        var tip = LineFamily.World(p0, m);
        var baseMid = new Vec2(p0.X - vx * len, p0.Y - vy * len);
        double px = -vy * w / 2, py = vx * w / 2;
        var ring = new[]
        {
            tip,
            LineFamily.World(new Vec2(baseMid.X + px, baseMid.Y + py), m),
            LineFamily.World(new Vec2(baseMid.X - px, baseMid.Y - py), m),
        };
        device.FillPolygon(ring, new FillStyle(Rgba.FromHex(ctx.EntityColor(e)), 1, Stroked: false));
    }

    internal static double ArrowLength(DwgEntity e, RenderContext ctx)
    {
        var s = ctx.ResolveDimStyle(e.DimStyleHandle);
        if (s is { Decoded: true })
        {
            double sc = s.Dimscale > 0 ? s.Dimscale : 1.0;
            double a = s.Dimasz * sc;
            if (double.IsFinite(a) && a > 0) return a;
        }
        return DimensionRenderer.ArrowLen;
    }
}
