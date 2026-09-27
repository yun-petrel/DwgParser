namespace DwgParser.Core.Objects;

public interface IPoint : IEntity
{
    Vec2 Start { get; }

    double Elevation { get; }

    double Thickness { get; }

    double ExtrusionZ { get; }

    double PointAngle { get; }
}
