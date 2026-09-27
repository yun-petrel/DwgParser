namespace DwgParser.Core.Objects.Records;

public sealed class PurgePreventerCapture
{
    public bool Captured;

    public int SourceDataBits;

    public int TailBits;

    public int Flag;

    public long WalkEndBit;

    public string FailReason = "";

    public long FailBit;
}
