using DwgParser.Core.IO;

namespace DwgParser.Core.Objects.Records;

public abstract class LtypeRecord : ILtype
{
    public const short TypeCodeValue = 0x39;

    protected readonly DwgObjectRecord Rec;

    protected readonly int ObjStart;

    protected readonly ulong Handle;

    protected LtypeRecord(ulong handle, DwgObjectRecord rec, int objStart)
    {
        Handle = handle;
        Rec = rec;
        ObjStart = objStart;
        Cap = rec.LType ??= new LtypeCapture();
    }

    public LtypeCapture Cap { get; }

    public abstract string Family { get; }

    ulong IRecord.Handle => Handle;
    public short TypeCode => TypeCodeValue;
    public DwgVersion SourceVersion => Rec.SourceVersion;
    public string Name { get; protected set; } = "";
    public string Description { get; protected set; } = "";
    public int XrefResolved { get; protected set; }
    public int NumDashes { get; protected set; }
    public bool Captured => Cap.HasAnchors;

    internal virtual void Read(BitReader br)
    {
        if (!ReadFrameHead(br)) return;
        br.ReadBL();
        if (HasXdicMissing) br.ReadB();
        if (HasDsData) br.ReadB();

        long nameStart = br.BitPosition;
        string name = br.ReadTV();
        if (string.IsNullOrEmpty(name)) return;
        int resolved = ReadXref(br);
        long descStart = br.BitPosition;
        string desc = br.ReadTV();
        long patStart = br.BitPosition;
        PatternLen = br.ReadBD();
        Alignment = br.ReadRC();
        int numDashes = br.ReadRC();
        bool hasText = false;
        for (int i = 0; i < numDashes && i < 256; i++)
        {
            double dLen = br.ReadBD();
            long dComplex = br.ReadBS();
            double dX = br.ReadRD();
            double dY = br.ReadRD();
            double dScale = br.ReadBD();
            double dRot = br.ReadBD();
            int dShape = br.ReadBS();
            if ((dShape & 2) != 0) hasText = true;
            Dashes.Add(new LtypeDash(dLen, dComplex, dX, dY, dScale, dRot, dShape));
        }

        if (StringsAreaInline)
        {
            long saStart = br.BitPosition;
            if (saStart + 2048 != HsBit) return;
            bool allZero = true;
            for (int i = 0; i < 256; i++)
            {
                if (br.ReadRC() != 0) { allZero = false; break; }
            }
            if (!allZero || hasText || br.BitPosition != HsBit) return;

            CommitValues(name, desc, resolved, numDashes);
            Cap.PatStartBit = patStart;
            Cap.SaStartBit = saStart;
            Rec.StringFields.Add(new DwgObjectRecord.StringField(
                nameStart, (int)(descStart - nameStart), name));
            Rec.StringFields.Add(new DwgObjectRecord.StringField(
                descStart, (int)(patStart - descStart), desc));
            return;
        }

        if (hasText) return;
        if (Rec.StringStreamStartBit > 0 && br.BitPosition == Rec.StringStreamStartBit)
        {
            Cap.DownNameInsBit = nameStart;
            Cap.DownDescInsBit = descStart;
            Cap.DownPatEndBit = br.BitPosition;
        }
        CommitValues(name, desc, resolved, numDashes);
    }

    private void CommitValues(string name, string desc, int resolved, int numDashes)
    {
        Cap.ValuesCommitted = true;
        Cap.Name = name;
        Cap.Description = desc;
        Cap.PatternLen = PatternLen;
        Cap.Alignment = Alignment;
        Cap.NumDashes = numDashes;
        Cap.XrefResolved = resolved;
        Cap.Dashes.Clear();
        Cap.Dashes.AddRange(Dashes);
        Name = name;
        Description = desc;
        XrefResolved = resolved;
        NumDashes = numDashes;
    }

    protected long HsBit { get; private set; }

    public double PatternLen { get; protected set; }

    public int Alignment { get; protected set; }

    public sealed record LtypeDash(
        double Length, long ComplexShapecode, double XOffset, double YOffset,
        double Scale, double Rotation, int ShapeFlag);

    public System.Collections.Generic.List<LtypeDash> Dashes { get; } = new();

    protected virtual bool ReadFrameHead(BitReader br)
    {
        uint bitsize = br.ReadRL();
        br.ReadHandle();
        if (!DwgObjectReader.TrySkipEed(br)) return false;
        HsBit = (long)ObjStart * 8 + bitsize;
        Rec.HandleStreamStartBit = HsBit;
        Rec.HasHandleStream = true;
        if (AttachCentralStringStream)
            DwgObjectReader.AttachStringStream(br, ObjStart, bitsize, Rec.SourceVersion, Rec);
        return true;
    }

    protected virtual bool AttachCentralStringStream => false;

    protected virtual int ReadXref(BitReader br)
    {
        br.ReadB();
        int resolved = br.ReadBS();
        br.ReadB();
        return resolved;
    }

    protected virtual bool HasDsData => false;

    protected virtual bool StringsAreaInline => true;

    protected virtual bool HasXdicMissing => true;
}
