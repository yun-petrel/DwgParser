namespace DwgParser.Core.Objects;

public interface ILwPolyline : IEntity
{
    List<Vec2> Points { get; }

    List<double> Bulges { get; }

    List<double> VertexStartWidths { get; }

    List<double> VertexEndWidths { get; }

    bool Closed { get; }

    bool PlineGen { get; }

    double ConstantWidth { get; }

    double Elevation { get; }

    double Thickness { get; }

    double ExtrusionZ { get; }
}
