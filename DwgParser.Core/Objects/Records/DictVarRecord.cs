using DwgParser.Core.Format;
using DwgParser.Core.IO;

namespace DwgParser.Core.Objects.Records;

public abstract class DictVarRecord : IDictVar
{
    public const short TypeCodeValue = 0x1F7;

    protected readonly DwgObjectRecord Rec;
    protected readonly int ObjStart;
    protected readonly ulong Handle;

    protected DictVarRecord(ulong handle, DwgObjectRecord rec, int objStart)
    {
        Handle = handle;
        Rec = rec;
        ObjStart = objStart;
        Cap = rec.DictVar ??= new DictVarCapture();
        Cap.AttachIdentity(rec);
    }

    public DictVarCapture Cap { get; }

    public abstract string Family { get; }

    ulong IRecord.Handle => Handle;
    public short TypeCode => TypeCodeValue;
    public DwgVersion SourceVersion => Rec.SourceVersion;
    public string StrValue { get; protected set; } = "";
    public bool Captured => Cap.Captured;

    internal virtual void Read(BitReader br)
    {
        uint bitsize = br.ReadRL();
        long hs = (long)ObjStart * 8 + bitsize;
        if (hs <= br.BitPosition || hs > (long)Rec.BodyEnd * 8) return;
        Rec.HandleStreamStartBit = hs;
        Rec.HasHandleStream = true;
        br.ReadHandle();
        Rec.EedStartBit = br.BitPosition;
        if (!DwgObjectReader.TrySkipEed(br)) return;
        Rec.EedBitLen = (int)(br.BitPosition - Rec.EedStartBit);
        br.ReadBL();
        if (HasXdicMissing) br.ReadB();
        Schema = br.ReadRC();

        long strStart = br.BitPosition;
        string strvalue = br.ReadTV();
        long strEnd = br.BitPosition;

        bool dbg = System.Environment.GetEnvironmentVariable("DWGDBG") == "1";
        if (dbg) System.Console.Error.WriteLine(
            $"DVARREC h={Rec.Handle:X} t={Rec.TypeCode} hs={hs} str='{strvalue}'[{strStart}..{strEnd}]");

        if (strEnd != hs) return;

        Rec.StringFields.Add(new DwgObjectRecord.StringField(strStart, (int)(strEnd - strStart), strvalue));
        Cap.StrValue = strvalue;
        Cap.StrStartBit = strStart;
        Cap.StrBitLen = (int)(strEnd - strStart);
        Cap.Schema = Schema;
        Cap.EedStartBit = Rec.EedStartBit;
        Cap.EedBitLen = Rec.EedBitLen;
        Cap.Captured = true;
        StrValue = strvalue;
    }

    protected virtual bool HasXdicMissing => true;

    public int Schema { get; protected set; }
}
