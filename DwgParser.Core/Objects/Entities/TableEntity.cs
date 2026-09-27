using DwgParser.Core.IO;

namespace DwgParser.Core.Objects;

public abstract partial class TableEntity : DwgEntity, ITable
{
    public int Columns => TableColumns;

    public int Rows => TableRows;

    public short TableFlag { get; internal set; } = -1;

    public Vec2? HorizDirection { get; internal set; }

    public int? NumOwned { get; internal set; }

    public List<double> ColWidths { get; } = new();

    public List<double> RowHeights { get; } = new();

    public List<TableEntityCell> Cells { get; } = new();

    public TableStyleOverride? StyleOverride { get; internal set; }

    public List<TableBorderOverride> BorderOverrides { get; } = new();


    public string TableName { get; internal set; } = "";

    public string Description { get; internal set; } = "";

    public List<string> ColumnNames { get; } = new();

    public List<TableCellStyle> ColumnStyles { get; } = new();

    public List<TableCellStyle> RowStyles { get; } = new();

    public TableCellStyle? BaseCellStyle { get; internal set; }

    public ulong TableStyleHandle { get; internal set; }

    internal int NumFieldRefs = -1;

    internal readonly List<int> HsColCustomDataRefs = new();

    internal readonly List<int> HsRowCustomDataRefs = new();

    internal readonly List<int> HsRowCellCounts = new();

    public List<TableMergedCell> MergedCells { get; } = new();

    public abstract void ReadBody(BitReader br, DwgVersion version, DwgObjectRecord? record);
}
