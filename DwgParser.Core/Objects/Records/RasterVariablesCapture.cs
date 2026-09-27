namespace DwgParser.Core.Objects.Records;

public sealed class RasterVariablesCapture : RecordIdentity, IRasterVariables
{
    public long ClassVersion = -1;

    public long ImageFrame = -1;

    public long ImageQuality = -1;

    public long Units = -1;

    public bool StringStreamPresent;

    public bool Captured;

    public long WalkEndBit;

    public string FailReason = "";

    long IRasterVariables.ClassVersion => ClassVersion;
    long IRasterVariables.ImageFrame => ImageFrame;
    long IRasterVariables.ImageQuality => ImageQuality;
    long IRasterVariables.Units => Units;
    bool IRasterVariables.StringStreamPresent => StringStreamPresent;
    bool IRasterVariables.Captured => Captured;
}
