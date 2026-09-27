namespace DwgParser.Core.Objects.Records;

public sealed class TableContentValues
{
    public const int NoField = -1;

    public string Layout = "";

    public string LdataName = "";

    public string Description = "";

    public int NumCols = NoField;

    public readonly List<TableContentColumnValues> Columns = new();

    public int NumRows = NoField;

    public readonly List<TableContentRowValues> Rows = new();

    public int NumFieldRefs = NoField;

    public TableCellStyle? BaseStyle;

    public readonly List<TableMergedCell> MergedCells = new();

    public long StringsConsumedBits;
}

public sealed class TableContentColumnValues
{
    public string Name = "";

    public int CustomData;

    public int NumCustomDataItems;

    public readonly List<string> CustomDataItems = new();

    public int HsCustomDataRefs;

    public TableCellStyle? Style;

    public int StyleId;

    public double Width;
}

public sealed class TableContentRowValues
{
    public int NumCells;

    public readonly List<TableEntityCell> Cells = new();

    public int CustomData;

    public int NumCustomDataItems;

    public int HsCustomDataRefs;

    public TableCellStyle? Style;

    public int StyleId;

    public double Height;
}
