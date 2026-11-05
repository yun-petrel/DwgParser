using DwgParser.Core.Objects;

namespace DwgParser.Core.Render;

internal static class EntityRenderRegistry
{
    internal static IEntityRenderer? For(DwgEntityType kind) => kind switch
    {
        DwgEntityType.Point => PointRenderer.Instance,
        DwgEntityType.Ellipse => EllipseRenderer.Instance,
        DwgEntityType.Hatch => HatchRenderer.Instance,
        DwgEntityType.Table => TableRenderer.Instance,
        DwgEntityType.MLeader => MLeaderRenderer.Instance,
        DwgEntityType.PointSequence => PointSequenceRenderer.Instance,
        _ => ForSvg(kind),
    };

    internal static IEntityRenderer? ForSvg(DwgEntityType kind) => kind switch
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

        DwgEntityType.Unknown or DwgEntityType.Vertex2D or DwgEntityType.SeqEnd
            or DwgEntityType.BlockHeader or DwgEntityType.EndBlk or DwgEntityType.Block
            or DwgEntityType.PointSequence
            or DwgEntityType.Insert or DwgEntityType.Point
            or DwgEntityType.Ellipse or DwgEntityType.Hatch
            or DwgEntityType.Table or DwgEntityType.MLeader => null,

        _ => throw new InvalidOperationException(
            $"DwgEntityType.{kind} 未在 EntityRenderRegistry 表态（漏一枚 = 图上静默少画，勿以 null 兜）"),
    };

    internal static string? TryRenderSvg(DwgEntity e, RenderContext ctx, Matrix toWorld)
    {
        if (ForSvg(e.Kind) is not { } renderer) return null;
        var device = new SvgDrawDevice(ctx);
        renderer.Render(e, ctx, toWorld, device);
        return device.Body;
    }
}
