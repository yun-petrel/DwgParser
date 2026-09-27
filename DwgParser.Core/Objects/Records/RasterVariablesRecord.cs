using DwgParser.Core.IO;

namespace DwgParser.Core.Objects.Records;

public abstract class RasterVariablesRecord : IRasterVariables
{
    protected readonly DwgObjectRecord Rec;
    protected readonly int ObjStart;
    protected readonly ulong Handle;

    protected RasterVariablesRecord(ulong handle, DwgObjectRecord rec, int objStart)
    {
        Handle = handle;
        Rec = rec;
        ObjStart = objStart;
        Cap = rec.RasterVariables ??= new RasterVariablesCapture();
        Cap.AttachIdentity(rec);
    }

    public RasterVariablesCapture Cap { get; }

    public abstract string Family { get; }

    ulong IRecord.Handle => Handle;
    public short TypeCode => Rec.LogicalType;
    public DwgVersion SourceVersion => Rec.SourceVersion;
    public long ClassVersion { get; protected set; } = -1;
    public long ImageFrame { get; protected set; } = -1;
    public long ImageQuality { get; protected set; } = -1;
    public long Units { get; protected set; } = -1;
    public bool StringStreamPresent { get; protected set; }
    public bool Captured => Cap.Captured;

    protected virtual bool HasInlineRl => true;
    protected virtual bool HasXdicMissing => false;
    protected virtual bool HasDsData => false;
    protected virtual bool HasTrailingStrings => false;

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
        long frame = br.ReadBS();
        long quality = br.ReadBS();
        long units = br.ReadBS();

        bool strings = false;
        if (HasTrailingStrings) strings = br.ReadB();

        if (br.BitPosition != hs) { Fail($"停点 {br.BitPosition - hs}"); return; }

        Cap.ClassVersion = version;
        Cap.ImageFrame = frame;
        Cap.ImageQuality = quality;
        Cap.Units = units;
        Cap.StringStreamPresent = strings;
        Cap.WalkEndBit = br.BitPosition;
        Cap.FailReason = "";
        Cap.Captured = true;
        ClassVersion = version;
        ImageFrame = frame;
        ImageQuality = quality;
        Units = units;
        StringStreamPresent = strings;
    }

    private void Fail(string why)
    {
        Cap.FailReason = why;
        Cap.WalkEndBit = -1;
    }
}
