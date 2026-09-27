namespace DwgParser.Core.Objects.Records;

internal sealed class VisualStyleRegion
{
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

    public readonly List<int> Ints176 = new(28);

    public void CommitTo(VisualStyleCapture cap, string layout)
    {
        cap.FaceLightingModel = FaceLightingModel;
        cap.FaceLightingQuality = FaceLightingQuality;
        cap.FaceColorMode = FaceColorMode;
        cap.FaceOpacity = FaceOpacity;
        cap.FaceSpecular = FaceSpecular;
        cap.FaceMonoColor = FaceMonoColor;
        cap.FaceModifier = FaceModifier;
        cap.EdgeModel = EdgeModel;
        cap.EdgeStyle = EdgeStyle;
        cap.EdgeIntersectionColor = EdgeIntersectionColor;
        cap.EdgeObscuredColor = EdgeObscuredColor;
        cap.EdgeObscuredLinetype = EdgeObscuredLinetype;
        cap.EdgeIntersectionLinetype = EdgeIntersectionLinetype;
        cap.EdgeCreaseAngle = EdgeCreaseAngle;
        cap.EdgeModifier = EdgeModifier;
        cap.EdgeColor = EdgeColor;
        cap.EdgeOpacity = EdgeOpacity;
        cap.EdgeWidth = EdgeWidth;
        cap.EdgeOverhang = EdgeOverhang;
        cap.EdgeJitter = EdgeJitter;
        cap.EdgeSilhouetteColor = EdgeSilhouetteColor;
        cap.EdgeSilhouetteWidth = EdgeSilhouetteWidth;
        cap.EdgeIsolines = EdgeIsolines;
        cap.EdgeDoHidePrecision = EdgeDoHidePrecision;
        cap.EdgeStyleApply = EdgeStyleApply;
        cap.HasEdgeStyleApply = HasEdgeStyleApply;
        cap.DisplaySettings = DisplaySettings;
        cap.DisplayShadowType = DisplayShadowType;
        cap.Bd2007Field45 = Bd2007Field45;
        cap.ExtLightingModel = ExtLightingModel;
        cap.Ints176 = Ints176.Count == 0 ? System.Array.Empty<int>() : Ints176.ToArray();
        cap.ValueAxisLayout = layout;
        cap.ValuesCommitted = true;
    }
}
