using DwgParser.Core.IO;

namespace DwgParser.Core.Objects.Records;

public abstract class MLeaderStyleRecord : IMLeaderStyle
{
    public const short TypeCodeValue = 0x1FB;

    public abstract string Family { get; }

    protected readonly DwgObjectRecord Rec;

    protected readonly int ObjStart;

    protected MLeaderStyleRecord(ulong handle, DwgVersion sourceVersion,
        DwgObjectRecord record, int objStart)
    {
        HandleValue = handle;
        SrcVersion = sourceVersion;
        Rec = record;
        ObjStart = objStart;
        Cap = record.MLeader ??= new MLeaderStyleCapture();
        Cap.AttachIdentity(record);
    }

    private readonly ulong HandleValue;
    protected readonly DwgVersion SrcVersion;

    public MLeaderStyleCapture Cap { get; }

    public ulong Handle => HandleValue;
    public short TypeCode => TypeCodeValue;
    public DwgVersion SourceVersion => SrcVersion;

    public bool Captured => Cap.Captured;
    public bool DownCaptured => Cap.DownCaptured;
    public string Description => Cap.Captured ? Cap.Desc : Cap.DownDesc;
    public string TextDefault => Cap.Captured ? Cap.TextDefault : Cap.DownTextDefault;
    public MLeaderStyleCapture.MLeaderStyleValues? Values => Cap.Values;

    public abstract bool TryCapture(BitReader br);

    protected void Fail(BitReader br, string why)
    {
        Cap.FailReason = why;
        Cap.FailBit = br.BitPosition - Rec.CarryStartBit;
    }
}
