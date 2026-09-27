namespace DwgParser.Core.Objects.Records;

public sealed class VportCapture : RecordIdentity, IVport
{
    public bool Captured;

    public bool ValuesCommitted;

    public long LightsAnchorBit;

    public long XrefStartBit;

    public int XrefResolved;

    public ulong VisualStyleHandle;


    public long DownXrefEndBit;

    public long DownLightsStartBit;

    public long DownLightsEndBit;

    public long DownGridTailBit;

    public string Name = "";

    public VportValues? Values;

    bool IVport.Captured => Captured;
    bool IVport.ValuesCommitted => ValuesCommitted;
    long IVport.LightsAnchorBit => LightsAnchorBit;
    long IVport.XrefStartBit => XrefStartBit;
    int IVport.XrefResolved => XrefResolved;
    ulong IVport.VisualStyleHandle => VisualStyleHandle;
    string IVport.Name => Name;
    VportValues? IVport.Values => Values;
}

public sealed class VportValues
{
    public bool HasR2007aFields;

    public double ViewSize;

    public double ViewWidth;

    public double AspectRatio;

    public Vec2 ViewCenter;

    public Vec3 ViewTarget;

    public Vec3 ViewDirection;

    public double Twist;

    public double LensLength;

    public double FrontClipZ;

    public double BackClipZ;

    public int ViewMode;

    public int RenderMode;

    public Vec2 LowerLeft;

    public Vec2 UpperRight;

    public bool UcsFollow;

    public int CircleZoom;

    public bool FastZoom;

    public int UcsIcon;

    public bool GridMode;

    public Vec2 GridUnit;

    public bool SnapMode;

    public bool SnapStyle;

    public int SnapIsoPair;

    public double SnapAngle;

    public Vec2 SnapBase;

    public Vec2 SnapUnit;

    public bool UcsAtOrigin;

    public bool UcsVp;

    public Vec3 UcsOrigin;

    public Vec3 UcsXDir;

    public Vec3 UcsYDir;

    public double UcsElevation;

    public int UcsOrthoView;


    public bool UseDefaultLights;

    public int DefaultLightingType;

    public double Brightness;

    public double Contrast;

    public int AmbientIndex;

    public uint AmbientRgb;

    public int AmbientFlag;

    public int GridFlags;

    public int GridMajor;
}
