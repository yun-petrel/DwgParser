using DwgParser.Core.Objects;

namespace DwgParser.Core.Render;

internal sealed class MLineRenderer : IEntityRenderer
{
    public static readonly MLineRenderer Instance = new();

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

internal sealed class ToleranceRenderer : IEntityRenderer
{
    public static readonly ToleranceRenderer Instance = new();

    public void Render(DwgEntity e, RenderContext ctx, Matrix m, IDrawDevice device)
    {
        if (string.IsNullOrEmpty(e.TextValue)) return;

        var anchor = LineFamily.World(e.Start, m);
        ctx.Expand(anchor.X, anchor.Y);

        double rot = Math.Abs(e.Dir.X) > 1e-12 || Math.Abs(e.Dir.Y) > 1e-12
            ? Math.Atan2(e.Dir.Y, e.Dir.X) : 0.0;
        string content = StripFcfSeparators(e.TextValue);

        device.DrawText(new TextRequest(content, anchor,
            new TextStyle(ctx.ResolveFontFamily(e.TextStyleHandle, content),
                          e.Height, rot, TextAlignment.BaseLeft,
                          Rgba.FromHex(ctx.EntityColor(e)))));
    }

    internal static string StripFcfSeparators(string s)
    {
        if (s.IndexOf("%%v", StringComparison.Ordinal) < 0
            && s.IndexOf("%%V", StringComparison.Ordinal) < 0) return s;
        var sb = new System.Text.StringBuilder(s.Length);
        for (int i = 0; i < s.Length; i++)
        {
            if (s[i] == '%' && i + 2 < s.Length && s[i + 1] == '%' && (s[i + 2] == 'v' || s[i + 2] == 'V'))
            {
                sb.Append(' ');
                i += 2;
                continue;
            }
            sb.Append(s[i]);
        }
        return sb.ToString();
    }
}

internal sealed class AcisWireframeRenderer : IEntityRenderer
{
    public static readonly AcisWireframeRenderer Instance = new();

    public void Render(DwgEntity e, RenderContext ctx, Matrix m, IDrawDevice device)
    {
        if (e.AcisVertices is not { Length: >= 2 } vs) return;

        var pts = new Vec2[vs.Length];
        for (int i = 0; i < vs.Length; i++)
            pts[i] = LineFamily.World(new Vec2(vs[i].X, vs[i].Y), m);

        device.DrawPolyline(pts, false, LineFamily.Pen(e, ctx, dashed: false), FillStyle.NoFill);
    }
}
