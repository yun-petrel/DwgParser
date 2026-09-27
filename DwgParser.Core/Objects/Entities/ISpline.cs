namespace DwgParser.Core.Objects;

public interface ISpline : IEntity
{
    System.Collections.Generic.List<Vec2> Points { get; }

    Vec2 Start { get; }

    bool Closed { get; }

    long Scenario { get; }

    long Degree { get; }

    long Flags1 { get; }

    long KnotParam { get; }

    bool Rational { get; }

    bool Periodic { get; }

    double KnotTol { get; }

    double CtrlTol { get; }

    System.Collections.Generic.List<double> Knots { get; }

    System.Collections.Generic.List<double> Weights { get; }

    int KnotCount { get; }

    double FitTol { get; }

    (double X, double Y, double Z) BeginTangent { get; }

    (double X, double Y, double Z) EndTangent { get; }

    System.Collections.Generic.List<(double X, double Y, double Z)> FitPoints { get; }

    bool IsNurbsBranch { get; }
}
