namespace DwgParser.Core.Objects;

public interface IArc : IEntity
{
    Vec2 Center { get; }

    double Radius { get; }

    double StartAngle { get; }

    double EndAngle { get; }

    double Thickness { get; }

    double ExtrusionZ { get; }

    double Elevation { get; }
}
