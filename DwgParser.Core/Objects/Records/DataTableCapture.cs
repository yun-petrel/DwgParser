namespace DwgParser.Core.Objects.Records;

public sealed class DataTableCapture
{
    public bool Captured;

    public int SourceDataBits;

    public long WalkEndBit;

    public int Flags;

    public int NumCols;

    public int NumRows;

    public string TableName = "";

    public List<DataTableColumnCapture> Columns { get; } = new();

    public List<ulong> TailRefs { get; } = new();

    public bool TailCaptured;

    public int TailPadBits = -1;

    public string TailFailReason = "";

    public string FailReason = "";

    public long FailBit = -1;
}

public sealed class DataTableColumnCapture
{
    public int Type;

    public string Text = "";

    public bool ValuesInRegion;

    public List<double> Reals { get; } = new();

    public List<long> Integers { get; } = new();
}
