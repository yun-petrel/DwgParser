namespace DwgParser.Core.Objects;

public enum SynthEntityKind
{
    None = 0,
    MText = 1,
    LwPolyline = 2,
}

public sealed class SyntheticMText
{
    public double InsX, InsY, InsZ;
    public double ExtrusionX, ExtrusionY, ExtrusionZ;
    public double XAxisX, XAxisY, XAxisZ;
    public double RectWidth;
    public double TextHeight;
    public short Attachment;
    public short FlowDir;
    public double ExtentsHeight;
    public double ExtentsWidth;
    public string Text = "";
    public System.Text.Encoding? LegacyEncoding;
    public short LineSpaceStyle = 1;
    public double LineSpaceFactor = 1.0;
    public bool LineSpaceUnknownBit;
    public long BgFillFlag;
}

public sealed class SyntheticPolyline
{
    public bool Closed = true;
    public bool PlineGen;
    public double[] X = Array.Empty<double>();
    public double[] Y = Array.Empty<double>();
    public bool HasElevation;
    public double Elevation;
}

public sealed class SyntheticFrame
{
    public SynthEntityKind Kind = SynthEntityKind.None;
    public SyntheticMText? MText;
    public SyntheticPolyline? Polyline;
}
