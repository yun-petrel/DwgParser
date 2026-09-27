namespace DwgParser.Core.Objects.Records;

public sealed class TableContentValueCapture : RecordIdentity
{
    public TableContentValues Values { get; } = new();

    public bool ValuesCommitted;

    public long WalkEndBit;

    public string FailReason = "";
}
