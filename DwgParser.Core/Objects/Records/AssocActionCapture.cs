namespace DwgParser.Core.Objects.Records;

public sealed class AssocActionCapture
{
    public bool Captured;

    public bool EntriesCaptured;

    public bool IsNetwork;

    public long SourceDataBits;

    public int ClassVersion = -1;

    public long GeometryStatus;

    public long ActionIndex;

    public long MaxAssocDepIndex;

    public long NumDeps = -1;

    public IReadOnlyList<bool> DepOwnedFlags = Array.Empty<bool>();

    public int NetworkVersion = -1;

    public long NetworkActionIndex = -1;

    public long NumActions = -1;

    public long NumOwnedActions = -1;

    public IReadOnlyList<bool> ActionOwnedFlags = Array.Empty<bool>();

    public ulong OwningNetwork;

    public ulong ActionBody;

    public IReadOnlyList<ulong> DepHandles = Array.Empty<ulong>();

    public IReadOnlyList<ulong> ActionHandles = Array.Empty<ulong>();

    public IReadOnlyList<ulong> OwnedActionHandles = Array.Empty<ulong>();

    public int OwnedParamSlot1 = -1;

    public long NumOwnedParams = -1;

    public IReadOnlyList<ulong> OwnedParamHandles = Array.Empty<ulong>();

    public int OwnedParamSlot2 = -1;

    public long NumValues = -1;

    public IReadOnlyList<AssocValueParamCapture> Values = Array.Empty<AssocValueParamCapture>();

    public long NumReactors = -1;

    public bool XdicMissing;

    public ulong OwnerHandle;

    public IReadOnlyList<ulong> ReactorHandles = Array.Empty<ulong>();

    public ulong XdicHandle;

    public long HandleStreamPadBits = -1;

    public long StopBit = -1;

    public long WalkEndBit;

    public string FailReason = "";

    public long FailBit;

    public string EntriesFailReason = "";
}
