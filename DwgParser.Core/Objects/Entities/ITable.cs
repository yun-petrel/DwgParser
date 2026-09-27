namespace DwgParser.Core.Objects;

public interface ITable : IEntity
{
    int Columns { get; }

    int Rows { get; }

    short TableFlag { get; }

    Vec2? HorizDirection { get; }

    System.Collections.Generic.List<double> ColWidths { get; }

    System.Collections.Generic.List<double> RowHeights { get; }

    System.Collections.Generic.List<TableEntityCell> Cells { get; }

    TableStyleOverride? StyleOverride { get; }

    System.Collections.Generic.List<TableBorderOverride> BorderOverrides { get; }

    ulong TableStyleHandle { get; }


    string TableName { get; }

    string Description { get; }

    System.Collections.Generic.List<string> ColumnNames { get; }

    System.Collections.Generic.List<TableCellStyle> ColumnStyles { get; }

    System.Collections.Generic.List<TableCellStyle> RowStyles { get; }

    TableCellStyle? BaseCellStyle { get; }

    System.Collections.Generic.List<TableMergedCell> MergedCells { get; }

    int? NumOwned { get; }
}
