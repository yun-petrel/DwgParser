namespace DwgParser.Core.Objects;

public interface IEllipse : IEntity
{
    Vec2 Center { get; }

    Vec2 Start { get; }

    double Radius { get; }

    double WidthFactor { get; }

    double StartAngle { get; }

    double EndAngle { get; }

    double ExtrusionZ { get; }

    double Elevation { get; }
}
