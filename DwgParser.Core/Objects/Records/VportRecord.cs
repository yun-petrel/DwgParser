using DwgParser.Core.IO;

namespace DwgParser.Core.Objects.Records;

public abstract class VportRecord : IVport
{
    public const short TypeCodeValue = 0x41;

    public abstract string Family { get; }

    protected readonly DwgObjectRecord Rec;

    protected readonly int ObjStart;

    protected VportRecord(ulong handle, DwgVersion sourceVersion,
        DwgObjectRecord record, int objStart)
    {
        HandleValue = handle;
        SrcVersion = sourceVersion;
        Rec = record;
        ObjStart = objStart;
        Cap = record.Vport ??= new VportCapture();
        Cap.AttachIdentity(record);
    }

    private readonly ulong HandleValue;
    protected readonly DwgVersion SrcVersion;

    public VportCapture Cap { get; }

    public ulong Handle => HandleValue;
    public short TypeCode => TypeCodeValue;
    public DwgVersion SourceVersion => SrcVersion;

    public bool Captured => Cap.Captured;
    public bool ValuesCommitted => Cap.ValuesCommitted;
    public long LightsAnchorBit => Cap.LightsAnchorBit;
    public long XrefStartBit => Cap.XrefStartBit;
    public int XrefResolved => Cap.XrefResolved;
    public ulong VisualStyleHandle => Cap.VisualStyleHandle;
    public string Name => Cap.Name;
    public VportValues? Values => Cap.Values;

    public virtual bool TryCapture(BitReader br)
    {
        if (Rec.TypeCode != TypeCodeValue) return false;
        var head = new FrameHead();
        if (!TryBeginFrame(br, head)) return false;
        long ownStart = head.OwnStart;
        long hs = head.DataEnd;

        br.ReadBL();
        if (HasXdicMissing)
            br.ReadB();
        if (HasDsData)
            br.ReadB();

        string name;
        if (NameInline)
        {
            name = br.ReadTV();
        }
        else
        {
            if (head.Strings is null) return false;
            if (!CentralStrings.ReadAll(head.Strings, head.StringsEnd, 2, out var strings)) return false;
            if (strings.Count != 1 || strings[0].Length == 0) return false;
            name = strings[0];
        }
        long xrefStart = br.BitPosition;
        int xrefResolved;
        if (XrefTriple)
        {
            br.ReadB();
            xrefResolved = br.ReadBS();
            br.ReadB();
        }
        else
        {
            xrefResolved = br.ReadBS();
        }

        long xrefEnd = br.BitPosition;
        var v = new VportValues();
        bool Bad = false;
        double BD()
        {
            double d = br.ReadBD();
            if (double.IsNaN(d)) Bad = true;
            return d;
        }
        double RD()
        {
            double d = br.ReadRD();
            if (double.IsNaN(d)) Bad = true;
            return d;
        }
        Vec2 RD2()
        {
            double x = RD(), y = 0;
            if (!Bad) y = RD();
            return new Vec2(x, y);
        }
        Vec3 BD3()
        {
            double x = BD(), y = 0, z = 0;
            if (!Bad) { y = BD(); if (!Bad) z = BD(); }
            return new Vec3(x, y, z);
        }

        v.ViewSize = BD();
        v.ViewWidth = BD();
        v.AspectRatio = v.ViewSize == 0 ? 0 : v.ViewWidth / v.ViewSize;
        v.ViewCenter = RD2();
        v.ViewTarget = BD3();
        v.ViewDirection = BD3();
        v.Twist = BD();
        v.LensLength = BD();
        v.FrontClipZ = BD();
        v.BackClipZ = BD();
        v.ViewMode = (int)br.ReadBits(4);
        v.RenderMode = br.ReadRC();
        long anchor = br.BitPosition;

        if (HasLightsBlock)
        {
            v.UseDefaultLights = br.ReadB();
            v.DefaultLightingType = br.ReadRC();
            v.Brightness = BD();
            v.Contrast = BD();
            v.AmbientIndex = br.ReadBS();
            v.AmbientRgb = (uint)br.ReadBL();
            int cmcFlag = br.ReadRC();
            if (cmcFlag != 0) Bad = true;
            v.AmbientFlag = cmcFlag;
            v.HasR2007aFields = true;
        }
        long lightsEnd = br.BitPosition;

        v.LowerLeft = RD2();
        v.UpperRight = RD2();
        v.UcsFollow = br.ReadB();
        v.CircleZoom = br.ReadBS();
        v.FastZoom = br.ReadB();
        v.UcsIcon = br.ReadBB();
        v.GridMode = br.ReadB();
        v.GridUnit = RD2();
        v.SnapMode = br.ReadB();
        v.SnapStyle = br.ReadB();
        v.SnapIsoPair = br.ReadBS();
        v.SnapAngle = BD();
        v.SnapBase = RD2();
        v.SnapUnit = RD2();
        v.UcsAtOrigin = br.ReadB();
        v.UcsVp = br.ReadB();
        v.UcsOrigin = BD3();
        v.UcsXDir = BD3();
        v.UcsYDir = BD3();
        v.UcsElevation = BD();
        v.UcsOrthoView = br.ReadBS();

        long gridTailStart = br.BitPosition;
        if (HasGridTail)
        {
            v.GridFlags = br.ReadBS();
            v.GridMajor = br.ReadBS();
        }

        long closure = head.NumericEnd;
        bool dbg = System.Environment.GetEnvironmentVariable("DWGDBG") == "1";
        if (dbg) System.Console.Error.WriteLine(
            $"VP2007A h={Rec.Handle:X} t={Rec.TypeCode} hs={hs} closure={closure} anchor={anchor} " +
            $"end={br.BitPosition} Δ={(int)(br.BitPosition - closure)} bad={Bad}");
        if (Bad || ownStart == 0 || anchor <= ownStart || anchor >= closure
            || br.BitPosition != closure || xrefResolved is not (0 or 256)) return false;
        if (ValuesOnly)
        {
            Cap.ValuesCommitted = true;
            Cap.DownXrefEndBit = xrefEnd;
            Cap.DownLightsStartBit = anchor;
            Cap.DownLightsEndBit = lightsEnd;
            Cap.DownGridTailBit = gridTailStart;
        }
        else
        {
            Cap.Captured = true;
            Cap.LightsAnchorBit = anchor;
            Cap.XrefStartBit = xrefStart;
        }
        Cap.XrefResolved = xrefResolved;
        Cap.Name = name;
        Cap.Values = v;
        return true;
    }

    protected virtual bool TryBeginFrame(BitReader br, FrameHead head)
    {
        DwgObjectRecord.SeekTo(br, Rec.CarryStartBit);
        uint bitsize = br.ReadRL();
        head.DataEnd = (long)ObjStart * 8 + bitsize;
        head.NumericEnd = head.DataEnd;
        if (head.DataEnd <= br.BitPosition || head.DataEnd > (long)Rec.BodyEnd * 8) return false;
        head.OwnStart = br.BitPosition;
        br.ReadHandle();
        return DwgObjectReader.TrySkipEed(br);
    }

    protected sealed class FrameHead
    {
        public long OwnStart;
        public long DataEnd;
        public long NumericEnd;
        public BitReader? Strings;
        public long StringsEnd;
    }

    protected virtual bool HasXdicMissing => true;

    protected virtual bool HasDsData => false;

    protected virtual bool NameInline => true;

    protected virtual bool XrefTriple => true;

    protected virtual bool HasLightsBlock => false;

    protected virtual bool HasGridTail => false;

    protected virtual bool ValuesOnly => false;
}
