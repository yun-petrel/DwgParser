using DwgParser.Core.Objects;

namespace DwgParser.Core.Render;

internal sealed class ViewportRenderer : IEntityRenderer
{
    public static readonly ViewportRenderer Instance = new();

    public void Render(DwgEntity e, RenderContext ctx, Matrix m, IDrawDevice device)
    {
        if (e.VportWidth <= 0 || e.Height <= 0) return;
        double hw = e.VportWidth / 2, hh = e.Height / 2;
        var a = LineFamily.World(new Vec2(e.Center.X - hw, e.Center.Y - hh), m);
        var b = LineFamily.World(new Vec2(e.Center.X + hw, e.Center.Y + hh), m);
        device.DrawRect(a, b, LineFamily.Pen(e, ctx, dashed: false));
    }
}
