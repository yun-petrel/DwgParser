using DwgParser.Core.Objects;

namespace DwgParser.Core.Render;

public enum PathSegKind { Move, Line, Arc }

public readonly record struct PathSeg(PathSegKind Kind, Vec2 P1, Vec2 P2, double Bulge, bool InBounds = true)
{
    public static PathSeg Move(Vec2 p) => new(PathSegKind.Move, p, p, 0);
    public static PathSeg Line(Vec2 from, Vec2 to) => new(PathSegKind.Line, from, to, 0);
    public static PathSeg Arc(Vec2 from, Vec2 to, double bulge) => new(PathSegKind.Arc, from, to, bulge);

    public static PathSeg LineUnbounded(Vec2 from, Vec2 to) => new(PathSegKind.Line, from, to, 0, false);
    public static PathSeg ArcUnbounded(Vec2 from, Vec2 to, double bulge)
        => new(PathSegKind.Arc, from, to, bulge, false);
}

public enum PathLayout
{
    StrokeFirst,

    FillFirst,

    FillNoStroke,

    StrokeFirstSpaced,
}
