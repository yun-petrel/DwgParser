namespace DwgParser.Core.Objects;

public interface IRay : IEntity
{
    Vec2 Start { get; }

    Vec2 End { get; }

    double Rotation { get; }
}
