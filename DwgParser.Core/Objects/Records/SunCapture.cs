namespace DwgParser.Core.Objects.Records;

public sealed class SunCapture : RecordIdentity, ISun
{
    public long ClassVersion = -1;

    public bool IsOn;

    public int ColorIndex = -1;

    public uint ColorRgb;

    public string ColorName = "";

    public string ColorBookName = "";

    public double Intensity = double.NaN;

    public bool HasShadow;

    public long JulianDay = -1;

    public long Msecs = -1;

    public bool IsDayLightSavings;

    public long ShadowType = -1;

    public long ShadowMapSize = -1;

    public int ShadowSoftness = -1;

    public bool StringStreamPresent;

    public bool ColorIsShort;

    public bool Captured;

    public long WalkEndBit;

    public string FailReason = "";

    long ISun.ClassVersion => ClassVersion;
    bool ISun.IsOn => IsOn;
    int ISun.ColorIndex => ColorIndex;
    uint ISun.ColorRgb => ColorRgb;
    string ISun.ColorName => ColorName;
    string ISun.ColorBookName => ColorBookName;
    double ISun.Intensity => Intensity;
    bool ISun.HasShadow => HasShadow;
    long ISun.JulianDay => JulianDay;
    long ISun.Msecs => Msecs;
    bool ISun.IsDayLightSavings => IsDayLightSavings;
    long ISun.ShadowType => ShadowType;
    long ISun.ShadowMapSize => ShadowMapSize;
    int ISun.ShadowSoftness => ShadowSoftness;
    bool ISun.StringStreamPresent => StringStreamPresent;
    bool ISun.Captured => Captured;
}
