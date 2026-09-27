namespace DwgParser.Core.Objects;

public interface IHatch : IEntity
{
    bool IsSolidFill { get; }

    bool IsGradientFill { get; }

    double GradientAngle { get; }

    double GradientShift { get; }

    bool GradientSingleColor { get; }

    double GradientTint { get; }

    long GradientColorCount { get; }

    System.Collections.Generic.List<(double Shift, short Index, uint Rgb, string Name, string BookName)> GradientColors { get; }

    bool IsAssociative { get; }

    System.Collections.Generic.List<long> PathFlags { get; }

    System.Collections.Generic.List<bool> ArcCcwFlags { get; }

    System.Collections.Generic.List<bool> EllipseCcwFlags { get; }

    System.Collections.Generic.List<(int Degree, bool Rational, bool Periodic)> Splines { get; }


    Vec3 Extrusion { get; }

    string PatternName { get; }

    System.Collections.Generic.List<HatchBoundarySegment> Segments { get; }

    long PathCount { get; }

    System.Collections.Generic.List<long> PathBoundaryHandleCounts { get; }

    long BoundaryHandleTotal { get; }

    System.Collections.Generic.List<bool> PolylineBulgesPresent { get; }


    long Style { get; }

    long PatternType { get; }

    double? PatternAngle { get; }

    double? PatternScale { get; }

    bool? PatternDouble { get; }

    System.Collections.Generic.List<(double Angle, Vec2 Base, Vec2 Offset,
        System.Collections.Generic.List<double> Dashes)> PatternLines { get; }

    double? PixelSize { get; }

    System.Collections.Generic.List<Vec2> SeedPoints { get; }
}
