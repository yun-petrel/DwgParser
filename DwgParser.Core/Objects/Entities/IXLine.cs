namespace DwgParser.Core.Objects;

public interface IXLine : IEntity
{
    Vec2 Start { get; }

    Vec2 End { get; }

    double Rotation { get; }
}
