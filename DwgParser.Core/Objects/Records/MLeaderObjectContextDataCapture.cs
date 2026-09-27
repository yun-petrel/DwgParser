namespace DwgParser.Core.Objects.Records;

public sealed class MLeaderObjectContextDataCapture
{
    public bool Captured;

    public bool EntriesCaptured;

    public long SourceDataBits;

    public int ClassVersion = -1;

    public bool IsDefault;

    public IReadOnlyList<MlCtxRootCapture> Roots = Array.Empty<MlCtxRootCapture>();

    public double ContextScale;
    public MlCtxVec ContentBase = MlCtxVec.Zero;
    public double TextHeight;
    public double ArrowSize;
    public double LandingGap;
    public int TextLeft = -1;
    public int TextRight = -1;
    public int TextAngleType = -1;
    public int TextAlignment = -1;

    public bool HasTextContent;

    public string TextLabel = "";
    public MlCtxVec TextNormal = MlCtxVec.Zero;
    public MlCtxVec TextLocation = MlCtxVec.Zero;
    public MlCtxVec TextDirection = MlCtxVec.Zero;
    public double TextRotation;
    public double TextBoundaryWidth;
    public double TextBoundaryHeight;
    public double LineSpacingFactor;
    public int LineSpacingStyle = -1;
    public MlCtxColor TextColor = MlCtxColor.Empty;
    public int TextAlignmentPoint = -1;
    public int FlowDirection = -1;
    public MlCtxColor BackgroundColor = MlCtxColor.Empty;
    public double BackgroundScale;
    public long BackgroundTransparency;
    public bool BackgroundFillEnabled;
    public bool BackgroundMaskFillOn;
    public int ColumnType = -1;
    public bool TextHeightAutomatic;
    public double ColumnWidth;
    public double ColumnGutter;
    public bool ColumnFlowReversed;
    public IReadOnlyList<double> ColumnSizes = Array.Empty<double>();
    public bool WordBreak;
    public bool UnknownTailBit;

    public bool HasBlockContent;
    public MlCtxVec BlockNormal = MlCtxVec.Zero;
    public MlCtxVec BlockLocation = MlCtxVec.Zero;
    public MlCtxVec BlockScale = MlCtxVec.Zero;
    public double BlockRotation;
    public MlCtxColor BlockColor = MlCtxColor.Empty;
    public IReadOnlyList<double> BlockTransform = Array.Empty<double>();

    public MlCtxVec BasePoint = MlCtxVec.Zero;
    public MlCtxVec BaseDirection = MlCtxVec.Zero;
    public MlCtxVec BaseVertical = MlCtxVec.Zero;
    public bool NormalReversed;
    public int TextTopAttachment = -1;
    public int TextBottomAttachment = -1;

    public ulong OwnerHandle;
    public long NumReactors = -1;
    public IReadOnlyList<ulong> ReactorHandles = Array.Empty<ulong>();
    public bool XdicMissing;
    public ulong XdicHandle;
    public ulong ScaleHandle;
    public ulong TextStyleHandle;
    public ulong BlockRecordHandle;

    public long HandleStreamPadBits = -1;
    public long StopBit = -1;
    public long WalkEndBit;
    public string FailReason = "";
    public long FailBit;
    public string EntriesFailReason = "";
}

public sealed class MlCtxRootCapture
{
    public bool HasLastLeaderLinePoint;
    public MlCtxVec LastLeaderLinePoint = MlCtxVec.Zero;
    public bool HasDogleg;
    public MlCtxVec DoglegVector = MlCtxVec.Zero;
    public IReadOnlyList<MlCtxBreakCapture> Breaks = Array.Empty<MlCtxBreakCapture>();
    public long BranchIndex;
    public double DoglegLength;
    public IReadOnlyList<MlCtxLineCapture> Lines = Array.Empty<MlCtxLineCapture>();
    public int AttachDir = -1;
}

public sealed class MlCtxLineCapture
{
    public IReadOnlyList<MlCtxVec> Points = Array.Empty<MlCtxVec>();
    public IReadOnlyList<MlCtxBreakCapture> Breaks = Array.Empty<MlCtxBreakCapture>();
    public long LineIndex;
    public int Type = -1;
    public MlCtxColor Color = MlCtxColor.Empty;
    public long LineWeight;
    public double ArrowSize;
    public long Flags;
    public ulong LTypeHandle;
    public ulong ArrowHandle;
}

public sealed record MlCtxBreakCapture(MlCtxVec Start, MlCtxVec End);

public sealed record MlCtxVec(double X, double Y, double Z)
{
    public static readonly MlCtxVec Zero = new(0, 0, 0);
}

public sealed record MlCtxColor(short Index, uint Rgb, string Name, string Book)
{
    public static readonly MlCtxColor Empty = new(0, 0, "", "");
}
