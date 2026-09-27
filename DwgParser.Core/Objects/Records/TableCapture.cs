using DwgParser.Core.IO;

namespace DwgParser.Core.Objects.Records;

public sealed class TableCapture
{
    public bool Captured;

    public double InsX, InsY, InsZ;
    public double NormalX, NormalY, NormalZ;
    public byte ScaleFlag;
    public double ScaleX = 1.0, ScaleY = 1.0, ScaleZ = 1.0;
    public double Rotation;
    public bool HasAttribs;

    public int Columns, Rows;

    public readonly List<double> ColWidths = new();
    public readonly List<double> RowHeights = new();

    public readonly List<TableEntityCell> Cells = new();

    public readonly List<TableMergedCell> MergedCells = new();

    public double HorizDirX = 1.0, HorizDirY, HorizDirZ;

    public double HorzCellMargin, VertCellMargin, DataRowHeight;

    public double BaseTextHeight;

    public bool TitleSuppressed;

    public readonly List<DwgHandleReference> TypeAxisRefs = new();
}
