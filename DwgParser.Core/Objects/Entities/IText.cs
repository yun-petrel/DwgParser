namespace DwgParser.Core.Objects;

public interface IText : IEntity
{
    string TextValue { get; }

    double Height { get; }

    double Rotation { get; }

    double WidthFactor { get; }

    Vec2 Start { get; }

    Vec2 AlignmentPoint { get; }

    double ObliqueAngle { get; }

    short TextGenerationFlags { get; }

    short HorizMode { get; }

    short VertMode { get; }
}
