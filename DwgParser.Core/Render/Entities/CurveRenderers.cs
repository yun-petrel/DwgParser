using DwgParser.Core.Objects;

namespace DwgParser.Core.Render;

internal sealed class CircleRenderer : IEntityRenderer
{
    public static readonly CircleRenderer Instance = new();

    public void Render(DwgEntity e, RenderContext ctx, Matrix m, IDrawDevice device)
    {
        device.DrawCircle(LineFamily.World(e.Center, m), e.Radius * CurveFamily.AxisScale(m),
                          LineFamily.PenResolved(e, ctx), FillStyle.NoFill);
    }
}

internal sealed class ArcRenderer : IEntityRenderer
{
    public static readonly ArcRenderer Instance = new();

    public void Render(DwgEntity e, RenderContext ctx, Matrix m, IDrawDevice device)
    {
        double a0 = e.StartAngle, a1 = e.EndAngle;
        if (e.ExtrusionZ < 0) { a0 = -a0; a1 = -a1; }
        if (a1 < a0) a1 += Math.PI * 2;
        device.DrawArc(LineFamily.World(e.Center, m), e.Radius * CurveFamily.AxisScale(m), a0, a1,
                       LineFamily.PenResolved(e, ctx));
    }
}
