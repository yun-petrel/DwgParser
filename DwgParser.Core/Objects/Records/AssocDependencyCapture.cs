namespace DwgParser.Core.Objects.Records;

public sealed class AssocDependencyCapture
{
    public bool Captured;

    public bool EntriesCaptured;

    public long SourceDataBits;

    public bool IsGeomShape;

    public long NumReactors = -1;

    public bool XdicMissing;

    public int ClassVersion = -1;

    public long Status;

    public bool IsReadDep;

    public bool IsWriteDep;

    public bool IsAttachedToObject;

    public bool IsDelegatingToOwningAction;

    public long DepOrder;

    public bool HasName;

    public string Name = "";

    public long DepBodyId;

    public int GeomVersion = -1;

    public bool Enabled;

    public string PersubentClassname = "";

    public bool DependentOnCompoundObject;

    public ulong OwnerHandle;

    public IReadOnlyList<ulong> ReactorHandles = Array.Empty<ulong>();

    public ulong XdicHandle;

    public ulong DepOn;

    public ulong ReadDep;

    public ulong Node;

    public ulong DepBody;

    public long HandleStreamPadBits = -1;

    public long StopBit = -1;

    public long WalkEndBit;

    public string FailReason = "";

    public long FailBit;

    public string EntriesFailReason = "";
}
