using DwgParser.Core.IO;

namespace DwgParser.Core.Objects.Records;

public abstract class VisualStyleRecord : IVisualStyle
{
    public const short TypeCodeValue = 509;

    public abstract string Family { get; }

    protected readonly DwgObjectRecord Rec;

    protected readonly int ObjStart;

    protected VisualStyleRecord(ulong handle, DwgVersion sourceVersion,
        DwgObjectRecord record, int objStart)
    {
        HandleValue = handle;
        SrcVersion = sourceVersion;
        Rec = record;
        ObjStart = objStart;
        Cap = record.VisualStyle ??= new VisualStyleCapture();
    }

    private readonly ulong HandleValue;
    protected readonly DwgVersion SrcVersion;

    public VisualStyleCapture Cap { get; }

    public ulong Handle => HandleValue;
    public short TypeCode => TypeCodeValue;
    public DwgVersion SourceVersion => SrcVersion;

    public bool Captured { get; protected set; }
    public int StyleType { get; protected set; }
    public string Description { get; protected set; } = "";
    public int EdgeHaloGap { get; protected set; }
    public double DisplayBrightness { get; protected set; }
    public bool InternalOnly { get; protected set; }


    public virtual long BodyStartBit => Cap.BodyStartBit;

    public virtual long StreamStartBit => Cap.StreamStartBit;

    public virtual long[] FieldSpans => Cap.FieldSpans;

    public virtual int[] Scalars => Cap.DownScalars;

    public abstract bool TryCapture(BitReader br);

    protected void Fail(BitReader br, string why)
    {
        Cap.FailReason = why;
        Cap.FailBit = br.BitPosition - Rec.CarryStartBit;
    }
}
