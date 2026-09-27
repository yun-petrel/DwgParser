using DwgParser.Core.IO;

namespace DwgParser.Core.Objects.Records;

public abstract class AppidRecord : IAppid
{
    public const short TypeCodeValue = 0x43;

    protected readonly DwgObjectRecord Rec;
    protected readonly int ObjStart;
    protected readonly ulong Handle;

    protected AppidRecord(ulong handle, DwgObjectRecord rec, int objStart)
    {
        Handle = handle;
        Rec = rec;
        ObjStart = objStart;
        Cap = rec.Appid ??= new AppidCapture();
        Cap.AttachIdentity(rec);
    }

    public AppidCapture Cap { get; }

    public abstract string Family { get; }

    ulong IRecord.Handle => Handle;
    public short TypeCode => TypeCodeValue;
    public DwgVersion SourceVersion => Rec.SourceVersion;
    public string Name { get; protected set; } = "";
    public int XrefResolved { get; protected set; }
    public int Unknown71 { get; protected set; } = -1;
    public bool HasUnknown71 { get; protected set; }
    public bool Captured => Cap.Captured;

    internal virtual void Read(BitReader br)
    {
        if (!TryBeginFrame(br, out long strAreaStart, out long dataEnd, out BitReader? strBr))
            return;

        br.ReadBL();
        if (HasXdicMissing) br.ReadB();
        if (HasDsData) br.ReadB();

        int xref = br.ReadBS();
        long afterXref = br.BitPosition;
        int unk = br.ReadRC();
        long afterUnk = br.BitPosition;

        long expected = strAreaStart > 0 ? strAreaStart : dataEnd;
        bool hasUnk = afterUnk == expected;
        bool noUnk = afterXref == expected;
        if (!hasUnk && !noUnk) return;
        if (hasUnk && noUnk) return;
        if (xref is not (0 or 256)) return;
        if (strBr is null) return;
        string name = strBr.ReadTV();

        Cap.Name = name;
        Cap.XrefResolved = xref;
        Cap.HasUnknown71 = hasUnk;
        Cap.Unknown71 = hasUnk ? unk : -1;
        Cap.Captured = true;
        Name = name;
        XrefResolved = xref;
        HasUnknown71 = hasUnk;
        Unknown71 = hasUnk ? unk : -1;
    }

    protected virtual bool TryBeginFrame(BitReader br, out long strAreaStart, out long dataEnd,
        out BitReader? strBr)
        => CentralFrame.TryRlFrame(Rec, ObjStart, br, out strAreaStart, out dataEnd, out strBr);

    protected virtual bool HasXdicMissing => true;

    protected virtual bool HasDsData => false;
}
