using DwgParser.Core.IO;
using DwgParser.Core.Render;

namespace DwgParser.Core.Objects.Records;

public abstract partial class LayerRecord : ILayer
{
    public const short TypeCodeValue = 0x33;

    protected readonly DwgObjectRecord Rec;

    protected readonly DwgObjectReader.WalkResult Result;

    protected readonly int ObjStart;

    protected readonly ulong Handle;

    protected uint Bitsize;

    private protected BitReader? _cursor;

    protected LayerRecord(ulong handle, DwgObjectRecord rec,
        DwgObjectReader.WalkResult result, int objStart)
    {
        Handle = handle;
        Rec = rec;
        Result = result;
        ObjStart = objStart;
        Cap = rec.Layer ??= new LayerCapture();
        Cap.BeginFrame(rec);
    }

    public LayerCapture Cap { get; }

    public abstract string Family { get; }

    ulong IRecord.Handle => Handle;
    public short TypeCode => TypeCodeValue;
    public DwgVersion SourceVersion => Rec.SourceVersion;
    public string Name => Cap.Name;
    public int ColorIndex => Cap.ColorIndex;
    public uint TrueColor => Cap.TrueColor;
    public string ColorName => Cap.ColorName;
    public string ColorBookName => Cap.ColorBookName;
    public short? LayerColor => Cap.LayerColor;
    public bool Captured => !string.IsNullOrEmpty(Cap.Name);
    public bool Frozen => Cap.Frozen;
    public bool Off => Cap.Off;
    public bool FrozenInNew => Cap.FrozenInNew;
    public bool Locked => Cap.Locked;
    public bool? PlotFlag => Cap.PlotFlag;
    public int? LineWeightRaw => Cap.LineWeightRaw;
    public int Dxf70 => Cap.Dxf70;
    public bool XrefRef => Cap.XrefRef;
    public int XrefResolved => Cap.XrefResolved;
    public bool XrefDep => Cap.XrefDep;

    internal virtual bool Read(BitReader br)
    {
        _cursor = br;
        try
        {
            if (!ReadFrameHead(br)) return false;

            br.ReadBL();
            if (HasXdicMissing) br.ReadB();
            if (HasDsData) br.ReadB();

            long nameStart = br.BitPosition;
            string name = br.ReadTV();
            Rec.CaptureString(nameStart, br.BitPosition, name);

            ReadXref(br);
            ReadFlags(br);
            Cap.CommitDxf70();

            long colorStart = br.BitPosition;
            short colorIndex = ReadColor(br, out uint trueColor,
                out string colorName, out string colorBookName);
            Cap.CommitColorSpan(colorStart, br.BitPosition);
            Cap.CommitColor(colorIndex, trueColor, colorName, colorBookName);
            bool named = CommitNameAndPublish(name);
            CommitLayerColorIfLanded(named, colorIndex, trueColor);
            if (named) ReadObjectHandles(br);
            return named;
        }
        catch { return false; }
    }

    protected virtual bool ReadFrameHead(BitReader br)
    {
        Bitsize = br.ReadRL();
        br.ReadHandle();
        if (!DwgObjectReader.TrySkipEed(br)) return false;
        Rec.HandleStreamStartBit = (long)ObjStart * 8 + Bitsize;
        Rec.HasHandleStream = true;
        if (AttachCentralStringStream)
            DwgObjectReader.AttachStringStream(br, ObjStart, Bitsize, Rec.SourceVersion, Rec);
        return true;
    }

    protected virtual bool AttachCentralStringStream => false;

    private void ReadObjectHandles(BitReader br)
    {
        long endBit = (long)Rec.BodyEnd * 8;
        long hss = Rec.HandleStreamStartBit;
        if (hss <= 0 || endBit <= hss) return;
        long saved = br.BitPosition;
        DwgObjectRecord.SeekTo(br, hss);
        try
        {
            for (int k = 0; k < 8 && br.BitPosition < endBit; k++)
            {
                DwgHandleReference h;
                try { h = br.ReadHandle(); }
                catch { return; }
                if (br.BitPosition > endBit) return;
                Cap.CommitObjectHandle(h.Resolve(Handle));
            }
        }
        finally { br.SetPosition((int)(saved / 8), (int)(saved % 8)); }
    }

    protected virtual bool HasXdicMissing => false;

    protected virtual bool HasDsData => false;

    protected virtual void ReadXref(BitReader br)
        => Cap.CommitXref(br.ReadB(), br.ReadBS(), br.ReadB());

    protected void ReadXrefSingleBs(BitReader br)
    {
        int resolved = br.ReadBS();
        Cap.CommitXref(true, resolved, resolved == 256);
    }

    protected virtual void ReadFlags(BitReader br)
    {
        ushort flag0 = (ushort)br.ReadBS();
        Cap.CommitFlags((flag0 & 1) != 0, (flag0 & 2) != 0, (flag0 & 4) != 0, (flag0 & 8) != 0);
        Cap.CommitPlotAndLineWeight((flag0 & 16) != 0, (flag0 & 0x3E0) >> 5);
    }

    protected void ApplyBits(bool frozen, bool off, bool frozenInNew, bool locked)
        => Cap.CommitFlags(frozen, off, frozenInNew, locked);

    protected virtual short ReadColor(BitReader br, out uint trueColor,
        out string colorName, out string colorBookName)
    {
        trueColor = 0;
        colorName = "";
        colorBookName = "";
        return br.ReadBS();
    }

    protected virtual bool UsesCmcColor => false;
}
