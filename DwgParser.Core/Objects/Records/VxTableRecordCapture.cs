namespace DwgParser.Core.Objects.Records;

public sealed class VxTableRecordCapture
{
    public bool Captured;

    public int SourceDataBits;

    public string Name = "";

    public bool IsXrefRef;

    public int XrefResolved;

    public bool IsXrefDep;

    public bool IsOn;

    public ulong Xref;

    public ulong Viewport;

    public ulong PrevEntry;

    public long WalkEndBit;

    public string FailReason = "";

    public long FailBit;
}
