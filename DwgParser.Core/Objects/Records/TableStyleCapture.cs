namespace DwgParser.Core.Objects.Records;

public sealed class TableStyleCapture : RecordIdentity, ITableStyle
{
    public bool Captured;

    public int FlowDirection, Flags, TitleSuppressed, HeaderSuppressed;

    public bool HeaderNonDefault;

    public string FailReason = "";
    public long FailBit = -1;

    public string Name = "";

    public double HorizMargin, VertMargin;

    public long HeadEndBit;

    public long HsCommonEndBit;

    public readonly ulong[] Tsty = new ulong[3];

    public readonly TableStyleRowCapture[] Rows = { new(), new(), new() };

    public TableStyleCellStyle? BaseCellStyle;

    public readonly List<(int Bl3, TableStyleCellStyle Style)> OverrideCellStyles = new();

    public bool HasAnchors => Captured;

    public bool ValuesCommitted;

    public bool DownReady;

    bool ITableStyle.Captured => Captured;
    string ITableStyle.Name => Name;
    double ITableStyle.HorizMargin => HorizMargin;
    double ITableStyle.VertMargin => VertMargin;
}

public sealed class TableStyleRowCapture
{
    public double TextHeight;
    public int Alignment;
    public int DataType, UnitType;
    public (int Idx, uint Rgb, byte Flag) TextColor, FillColor;
    public readonly int[] BorderLw = new int[6];
    public readonly (int Idx, uint Rgb, byte Flag)[] BorderCol =
        new (int, uint, byte)[6];
    public bool HasBg;
    public readonly bool[] BorderVis = new bool[6];
}
