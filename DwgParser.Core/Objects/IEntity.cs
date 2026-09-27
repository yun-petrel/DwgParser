using System.Collections.Generic;
using DwgParser.Core.Render;

namespace DwgParser.Core.Objects;

public interface IEntity
{
    EntityKind Kind { get; }

    ulong Handle { get; }

    ulong LayerHandle { get; }

    ulong OwnerHandle { get; }

    short ColorIndex { get; }

    byte EntMode { get; }

    string ToSvg(RenderContext ctx, Matrix transform);
}

public enum EntityKind
{
    Unknown,
    Line,
    Circle,
    Arc,
    Point,
    Text,
    MText,
    Insert,
    LwPolyline,
    Polyline2D,
    Vertex2D,
    Solid,
    Trace,
    ThreeDFace,
    BlockHeader,
    EndBlk,
    Block,
    Attrib,
    AttDef,
    SeqEnd,
    Ellipse,
    Spline,
    Leader,
    MLeader,
    Hatch,
    Dimension,
    Viewport,
    Table,
    MLine,
    Tolerance,
    Ray,
    XLine
}