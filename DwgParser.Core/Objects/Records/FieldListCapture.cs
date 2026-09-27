namespace DwgParser.Core.Objects.Records;

public sealed class FieldListCapture : RecordIdentity, IFieldList
{
    public long NumFields = -1;

    public bool UnknownFlag;

    public List<ulong> FieldHandles = new();

    public bool StringStreamPresent;

    public bool Captured;

    public bool EntriesCaptured;

    public long WalkEndBit;

    public string FailReason = "";

    public string EntriesFailReason = "";

    long IFieldList.NumFields => NumFields;
    bool IFieldList.UnknownFlag => UnknownFlag;
    IReadOnlyList<ulong> IFieldList.FieldHandles => FieldHandles;
    bool IFieldList.StringStreamPresent => StringStreamPresent;
    bool IFieldList.Captured => Captured;
    bool IFieldList.EntriesCaptured => EntriesCaptured;
}
