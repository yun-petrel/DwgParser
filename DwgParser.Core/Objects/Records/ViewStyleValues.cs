namespace DwgParser.Core.Objects.Records;

public class ViewStyleValues
{
    public const int NoField = -1;

    public readonly record struct Color(short Index, uint Rgb, byte Flag, string Name, string BookName);

    public string Layout = "";

    public int MdocClassVersion;
    public string Description = "";
    public bool IsModifiedForRecompute;
    public int ViewstyleFlags = NoField;
    public string DisplayName = "";

    public int ClassVersion;
    public uint Flags;
    public Color IdentifierColor;
    public double IdentifierHeight;
    public string IdentifierExcludeCharacters = "";
    public Color ArrowSymbolColor;
    public double ArrowSymbolSize;
    public Color ViewlabelTextColor;
    public double ViewlabelTextHeight;
    public int ViewlabelAttachment;
    public double ViewlabelOffset;
    public int ViewlabelAlignment;
    public string ViewlabelPattern = "";
    public double IdentifierOffset;
}

public sealed class SectionViewStyleValues : ViewStyleValues
{
    public double ArrowSymbolExtensionLength;
    public int PlaneLinewt;
    public Color PlaneLineColor;
    public int BendLinewt;
    public Color BendLineColor;
    public double BendLineLength;
    public double EndLineLength;
    public Color HatchColor;
    public Color HatchBgColor;
    public string HatchPattern = "";
    public double HatchScale;
    public int HatchTransparency;
    public bool UnknownB1;
    public bool UnknownB2;
    public int IdentifierPosition;
    public int ArrowPosition;
    public double EndLineOvershoot;
    public int NumHatchAngles;
    public readonly List<double> HatchAngles = new();
}

public sealed class DetailViewStyleValues : ViewStyleValues
{
    public byte IdentifierPlacement;
    public int BoundaryLinewt;
    public Color BoundaryLineColor;
    public int ConnectionLinewt;
    public Color ConnectionLineColor;
    public int BorderlineLinewt;
    public Color BorderlineColor;
    public byte ModelEdge;
}
