namespace DwgParser.Core.Objects.Records;

public sealed class AssocVertexParamCapture
{
    public bool Captured;

    public bool EntriesCaptured;

    public long SourceDataBits;

    public int IsR2013 = -1;

    public long? AapVersion;

    public string Name = "";

    public long AsdapClassVersion;

    public long ClassVersion;

    public double PtX;
    public double PtY;
    public double PtZ;

    public ulong OwnerHandle;

    public long NumReactors = -1;

    public IReadOnlyList<ulong> ReactorHandles = Array.Empty<ulong>();

    public bool XdicMissing;

    public ulong XdicHandle;

    public ulong Dep;

    public long HandleStreamPadBits = -1;

    public long StopBit = -1;

    public long WalkEndBit;

    public string FailReason = "";

    public long FailBit;

    public string EntriesFailReason = "";
}
