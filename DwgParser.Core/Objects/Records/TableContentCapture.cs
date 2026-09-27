namespace DwgParser.Core.Objects.Records;

public sealed class TableContentCapture
{
    public bool Captured;

    public string FailReason = "";
    public long FailBit = -1;

    public int SourceDataBits;

    public int UnitAnchorBits;

    public int UnitStepBits;

    public int UnitCount;

    public int CellGroupCount;

    public readonly List<long> UnitStarts = new();

    public long FirstUnitBit;

    public int HeaderBits;
}
