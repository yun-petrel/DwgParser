namespace DwgParser.Core.Objects;

public interface IPolyline2D : IEntity
{
    bool Closed { get; }

    bool PlineGen { get; }

    int CurveType { get; }

    double StartWidth { get; }

    double EndWidth { get; }

    System.Collections.Generic.List<Vec2> Points { get; }

    System.Collections.Generic.List<double> Bulges { get; }
}
