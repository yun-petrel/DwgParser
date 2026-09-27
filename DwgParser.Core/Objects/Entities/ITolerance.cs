namespace DwgParser.Core.Objects;

public interface ITolerance : IEntity
{
    double DirX { get; }

    double DirY { get; }

    double DirZ { get; }

    string ToleranceText { get; }

    int ContentSize { get; }
}
