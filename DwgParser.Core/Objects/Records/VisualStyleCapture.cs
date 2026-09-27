namespace DwgParser.Core.Objects.Records;

public sealed class VisualStyleCapture
{
    public bool Captured;
    public string FailReason = "";
    public long FailBit = -1;
    public long BodyStartBit;
    public long StreamStartBit;
    public long[] FieldSpans = System.Array.Empty<long>();
    public int HaloGap;
    public double Brightness;
    public byte InternalOnly;

    public int[]? PreR2004CmcIndices;
    public long HsBit;
    public long EedStartBit;
    public int EedBitLen;

    public bool DownCaptured;
    public long DownBodyStartBit;
    public long DownStreamStartBit;
    public long DownFieldsEndBit;
    public long[] DownFieldSpans = System.Array.Empty<long>();
    public int[] DownScalars = System.Array.Empty<int>();
    public int DownHaloGap;
    public double DownBrightness;


    public bool ValuesCommitted;
    public string ValueAxisLayout = "";

    public int FaceLightingModel;
    public int FaceLightingQuality;
    public int FaceColorMode;
    public double FaceOpacity;
    public double FaceSpecular;
    public CmcTriple FaceMonoColor;
    public int FaceModifier;

    public int EdgeModel;
    public int EdgeStyle;
    public CmcTriple EdgeIntersectionColor;
    public CmcTriple EdgeObscuredColor;
    public int EdgeObscuredLinetype;
    public int EdgeIntersectionLinetype;
    public double EdgeCreaseAngle;
    public int EdgeModifier;
    public CmcTriple EdgeColor;
    public double EdgeOpacity;
    public int EdgeWidth;
    public int EdgeOverhang;
    public int EdgeJitter;
    public CmcTriple EdgeSilhouetteColor;
    public int EdgeSilhouetteWidth;
    public int EdgeIsolines;
    public bool EdgeDoHidePrecision;
    public int EdgeStyleApply;
    public bool HasEdgeStyleApply;

    public int DisplaySettings;
    public int DisplayShadowType;

    public double Bd2007Field45 = double.NaN;
    public int ExtLightingModel = -1;
    public int[] Ints176 = System.Array.Empty<int>();
}
