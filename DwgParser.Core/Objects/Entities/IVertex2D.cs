namespace DwgParser.Core.Objects;

public interface IVertex2D : IEntity
{
    Vec2 Start { get; }

    double Bulge { get; }

    int Flag { get; }

    double StartWidth { get; }

    double EndWidth { get; }

    double TangentDir { get; }
}
