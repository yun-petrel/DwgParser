namespace DwgParser.Core.Objects.Records;

public interface IFieldList : IRecord
{
    long NumFields { get; }

    bool UnknownFlag { get; }

    IReadOnlyList<ulong> FieldHandles { get; }

    bool StringStreamPresent { get; }

    bool Captured { get; }

    bool EntriesCaptured { get; }
}
