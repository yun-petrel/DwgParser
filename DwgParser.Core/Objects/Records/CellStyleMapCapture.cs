namespace DwgParser.Core.Objects.Records;

public sealed class CellStyleMapCapture
{
    public bool Captured;

    public int NumCells;

    public readonly List<CellStyleMapString> Strings = new();


    public bool ValuesCommitted;

    public readonly List<CellStyleMapCell> Cells = new();


    public bool SurgeryCommitted;

    public string FailReason = "";

    public long FailBit = -1;
}

public readonly record struct CellStyleMapString(long Pos, string Value);
