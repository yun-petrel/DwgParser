namespace DwgParser.Core.Objects;

public interface IInsert : IEntity
{
    Vec2 Start { get; }

    double Rotation { get; }

    double ScaleX { get; }

    double ScaleY { get; }

    double ScaleZ { get; }

    ulong BlockHeaderHandle { get; }

    bool HasAttributes { get; }
}
