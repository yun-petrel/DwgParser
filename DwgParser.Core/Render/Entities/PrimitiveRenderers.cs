using DwgParser.Core.Objects;

namespace DwgParser.Core.Render;

internal sealed class LineRenderer : IEntityRenderer
{
    public static readonly LineRenderer Instance = new();

    public void Render(DwgEntity e, RenderContext ctx, Matrix m, IDrawDevice device)
    {
        device.DrawLine(LineFamily.World(e.Start, m), LineFamily.World(e.End, m),
                        LineFamily.PenResolved(e, ctx));
    }
}

internal sealed class RayRenderer : IEntityRenderer
{
    public static readonly RayRenderer Instance = new();

    public void Render(DwgEntity e, RenderContext ctx, Matrix m, IDrawDevice device)
    {
        device.DrawLine(LineFamily.World(e.Start, m), LineFamily.World(e.End, m), LineFamily.Pen(e, ctx, dashed: false));
    }
}

internal sealed class XLineRenderer : IEntityRenderer
{
    public static readonly XLineRenderer Instance = new();

    public void Render(DwgEntity e, RenderContext ctx, Matrix m, IDrawDevice device)
    {
        device.DrawLine(LineFamily.World(e.Start, m), LineFamily.World(e.End, m), LineFamily.Pen(e, ctx, dashed: false));
    }
}

internal static class LineFamily
{
    internal static StrokeStyle Pen(DwgEntity e, RenderContext ctx, bool dashed)
        => new(Rgba.FromHex(ctx.EntityColor(e)), true, dashed);

    internal static StrokeStyle PenResolved(DwgEntity e, RenderContext ctx)
    {
        var pat = ctx.ResolveDashPattern(e);
        return new(Rgba.FromHex(ctx.EntityColor(e)), true, pat is not null, pat);
    }

    internal static bool DashedByName(string? ltypeName)
        => ltypeName is not null
           && (ltypeName.Contains("M9") || ltypeName.Contains("dashed") || ltypeName.Contains("-"));

    internal static Vec2 World(Vec2 p, Matrix m)
    {
        var (x, y) = m.Apply(p.X, p.Y);
        return new Vec2(x, y);
    }
}

internal static class CurveFamily
{
    internal static double AxisScale(Matrix m)
    {
        double sx = Math.Sqrt(m.M11 * m.M11 + m.M21 * m.M21);
        double sy = Math.Sqrt(m.M12 * m.M12 + m.M22 * m.M22);
        return Math.Max(0.001, (sx + sy) / 2);
    }
}
