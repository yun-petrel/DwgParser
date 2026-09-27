using DwgParser.Core.Objects;

namespace DwgParser.Core.Render;

internal sealed class SolidRenderer : IEntityRenderer
{
    public static readonly SolidRenderer Instance = new();

    internal static bool IsArrowheadForm(System.Collections.Generic.IReadOnlyList<Vec2> corners)
    {
        int n = Math.Min(4, corners.Count);
        if (n < 4) return false;
        for (int i = 0; i < n; i++)
            for (int j = i + 1; j < n; j++)
            {
                double dx = corners[i].X - corners[j].X, dy = corners[i].Y - corners[j].Y;
                if (dx * dx + dy * dy <= 1e-12) return true;
            }
        return false;
    }

    public void Render(DwgEntity e, RenderContext ctx, Matrix m, IDrawDevice device)
    {
        if (e.Corners.Count < 3) return;
        var rgba = Rgba.FromHex(ctx.EntityColor(e));
        int n = Math.Min(4, e.Corners.Count);
        var ring = new Vec2[n];
        for (int i = 0; i < n; i++) ring[i] = LineFamily.World(e.Corners[i], m);
        bool arrow = IsArrowheadForm(e.Corners);
        device.FillPolygon(ring, arrow
            ? new FillStyle(rgba, 1.0, Stroked: false, RasterAlpha: 255)
            : new FillStyle(rgba, 0.3, Stroked: true, RasterAlpha: 80));
    }
}
