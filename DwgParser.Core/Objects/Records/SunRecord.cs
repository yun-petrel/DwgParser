using DwgParser.Core.IO;

namespace DwgParser.Core.Objects.Records;

public abstract class SunRecord : ISun
{
    protected readonly DwgObjectRecord Rec;
    protected readonly int ObjStart;
    protected readonly ulong Handle;

    protected SunRecord(ulong handle, DwgObjectRecord rec, int objStart)
    {
        Handle = handle;
        Rec = rec;
        ObjStart = objStart;
        Cap = rec.Sun ??= new SunCapture();
        Cap.AttachIdentity(rec);
    }

    public SunCapture Cap { get; }

    public abstract string Family { get; }

    ulong IRecord.Handle => Handle;
    public short TypeCode => Rec.LogicalType;
    public DwgVersion SourceVersion => Rec.SourceVersion;
    public long ClassVersion { get; protected set; } = -1;
    public bool IsOn { get; protected set; }
    public int ColorIndex { get; protected set; } = -1;
    public uint ColorRgb { get; protected set; }
    public string ColorName { get; protected set; } = "";
    public string ColorBookName { get; protected set; } = "";
    public double Intensity { get; protected set; } = double.NaN;
    public bool HasShadow { get; protected set; }
    public long JulianDay { get; protected set; } = -1;
    public long Msecs { get; protected set; } = -1;
    public bool IsDayLightSavings { get; protected set; }
    public long ShadowType { get; protected set; } = -1;
    public long ShadowMapSize { get; protected set; } = -1;
    public int ShadowSoftness { get; protected set; } = -1;
    public bool StringStreamPresent { get; protected set; }
    public bool Captured => Cap.Captured;

    protected virtual bool HasInlineRl => true;
    protected virtual bool HasXdicMissing => true;
    protected virtual bool HasDsData => false;
    protected virtual bool HasTrailingStrings => false;

    protected virtual bool HasShortColor => false;

    internal virtual void Read(BitReader br)
    {
        long hs;
        if (HasInlineRl)
        {
            uint rl = br.ReadRL();
            hs = (long)ObjStart * 8 + rl;
        }
        else
        {
            hs = Rec.HandleStreamStartBit;
        }
        if (hs <= br.BitPosition || hs > (long)Rec.BodyEnd * 8) { Fail("帧界"); return; }

        br.ReadHandle();
        if (!DwgObjectReader.TrySkipEed(br)) { Fail("EED"); return; }
        if (br.ReadBL() is < 0 or > 100_000) { Fail("num_reactors"); return; }
        if (HasXdicMissing) br.ReadB();
        if (HasDsData) br.ReadB();

        long version = br.ReadBL();
        if (version is < 0 or > 10) { Fail("class_version 越界"); return; }
        bool isOn = br.ReadB();
        short colorIndex;
        uint rgb = 0; string cname = "", cbook = "";
        bool shortColor = HasShortColor;
        if (shortColor)
        {
            colorIndex = br.ReadBS();
        }
        else
        {
            colorIndex = DwgObjectReader.ReadCmc(br, out rgb, out cname, out cbook);
        }
        double intensity = br.ReadBD();
        if (double.IsNaN(intensity)) { Fail("intensity=NaN"); return; }
        bool hasShadow = br.ReadB();
        long julian = br.ReadBL();
        long msecs = br.ReadBL();
        bool dst = br.ReadB();
        long shadowType = br.ReadBL();
        if (shadowType is < 0 or > 2) { Fail("shadow_type 越界"); return; }
        long mapSize = br.ReadBS();
        if (mapSize is < 0 or > 3968) { Fail("shadow_mapsize 越界"); return; }
        int softness = br.ReadRC();

        bool strings = false;
        if (HasTrailingStrings) strings = br.ReadB();

        if (br.BitPosition != hs) { Fail($"停点 {br.BitPosition - hs}"); return; }

        Cap.ClassVersion = version;
        Cap.IsOn = isOn;
        Cap.ColorIndex = colorIndex;
        Cap.ColorRgb = rgb;
        Cap.ColorName = cname;
        Cap.ColorBookName = cbook;
        Cap.Intensity = intensity;
        Cap.HasShadow = hasShadow;
        Cap.JulianDay = julian;
        Cap.Msecs = msecs;
        Cap.IsDayLightSavings = dst;
        Cap.ShadowType = shadowType;
        Cap.ShadowMapSize = mapSize;
        Cap.ShadowSoftness = softness;
        Cap.ColorIsShort = shortColor;
        Cap.StringStreamPresent = strings;
        Cap.WalkEndBit = br.BitPosition;
        Cap.FailReason = "";
        Cap.Captured = true;
        CommitProjection(version, isOn, colorIndex, rgb, cname, cbook, intensity, hasShadow,
            julian, msecs, dst, shadowType, mapSize, softness, strings);
    }

    private void CommitProjection(long version, bool isOn, int colorIndex, uint rgb, string cname,
        string cbook, double intensity, bool hasShadow, long julian, long msecs, bool dst,
        long shadowType, long mapSize, int softness, bool strings)
    {
        ClassVersion = version;
        IsOn = isOn;
        ColorIndex = colorIndex;
        ColorRgb = rgb;
        ColorName = cname;
        ColorBookName = cbook;
        Intensity = intensity;
        HasShadow = hasShadow;
        JulianDay = julian;
        Msecs = msecs;
        IsDayLightSavings = dst;
        ShadowType = shadowType;
        ShadowMapSize = mapSize;
        ShadowSoftness = softness;
        StringStreamPresent = strings;
    }

    private void Fail(string why)
    {
        Cap.FailReason = why;
        Cap.WalkEndBit = -1;
    }
}
