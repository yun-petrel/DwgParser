namespace DwgParser.Core.Objects.Records;

public sealed class CellStyleMapCell
{
    public int Type;

    public int DataFlags;

    public int? PropertyOverrideFlags;

    public int? MergeFlags;

    public CmcTriple? BgColor;

    public int? ContentLayout;

    public CellStyleMapContentFormat? ContentFormat;

    public int? MarginOverrideFlags;

    public readonly double?[] Margins = new double?[6];

    public readonly List<CellStyleMapBorder> Borders = new();

    public int Id;

    public int EntryType;

    public string Name = "";
}

public readonly record struct CmcTriple(int Index, uint Rgb, byte Flag);

public sealed class CellStyleMapContentFormat
{
    public int PropertyOverrideFlags;

    public int PropertyFlags;

    public int ValueDataType;

    public int ValueUnitType;

    public string ValueFormatString = "";

    public double Rotation;

    public double BlockScale;

    public int CellAlignment;

    public CmcTriple ContentColor;

    public double TextHeight;
}

public sealed class CellStyleMapBorder
{
    public int IndexMask;

    public int BorderOverrides;

    public int BorderType;

    public CmcTriple Color;

    public int LineWeight;

    public int Visible;

    public double DoubleLineSpacing;
}
