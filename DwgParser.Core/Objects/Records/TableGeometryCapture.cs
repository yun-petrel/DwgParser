namespace DwgParser.Core.Objects.Records;

public sealed class TableGeometryCapture : RecordIdentity, ITableGeometry
{
    public TableGeometryValues Values { get; } = new();

    public bool Captured;

    public long WalkEndBit;

    public string FailReason = "";


    public long FailBit = -1;
    long ITableGeometry.NumRows => Values.NumRows;
    long ITableGeometry.NumCols => Values.NumCols;
    IReadOnlyList<TableGeometryCellValues> ITableGeometry.Cells => Values.Cells;
    bool ITableGeometry.StringStreamPresent => Values.HasStrings == 1;
    bool ITableGeometry.Captured => Captured;
}
