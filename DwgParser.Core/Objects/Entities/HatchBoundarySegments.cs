namespace DwgParser.Core.Objects;

public sealed class HatchBoundarySegment
{
    public int CurveType { get; init; }

    public bool IsPolylinePath { get; init; }

    public Vec2 P1 { get; init; }
    public Vec2 P2 { get; init; }

    public Vec2 Center { get; init; }
    public double Radius { get; init; }
    public Vec2 MajorAxis { get; init; }
    public double Ratio { get; init; }
    public double StartAngle { get; init; }
    public double EndAngle { get; init; }
    public bool IsCcw { get; init; }

    public int Degree { get; init; }
    public bool Rational { get; init; }
    public bool Periodic { get; init; }
    public double[] Knots { get; init; } = System.Array.Empty<double>();
    public (Vec2 Point, double Weight)[] ControlPoints { get; init; } = System.Array.Empty<(Vec2, double)>();
    public Vec2[] FitPoints { get; init; } = System.Array.Empty<Vec2>();
    public Vec2 StartTangent { get; init; }
    public Vec2 EndTangent { get; init; }
    public bool HasFitPoints { get; init; }

    public Vec2[] PolylinePoints { get; init; } = System.Array.Empty<Vec2>();
    public bool BulgesPresent { get; init; }
    public double[] Bulges { get; init; } = System.Array.Empty<double>();
    public bool IsClosed { get; init; }

    public long BoundaryHandleCount { get; set; }
}
