namespace DwgParser.Core.Objects;

public readonly record struct TableCmcColor(short Index, uint Rgb, string Name, string BookName);

public sealed class TableEntityCell
{
    public int Type { get; internal set; }

    public short EdgeFlags { get; internal set; }

    public bool IsMergedValue { get; internal set; }

    public bool IsAutoFit { get; internal set; }

    public int MergedWidth { get; internal set; } = 1;

    public int MergedHeight { get; internal set; } = 1;

    public double Rotation { get; internal set; }

    public string Text { get; internal set; } = "";

    public string? Tooltip { get; internal set; }

    public string? FormattedValue { get; internal set; }

    public double BlockScale { get; internal set; } = 1.0;

    public bool HasOverride { get; internal set; }

    public int CellFlagOverride { get; internal set; }

    public short VirtualEdgeFlag { get; internal set; }

    public short? Alignment { get; internal set; }

    public double? TextHeight { get; internal set; }

    public bool? BgFillNone { get; internal set; }

    public short? TopLineWeight { get; internal set; }
    public short? RightLineWeight { get; internal set; }
    public short? BottomLineWeight { get; internal set; }
    public short? LeftLineWeight { get; internal set; }
    public short? TopVisibility { get; internal set; }
    public short? RightVisibility { get; internal set; }
    public short? BottomVisibility { get; internal set; }
    public short? LeftVisibility { get; internal set; }

    public List<TableCellAttrDef> AttrDefs { get; } = new();

    public TableCellStyle? CellStyle { get; internal set; }

    public ulong TextStyleHandle { get; internal set; }

    public TableContentFormat? ContentFormat { get; internal set; }

    public Records.TableGeometryCellValues? GeomData { get; internal set; }

    internal bool HsHasDataLink;
    internal bool HsHasGeomData;
    internal int HsCustomDataRefs;
    internal int HsNumContents;

    public int ValueFormatFlags { get; internal set; }
    public int ValueDataType { get; internal set; }
    public int ValueUnitType { get; internal set; }
    public double ValueDouble { get; internal set; }
    public int ValueLong { get; internal set; }
    public byte[]? ValueDateBytes { get; internal set; }
    public Vec2? ValuePoint2D { get; internal set; }
    public Vec3? ValuePoint3D { get; internal set; }
    public string? ValueFormat { get; internal set; }
    public int? LinkedRows { get; internal set; }
    public int? LinkedCols { get; internal set; }

    public TableCmcColor? BgColor { get; internal set; }
    public TableCmcColor? ContentColor { get; internal set; }
    public TableCmcColor? TopColor { get; internal set; }
    public TableCmcColor? RightColor { get; internal set; }
    public TableCmcColor? BottomColor { get; internal set; }
    public TableCmcColor? LeftColor { get; internal set; }

    internal long CellStartBit = -1;
    internal long CellEndBit = -1;
    internal long TextStartBit = -1;
    internal long TextEndBit = -1;

    internal long FlatEndBit = -1;
    internal long ValueUnionStartBit = -1;
}

public sealed class TableStyleOverride
{
    public int Flag { get; internal set; }

    public bool? TitleSuppressed { get; internal set; }

    public short? FlowDirection { get; internal set; }

    public double? HorzCellMargin { get; internal set; }

    public double? VertCellMargin { get; internal set; }

    public short? TitleRowAlignment { get; internal set; }
    public short? HeaderRowAlignment { get; internal set; }
    public short? DataRowAlignment { get; internal set; }

    public double? TitleRowHeight { get; internal set; }
    public double? HeaderRowHeight { get; internal set; }
    public double? DataRowHeight { get; internal set; }

    public bool? TitleFillNone { get; internal set; }
    public bool? HeaderFillNone { get; internal set; }
    public bool? DataFillNone { get; internal set; }

    public TableCmcColor? TitleRowColor { get; internal set; }
    public TableCmcColor? HeaderRowColor { get; internal set; }
    public TableCmcColor? DataRowColor { get; internal set; }
    public TableCmcColor? TitleFillColor { get; internal set; }
    public TableCmcColor? HeaderFillColor { get; internal set; }
    public TableCmcColor? DataFillColor { get; internal set; }
}

public sealed class TableBorderOverride
{
    public int Flag { get; internal set; }

    public List<short> Values { get; } = new();

    public List<uint> Rgbs { get; } = new();

    public List<string> ColorNames { get; } = new();

    public List<string> ColorBookNames { get; } = new();
}
