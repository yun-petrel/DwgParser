namespace DwgParser.Core.Objects.Records;

public sealed class ProxyObjectCapture : RecordIdentity
{
    public long DataStartBit;
    public long HandleStreamStartBit;

    public long HeadStartBit;
    public long HeadEndBit;

    public long ClassId;

    public string ClassName = string.Empty;

    public long NameStartBit = -1;

    public int NameBitLen;

    public long DsBitPos = -1;

    public long Version;

    public bool FromDxf;

    public long DataRegionStartBit;
    public long DataRegionEndBit;

    public long TailMarkerRelBit = -1;

    public bool Captured;

    public bool ValuesCommitted;

    public string ClassNameSource = "";

    public string FailReason = "";

    public long FailBit = -1;
}
