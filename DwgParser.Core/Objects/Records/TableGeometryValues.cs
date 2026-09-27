namespace DwgParser.Core.Objects.Records;

public sealed class TableGeometryValues
{
    public const int NoField = -1;

    public string Layout = "";

    public int NumRows = NoField;

    public int NumCols = NoField;

    public int NumCells = NoField;

    public readonly List<TableGeometryCellValues> Cells = new();

    public int HasStrings = NoField;
}

public sealed class TableGeometryCellValues
{
    public int GeomDataFlag;

    public double WidthWGap;

    public double HeightWGap;

    public int NumGeometry;

    public readonly List<TableGeometryGeomValues> Geometry = new();
}

public sealed class TableGeometryGeomValues
{
    public double TopLeftX, TopLeftY, TopLeftZ;

    public double CenterX, CenterY, CenterZ;

    public double ContentWidth;

    public double ContentHeight;

    public double Width;

    public double Height;

    public int Unknown;
}
