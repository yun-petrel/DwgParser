namespace DwgParser.Core.Objects.Records;

public interface IMLineStyle : IRecord
{
    string Name { get; }

    string Description { get; }

    bool Captured { get; }

    bool SemanticCaptured { get; }

    int Flag { get; }

    (int Idx, uint Rgb, byte Flag) FillColor { get; }

    double StartAngle { get; }

    double EndAngle { get; }

    MLineStyleCapture.MLineStyleLine[]? Lines { get; }
}
