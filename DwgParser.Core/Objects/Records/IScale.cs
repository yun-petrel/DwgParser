namespace DwgParser.Core.Objects.Records;

public interface IScale : IRecord
{
    string Name { get; }

    int Flag { get; }

    double PaperUnits { get; }

    double DrawingUnits { get; }

    bool IsUnitScale { get; }

    bool Captured { get; }
}
