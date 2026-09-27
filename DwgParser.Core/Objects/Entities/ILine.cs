namespace DwgParser.Core.Objects;

public interface ILine : IEntity
{
    Vec2 Start { get; }

    Vec2 End { get; }

    double StartZ { get; }

    double EndZ { get; }

    double Thickness { get; }

    double ExtrusionZ { get; }
}
