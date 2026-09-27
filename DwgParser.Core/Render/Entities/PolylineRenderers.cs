using DwgParser.Core.Objects;

namespace DwgParser.Core.Render;

internal sealed class PolylineRenderer : IEntityRenderer
{
    public static readonly PolylineRenderer Instance = new();

    public void Render(DwgEntity e, RenderContext ctx, Matrix m, IDrawDevice device)
    {
        var pts = e.Points;
        if (pts.Count == 0) return;

        var segs = new List<PathSeg>(pts.Count + 1) { PathSeg.Move(Point(pts[0], m)) };
        for (int i = 1; i < pts.Count; i++)
            segs.Add(Segment(Point(pts[i - 1], m), Point(pts[i], m), BulgeAt(e.Bulges, i - 1)));

        if (e.Closed && pts.Count > 1)
        {
            var (first, last) = (Point(pts[0], m), Point(pts[^1], m));
            segs.Add(Segment(last, first, BulgeAt(e.Bulges, pts.Count - 1), back: true));
        }

        device.DrawPath(segs.ToArray(), false, LineFamily.Pen(e, ctx, dashed: false),
                        FillStyle.NoFill, PathLayout.StrokeFirst);
    }

    private static PathSeg Segment(Vec2 from, Vec2 to, double bulge, bool back = false)
    {
        if (Math.Abs(bulge) < 1e-6)
            return back ? PathSeg.LineUnbounded(from, to) : PathSeg.Line(from, to);
        return back ? PathSeg.ArcUnbounded(from, to, bulge) : PathSeg.Arc(from, to, bulge);
    }

    internal static double BulgeAt(List<double> bulges, int i) => i < bulges.Count ? bulges[i] : 0.0;

    internal static Vec2 Point(Vec2 p, Matrix m) => LineFamily.World(p, m);
}

internal sealed class PointSequenceRenderer : IEntityRenderer
{
    public static readonly PointSequenceRenderer Instance = new();

    public void Render(DwgEntity e, RenderContext ctx, Matrix m, IDrawDevice device)
    {
        var pts = e.Points;
        if (pts.Count < 2) return;

        var segs = new List<PathSeg>(pts.Count) { PathSeg.Move(PolylineRenderer.Point(pts[0], m)) };
        for (int i = 1; i < pts.Count; i++)
            segs.Add(PathSeg.Line(PolylineRenderer.Point(pts[i - 1], m), PolylineRenderer.Point(pts[i], m)));

        device.DrawPath(segs.ToArray(), false, LineFamily.Pen(e, ctx, dashed: false),
                        FillStyle.NoFill, PathLayout.StrokeFirst);
    }
}
