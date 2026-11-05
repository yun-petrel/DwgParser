namespace DwgParser.Core.Objects;

public static class EntityKindRegistry
{
    public readonly record struct Entry(DwgEntityType Kind, DwgEntityType RasterKind);

    public static readonly Entry[] All =
    [
        new(DwgEntityType.Unknown,      DwgEntityType.Unknown),
        new(DwgEntityType.Text,         DwgEntityType.Text),
        new(DwgEntityType.Insert,       DwgEntityType.Insert),
        new(DwgEntityType.Vertex2D,     DwgEntityType.Vertex2D),
        new(DwgEntityType.Attrib,       DwgEntityType.Text),
        new(DwgEntityType.SeqEnd,       DwgEntityType.SeqEnd),
        new(DwgEntityType.Polyline2D,   DwgEntityType.LwPolyline),
        new(DwgEntityType.Arc,          DwgEntityType.Arc),
        new(DwgEntityType.Circle,       DwgEntityType.Circle),
        new(DwgEntityType.Line,         DwgEntityType.Line),
        new(DwgEntityType.Point,        DwgEntityType.Point),
        new(DwgEntityType.Solid,        DwgEntityType.Solid),
        new(DwgEntityType.Trace,        DwgEntityType.Solid),
        new(DwgEntityType.Ellipse,      DwgEntityType.Ellipse),
        new(DwgEntityType.LwPolyline,   DwgEntityType.LwPolyline),
        new(DwgEntityType.BlockHeader,  DwgEntityType.BlockHeader),
        new(DwgEntityType.EndBlk,       DwgEntityType.EndBlk),
        new(DwgEntityType.Block,        DwgEntityType.Block),
        new(DwgEntityType.Dimension,    DwgEntityType.Dimension),
        new(DwgEntityType.Spline,       DwgEntityType.PointSequence),
        new(DwgEntityType.Ray,          DwgEntityType.Line),
        new(DwgEntityType.XLine,        DwgEntityType.Line),
        new(DwgEntityType.Leader,       DwgEntityType.PointSequence),
        new(DwgEntityType.Hatch,        DwgEntityType.Hatch),
        new(DwgEntityType.MText,        DwgEntityType.Text),
        new(DwgEntityType.MLine,        DwgEntityType.MLine),
        new(DwgEntityType.Tolerance,    DwgEntityType.Tolerance),
        new(DwgEntityType.Viewport,     DwgEntityType.Viewport),
        new(DwgEntityType.Region,       DwgEntityType.Region),
        new(DwgEntityType.Solid3d,      DwgEntityType.Solid3d),
        new(DwgEntityType.Body,         DwgEntityType.Body),
        new(DwgEntityType.AttDef,       DwgEntityType.Text),
        new(DwgEntityType.Table,        DwgEntityType.Table),
        new(DwgEntityType.MLeader,      DwgEntityType.MLeader),
        new(DwgEntityType.PointSequence, DwgEntityType.PointSequence),
    ];

    internal static DwgEntityType RasterKindOf(DwgEntityType kind)
        => _rasterKindByKind.TryGetValue(kind, out var k) ? k : kind;

    private static readonly System.Collections.Generic.Dictionary<DwgEntityType, DwgEntityType>
        _rasterKindByKind = All.ToDictionary(e => e.Kind, e => e.RasterKind);
}
