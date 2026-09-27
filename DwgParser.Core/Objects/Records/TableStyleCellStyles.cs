namespace DwgParser.Core.Objects.Records;

public sealed class TableStyleCellStyle
{
    public int Type { get; internal set; }

    public short DataFlags { get; internal set; }

    public int PropertyOverrideFlags { get; internal set; }

    public int MergeFlags { get; internal set; }

    public TableCmcColor? BackgroundColor { get; internal set; }

    public int ContentLayout { get; internal set; }

    public TableStyleContentFormat? ContentFormat { get; internal set; }

    public short MarginOverrideFlags { get; internal set; }

    public List<double> Margins { get; } = new();

    public int NumBorders { get; internal set; }

    public List<TableStyleBorder> Borders { get; } = new();
}

public sealed class TableStyleContentFormat
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

    public double TextHeight { get; internal set; }

    public string ValueStyle { get; internal set; } = "";
}

public sealed class TableStyleBorder
{
    public int IndexMask { get; internal set; }

    public int BorderOverrides { get; internal set; }

    public int BorderType { get; internal set; }

    public TableCmcColor? Color { get; internal set; }

    public int LineWeight { get; internal set; }

    public int BorderLineType { get; internal set; }

    public int Visible { get; internal set; }

    public double DoubleLineSpacing { get; internal set; }
}
