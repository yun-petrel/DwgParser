namespace DwgParser.Core.Objects.Records;

public interface IStyle : IRecord
{
    string Name { get; }

    string FontFile { get; }

    string BigfontFile { get; }

    bool IsShape { get; }

    bool IsVertical { get; }

    double FixedHeight { get; }

    double WidthFactor { get; }

    double ObliqueAngle { get; }

    int XrefResolved { get; }

    bool Captured { get; }

    int Generation { get; }

    double LastHeight { get; }
}
