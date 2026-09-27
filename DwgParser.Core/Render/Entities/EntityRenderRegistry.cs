using DwgParser.Core.Objects;

namespace DwgParser.Core.Render;

internal static class EntityRenderRegistry
{
    internal static bool RasterReady(DwgEntityType kind) => kind switch
    {
        DwgEntityType.Line or DwgEntityType.Ray or DwgEntityType.XLine
            or DwgEntityType.Circle or DwgEntityType.Arc or DwgEntityType.Viewport
            or DwgEntityType.LwPolyline or DwgEntityType.Polyline2D
            or DwgEntityType.Spline or DwgEntityType.Leader
            or DwgEntityType.Text or DwgEntityType.MText
            or DwgEntityType.AttDef or DwgEntityType.Attrib
            or DwgEntityType.Dimension => true,
        _ => false,
    };

    internal static IEntityRenderer? For(DwgEntityType kind) => kind switch
    {
        DwgEntityType.Line => LineRenderer.Instance,
        DwgEntityType.Ray => RayRenderer.Instance,
        DwgEntityType.XLine => XLineRenderer.Instance,
        DwgEntityType.Circle => CircleRenderer.Instance,
        DwgEntityType.Arc => ArcRenderer.Instance,
        DwgEntityType.Viewport => ViewportRenderer.Instance,
        DwgEntityType.LwPolyline or DwgEntityType.Polyline2D => PolylineRenderer.Instance,
        DwgEntityType.Spline => PointSequenceRenderer.Instance,
        DwgEntityType.Leader => LeaderRenderer.Instance,
        DwgEntityType.Text or DwgEntityType.MText or DwgEntityType.AttDef or DwgEntityType.Attrib
            => TextEntityRenderer.Instance,
        DwgEntityType.Solid or DwgEntityType.Trace => SolidRenderer.Instance,
        DwgEntityType.Dimension => DimensionEntityRenderer.Instance,

        DwgEntityType.MLine => MLineRenderer.Instance,
        DwgEntityType.Tolerance => ToleranceRenderer.Instance,
        DwgEntityType.Region or DwgEntityType.Solid3d or DwgEntityType.Body
            => AcisWireframeRenderer.Instance,

        DwgEntityType.Insert or DwgEntityType.Point
            or DwgEntityType.Ellipse or DwgEntityType.Hatch
            or DwgEntityType.Table or DwgEntityType.MLeader => null,

        DwgEntityType.Unknown or DwgEntityType.Vertex2D or DwgEntityType.SeqEnd
            or DwgEntityType.BlockHeader or DwgEntityType.EndBlk or DwgEntityType.Block
            or DwgEntityType.PointSequence => null,

        _ => throw new InvalidOperationException(
            $"DwgEntityType.{kind} 未在 EntityRenderRegistry 表态（漏一枚 = 图上静默少画，勿以 null 兜）"),
    };

    internal static string? TryRenderSvg(DwgEntity e, RenderContext ctx, Matrix toWorld)
    {
        if (For(e.Kind) is not { } renderer) return null;
        var device = new SvgDrawDevice(ctx);
        renderer.Render(e, ctx, toWorld, device);
        return device.Body;
    }
}
