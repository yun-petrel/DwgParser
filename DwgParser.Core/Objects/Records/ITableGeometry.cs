namespace DwgParser.Core.Objects.Records;

public interface ITableGeometry : IRecord
{
    long NumRows { get; }

    long NumCols { get; }

    IReadOnlyList<TableGeometryCellValues> Cells { get; }

    bool StringStreamPresent { get; }

    bool Captured { get; }
}
