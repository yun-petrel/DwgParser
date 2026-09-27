namespace DwgParser.Core.Objects.Records;

public sealed class SortentsTableCapture : RecordIdentity, ISortentsTable
{
    public long NumEntities = -1;

    public List<ulong> SortEntityHandles = new();

    public ulong BlockOwnerHandle;

    public List<ulong> EntityHandles = new();

    public bool StringStreamPresent;

    public bool Captured;

    public bool EntriesCaptured;

    public long WalkEndBit;

    public string FailReason = "";

    public string EntriesFailReason = "";

    long ISortentsTable.NumEntities => NumEntities;
    IReadOnlyList<ulong> ISortentsTable.SortEntityHandles => SortEntityHandles;
    ulong ISortentsTable.BlockOwnerHandle => BlockOwnerHandle;
    IReadOnlyList<ulong> ISortentsTable.EntityHandles => EntityHandles;
    bool ISortentsTable.StringStreamPresent => StringStreamPresent;
    bool ISortentsTable.Captured => Captured;
    bool ISortentsTable.EntriesCaptured => EntriesCaptured;
}
