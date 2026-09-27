using DwgParser.Core.IO;

namespace DwgParser.Core.Objects.Records;

public abstract class ScaleRecord : IScale
{
    public const short TypeCodeValue = 506;

    protected readonly DwgObjectRecord Rec;
    protected readonly int ObjStart;
    protected readonly ulong Handle;

    protected ScaleRecord(ulong handle, DwgObjectRecord rec, int objStart)
    {
        Handle = handle;
        Rec = rec;
        ObjStart = objStart;
        Cap = rec.Scale ??= new ScaleCapture();
        Cap.AttachIdentity(rec);
    }

    public ScaleCapture Cap { get; }

    public abstract string Family { get; }

    ulong IRecord.Handle => Handle;
    public short TypeCode => TypeCodeValue;
    public DwgVersion SourceVersion => Rec.SourceVersion;
    public string Name { get; protected set; } = "";
    public int Flag { get; protected set; }
    public double PaperUnits { get; protected set; }
    public double DrawingUnits { get; protected set; }
    public bool IsUnitScale { get; protected set; }
    public bool Captured => Cap.Captured;

    internal virtual void Read(BitReader br)
    {
        if (!TryBeginFrame(br, out long strAreaStart, out long dataEnd, out BitReader? strBr))
            return;

        br.ReadBL();
        if (HasXdicMissing) br.ReadB();
        if (HasDsData) br.ReadB();

        int flag = br.ReadBS();
        long nameIns = br.BitPosition;
        if (strBr is null) return;
        string name = strBr.ReadTV();
        double paper = br.ReadBD();
        double drawing = br.ReadBD();
        bool unitScale = br.ReadB();
        long tailEnd = br.BitPosition;

        long expected = strAreaStart > 0 ? strAreaStart : dataEnd;
        if (br.BitPosition != expected) return;
        if (double.IsNaN(paper) || double.IsNaN(drawing)) return;

        Cap.DownNameInsertBit = nameIns;
        Cap.DownTailEndBit = tailEnd;
        Cap.Name = name;
        Cap.Flag = flag;
        Cap.PaperUnits = paper;
        Cap.DrawingUnits = drawing;
        Cap.IsUnitScale = unitScale;
        Cap.Captured = true;
        Name = name;
        Flag = flag;
        PaperUnits = paper;
        DrawingUnits = drawing;
        IsUnitScale = unitScale;
    }

    protected virtual bool TryBeginFrame(BitReader br, out long strAreaStart, out long dataEnd,
        out BitReader? strBr)
        => CentralFrame.TryRlFrame(Rec, ObjStart, br, out strAreaStart, out dataEnd, out strBr);

    protected virtual bool HasXdicMissing => true;

    protected virtual bool HasDsData => false;
}
