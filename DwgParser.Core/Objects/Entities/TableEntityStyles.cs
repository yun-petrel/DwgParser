namespace DwgParser.Core.Objects;

public sealed class TableContentFormat
{
    public int PropertyOverrideFlags { get; internal set; }

    public int PropertyFlags { get; internal set; }

    public int ValueDataType { get; internal set; }

    public int ValueUnitType { get; internal set; }

    public string ValueFormatString { get; internal set; } = "";

    public double Rotation { get; internal set; }

    public double BlockScale { get; internal set; }

    public int CellAlignment { get; internal set; }

    public TableCmcColor? ContentColor { get; internal set; }

    public ulong TextStyleHandle { get; internal set; }

    public double TextHeight { get; internal set; }
}

public sealed class TableCellBorder
{
    public int IndexMask { get; internal set; }

    public int BorderOverrides { get; internal set; }

    public int BorderType { get; internal set; }

    public TableCmcColor? Color { get; internal set; }

    public int LineWeight { get; internal set; }

    public ulong LineTypeHandle { get; internal set; }

    public int Visible { get; internal set; }

    public double DoubleLineSpacing { get; internal set; }
}

public sealed class TableCellStyle
{
    public int Type { get; internal set; }

    public short DataFlags { get; internal set; }

    public int PropertyOverrideFlags { get; internal set; }

    public int MergeFlags { get; internal set; }

    public TableCmcColor? BackgroundColor { get; internal set; }

    public int ContentLayout { get; internal set; }

    public TableContentFormat? ContentFormat { get; internal set; }

    public short MarginOverrideFlags { get; internal set; }

    public List<double> Margins { get; } = new();

    public List<TableCellBorder> Borders { get; } = new();
}

public sealed class TableCellAttrDef
{
    public int AttdefHandleCode { get; internal set; }

    public ulong AttdefHandle { get; internal set; }

    public int Index { get; internal set; }

    public string Text { get; internal set; } = "";
}

public readonly record struct TableMergedCell(int TopRow, int LeftCol, int BottomRow, int RightCol);
