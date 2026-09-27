namespace DwgParser.Core.Objects.Records;

public sealed class DimAssocCapture
{
    public bool AnchorsCaptured;

    public bool ValuesCommitted;

    public bool CentralCommitted;

    public string FailReason = "";
    public long FailBit = -1;

    public long HsBit;
    public long EedStartBit;
    public int EedBitLen;

    public int Associativity;

    public bool TransSpaceFlag;

    public int RotatedType;

    public List<DimAssocRef> Refs = new();

    public int StringCount;

    public readonly List<DwgObjectRecord.StringField> CentralStrings = new();

    public readonly List<long> CentralInsertBits = new();

    public long CentralSsBit = -1;
}

public sealed class DimAssocRef
{
    public int Bit;
    public bool Phantom;

    public string ClassName = "";

    public int OsnapType;

    public long NumXrefs;

    public int MainSubentType;
    public int MainGsMarker;
    public long NumXrefPaths;

    public List<string> XrefPaths = new();

    public double OsnapDistance;
    public double[] OsnapPoint = new double[3];

    public int NumIntersectObj;
    public int IntersectSubentType;
    public int IntersectGsMarker;
    public long NumIntersecPaths;
    public List<string> IntersecPaths = new();

    public bool HasLastPtRef;
}
