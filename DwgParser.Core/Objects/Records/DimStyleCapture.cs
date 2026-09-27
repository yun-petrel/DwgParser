namespace DwgParser.Core.Objects.Records;

public sealed class DimStyleCapture
{
    public long NameStartBit;

    public long BodyStartBit;

    public long NineBdEndBit;

    public long ThreeBsEndBit;

    public long CmcStartBit;

    public long FxlonEndBit;

    public long Flag0EndBit;

    public long LwdStartBit;

    public int XrefResolved;

    public long EedStartBit;

    public int EedBitLen;

    public string? Name;

    public string? Dimpost;

    public string? Dimapost;

    public bool ValuesCommitted;

    public bool DownCaptured;

    public int DownBlockBits;


    public long DownXrefEndBit;

    public long DownNineBdEndBit;

    public long DownHeadEndBit;

    public long DownThreeBsEndBit;

    public long DownArcSymEndBit;

    public long DownAtFitEndBit;

    public long CmcEndBit;

    public long HsBit;

    public short[]? PreR2004CmcIndices;

    public System.Collections.Generic.List<DwgObjectRecord.StringField>? DeferredStringFields;

    public long HandleStreamStart => HsBit > 0 ? HsBit : RecHandleStreamStart;

    public long RecHandleStreamStart;

    public ulong OwnHandle;

    public System.Collections.Generic.List<DimStyleHandleRef> HandleRefs = new();

    public bool HandleRefsCaptured;

    public string HandleRefsFailReason = "";

    public readonly record struct DimStyleHandleRef(
        long BitStart, int BitLen, int Code, ulong Value, ulong Resolved, string Slot);

    public bool HasAnchors => Flag0EndBit > 0;
}

