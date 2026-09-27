using DwgParser.Core.IO;

namespace DwgParser.Core.Objects.Records;

public abstract class TableStyleRecord : ITableStyle
{
    public const short TypeCodeValue = 0x1F8;

    public abstract string Family { get; }

    protected readonly DwgObjectRecord Rec;

    protected readonly int ObjStart;

    protected TableStyleRecord(ulong handle, DwgVersion sourceVersion,
        DwgObjectRecord record, int objStart)
    {
        HandleValue = handle;
        SrcVersion = sourceVersion;
        Rec = record;
        ObjStart = objStart;
        Cap = record.TableStyle ??= new TableStyleCapture();
        Cap.AttachIdentity(record);
    }

    private readonly ulong HandleValue;
    protected readonly DwgVersion SrcVersion;

    public TableStyleCapture Cap { get; }

    public ulong Handle => HandleValue;
    public short TypeCode => TypeCodeValue;
    public DwgVersion SourceVersion => SrcVersion;

    public bool Captured => Cap.Captured;
    public string Name => Cap.Name;
    public double HorizMargin => Cap.HorizMargin;
    public double VertMargin => Cap.VertMargin;

    public abstract bool TryCapture(BitReader br);

    protected void Fail(BitReader br, string why)
    {
        Cap.FailReason = why;
        Cap.FailBit = br.BitPosition - Rec.CarryStartBit;
    }
}
