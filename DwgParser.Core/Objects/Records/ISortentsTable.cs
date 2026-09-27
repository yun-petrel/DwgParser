namespace DwgParser.Core.Objects.Records;

public interface ISortentsTable : IRecord
{
    long NumEntities { get; }

    IReadOnlyList<ulong> SortEntityHandles { get; }

    ulong BlockOwnerHandle { get; }

    IReadOnlyList<ulong> EntityHandles { get; }

    bool StringStreamPresent { get; }

    bool Captured { get; }

    bool EntriesCaptured { get; }
}
