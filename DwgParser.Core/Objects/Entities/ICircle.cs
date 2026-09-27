namespace DwgParser.Core.Objects;

public interface ICircle : IEntity
{
    Vec2 Center { get; }

    double Radius { get; }

    double Thickness { get; }

    double ExtrusionZ { get; }

    double Elevation { get; }
}
