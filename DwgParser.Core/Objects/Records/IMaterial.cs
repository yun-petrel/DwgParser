namespace DwgParser.Core.Objects.Records;

public interface IMaterial : IRecord
{
    string Name { get; }

    string Description { get; }

    bool Captured { get; }
}
