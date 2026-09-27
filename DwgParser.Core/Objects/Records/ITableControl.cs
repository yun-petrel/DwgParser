namespace DwgParser.Core.Objects.Records;

public interface ITableControl : IRecord
{
    long NumEntries { get; }

    int NumMoreHandles { get; }

    bool StringStreamPresent { get; }

    bool Captured { get; }

    IReadOnlyList<ulong> EntryHandles { get; }

    IReadOnlyList<ulong> MoreHandleRefs { get; }

    bool EntriesCaptured { get; }

    bool EntriesValidated { get; }
}
