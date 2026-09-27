namespace DwgParser.Core.Objects.Records;

public interface IDictWdflt : IRecord
{
    long NumEntries { get; }

    string EntryName { get; }

    bool Captured { get; }

    long DuplicateCloning { get; }

    int HardOwner { get; }
}
