namespace DwgParser.Core.Objects.Records;

public sealed class TableControlCapture : RecordIdentity, ITableControl
{
    public long NumEntries = -1;

    public int NumMoreHandles = -1;

    public bool StringStreamPresent;

    public bool Captured;

    public long WalkEndBit;

    public string FailReason = "";

    public List<ulong> EntryHandles = new();

    public List<ulong> MoreHandleRefs = new();

    public bool EntriesCaptured;

    public bool EntriesValidated;

    public string EntriesFailReason = "";

    long ITableControl.NumEntries => NumEntries;
    int ITableControl.NumMoreHandles => NumMoreHandles;
    bool ITableControl.StringStreamPresent => StringStreamPresent;
    bool ITableControl.Captured => Captured;
    IReadOnlyList<ulong> ITableControl.EntryHandles => EntryHandles;
    IReadOnlyList<ulong> ITableControl.MoreHandleRefs => MoreHandleRefs;
    bool ITableControl.EntriesCaptured => EntriesCaptured;
    bool ITableControl.EntriesValidated => EntriesValidated;
}
