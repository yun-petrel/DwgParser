namespace DwgParser.Core.Objects.Records;

public sealed class AppidCapture : RecordIdentity, IAppid
{
    public string Name { get; internal set; } = "";

    public int XrefResolved { get; internal set; }

    public int Unknown71 { get; internal set; } = -1;

    public bool HasUnknown71 { get; internal set; }

    public bool Captured { get; internal set; }
}
