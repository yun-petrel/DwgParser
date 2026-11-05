namespace DwgParser.Core.Objects;

public static class EntityKindRegistry
{
    public readonly record struct Entry(DwgEntityType Kind, DwgEntityType RasterKind, bool HasSvg, bool HasRaster);

    public static readonly Entry[] All =
    [
        new(DwgEntityType.Unknown,      DwgEntityType.Unknown,     false, false),
        new(DwgEntityType.Text,         DwgEntityType.Text,        true,  true),
        new(DwgEntityType.Insert,       DwgEntityType.Insert,      true,  false),
        new(DwgEntityType.Vertex2D,     DwgEntityType.Vertex2D,    false, false),
        new(DwgEntityType.Attrib,       DwgEntityType.Text,        true,  true),
        new(DwgEntityType.SeqEnd,       DwgEntityType.SeqEnd,      false, false),
        new(DwgEntityType.Polyline2D,   DwgEntityType.LwPolyline,  true,  true),
        new(DwgEntityType.Arc,          DwgEntityType.Arc,         true,  true),
        new(DwgEntityType.Circle,       DwgEntityType.Circle,      true,  true),
        new(DwgEntityType.Line,         DwgEntityType.Line,        true,  true),
        new(DwgEntityType.Point,        DwgEntityType.Point,       true,  true),
        new(DwgEntityType.Solid,        DwgEntityType.Solid,       true,  true),
        new(DwgEntityType.Trace,        DwgEntityType.Solid,       true,  true),
        new(DwgEntityType.Ellipse,      DwgEntityType.Ellipse,     true,  true),
        new(DwgEntityType.LwPolyline,   DwgEntityType.LwPolyline,  true,  true),
        new(DwgEntityType.BlockHeader,  DwgEntityType.BlockHeader, false, false),
        new(DwgEntityType.EndBlk,       DwgEntityType.EndBlk,      false, false),
        new(DwgEntityType.Block,        DwgEntityType.Block,       false, false),
        new(DwgEntityType.Dimension,    DwgEntityType.Dimension,   true,  true),
        new(DwgEntityType.Spline,       DwgEntityType.PointSequence, true, true),
        new(DwgEntityType.Ray,          DwgEntityType.Line,        true,  true),
        new(DwgEntityType.XLine,        DwgEntityType.Line,        true,  true),
        new(DwgEntityType.Leader,       DwgEntityType.PointSequence, true, true),
        new(DwgEntityType.Hatch,        DwgEntityType.Hatch,       true,  true),
        new(DwgEntityType.MText,        DwgEntityType.Text,        true,  true),
        new(DwgEntityType.MLine,        DwgEntityType.MLine,       true,  false),
        new(DwgEntityType.Tolerance,    DwgEntityType.Tolerance,   true,  false),
        new(DwgEntityType.Viewport,     DwgEntityType.Viewport,    true,  true),
        new(DwgEntityType.Region,       DwgEntityType.Region,      true,  false),
        new(DwgEntityType.Solid3d,      DwgEntityType.Solid3d,     true,  false),
        new(DwgEntityType.Body,         DwgEntityType.Body,        true,  false),
        new(DwgEntityType.AttDef,       DwgEntityType.Text,        true,  true),
        new(DwgEntityType.Table,        DwgEntityType.Table,       true,  true),
        new(DwgEntityType.MLeader,      DwgEntityType.MLeader,     true,  true),
        new(DwgEntityType.PointSequence, DwgEntityType.PointSequence, false, true),
    ];

    internal static DwgEntityType RasterKindOf(DwgEntityType kind)
        => _rasterKindByKind.TryGetValue(kind, out var k) ? k : kind;

    private static readonly System.Collections.Generic.Dictionary<DwgEntityType, DwgEntityType>
        _rasterKindByKind = All.ToDictionary(e => e.Kind, e => e.RasterKind);
}
