using DwgParser.Core.Format;
using DwgParser.Core.IO;

namespace DwgParser.Core.Objects.Records;

public abstract class DictWdfltRecord : IDictWdflt
{
    public const short TypeCodeValue = 500;

    protected readonly DwgObjectRecord Rec;

    protected readonly int ObjStart;

    protected readonly ulong Handle;

    protected DictWdfltRecord(ulong handle, DwgObjectRecord rec, int objStart)
    {
        Handle = handle;
        Rec = rec;
        ObjStart = objStart;
        Cap = rec.DictWdflt ??= new DictWdfltCapture();
        Cap.AttachIdentity(rec);
    }

    public DictWdfltCapture Cap { get; }

    public abstract string Family { get; }

    ulong IRecord.Handle => Handle;
    public short TypeCode => TypeCodeValue;
    public DwgVersion SourceVersion => Rec.SourceVersion;
    public long NumEntries { get; protected set; }
    public string EntryName { get; protected set; } = "";
    public bool Captured => Cap.Captured;

    protected void Fail(BitReader br, string why)
    {
        Cap.FailReason = why;
        Cap.FailBit = br.BitPosition - Rec.CarryStartBit;
    }

    internal virtual void Read(BitReader br)
    {
        uint bitsize = br.ReadRL();
        long hs = (long)ObjStart * 8 + bitsize;
        if (hs <= br.BitPosition || hs > (long)Rec.BodyEnd * 8) { Fail(br, "帧界(RL vs BodyEnd)"); return; }
        long ownStart = br.BitPosition;
        br.ReadHandle();
        long eedStart = br.BitPosition;
        if (!DwgObjectReader.TrySkipEed(br)) { Fail(br, "EED"); return; }
        long eedLen = br.BitPosition - eedStart;
        br.ReadBL();
        if (HasXdicMissing) br.ReadB();

        long numEntries = br.ReadBL();
        DuplicateCloning = br.ReadBS();
        HardOwner = br.ReadRC();

        if (numEntries < 1 || numEntries > 4096) { Fail(br, $"num_entries={numEntries}"); return; }
        int nameStartRel = (int)(br.BitPosition - ownStart);
        var names = new List<(long Start, int BitLen, string Value)>((int)numEntries);
        for (long k = 0; k < numEntries; k++)
        {
            long s0 = br.BitPosition;
            string v = br.ReadTV();
            long e0 = br.BitPosition;
            if (v.Length == 0 || s0 < ownStart || e0 > hs)
            { Fail(br, $"条目[{k}] 空名或越界"); return; }
            names.Add((s0, (int)(e0 - s0), v));
        }
        long nameEnd = br.BitPosition;
        string name = names[0].Value;
        long nameStart = names[0].Start;

        bool dbg = System.Environment.GetEnvironmentVariable("DWGDBG") == "1";
        if (dbg) System.Console.Error.WriteLine(
            $"WDFLTREC h={Rec.Handle:X} t={Rec.TypeCode} hs={hs} nent={numEntries} " +
            $"names=[{string.Join(",", names.ConvertAll(n => n.Value))}] " +
            $"[{nameStartRel}..{nameEnd - ownStart}] gap={(int)(hs - nameEnd)}b");

        Rec.HandleStreamStartBit = hs;
        Rec.HasHandleStream = true;
        Rec.EedStartBit = eedStart;
        Rec.EedBitLen = (int)eedLen;
        foreach (var n in names)
            Rec.StringFields.Add(new DwgObjectRecord.StringField(n.Start, n.BitLen, n.Value));
        Cap.EntryNames = new List<string>(names.ConvertAll(n => n.Value));
        Cap.DefaultEntryName = names[names.Count - 1].Value;
        Cap.NumEntries = numEntries;
        Cap.EntryName = name;
        Cap.EntryNameStartBit = nameStart;
        Cap.EntryNameBitLen = (int)(nameEnd - nameStart);
        Cap.DuplicateCloning = DuplicateCloning;
        Cap.HardOwner = HardOwner;
        Cap.Captured = true;
        NumEntries = numEntries;
        EntryName = name;
    }

    protected virtual bool HasXdicMissing => true;

    public long DuplicateCloning { get; protected set; }

    public int HardOwner { get; protected set; }
}
