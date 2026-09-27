using DwgParser.Core.Format;
using DwgParser.Core.IO;

namespace DwgParser.Core.Objects.Records;

internal abstract class LayoutAcR2010Plus : LayoutRecord
{
    protected LayoutAcR2010Plus(ulong handle, DwgObjectRecord rec, int objStart)
        : base(handle, rec, objStart) { }

    protected virtual bool HasDsData => false;

    internal override void Read(BitReader br)
    {
        long ssStart = Rec.StringStreamStartBit;
        if (ssStart <= 0) return;

        DwgObjectRecord.SeekTo(br, Rec.CarryStartBit);
        long ownStart = br.BitPosition;
        br.ReadHandle();
        if (!DwgObjectReader.TrySkipEed(br)) return;
        WalkAndCommit(br, null, ssStart, ownStart);
    }

    protected void WalkAndCommit(BitReader br, BitReader? strBr, long ssStart, long ownStart)
    {
        var sbr = strBr ?? br;
        if (br.ReadBL() is < 0 or > 10000) return;
        br.ReadB();
        if (HasDsData) br.ReadB();

        bool bad = false;
        void BD() { if (double.IsNaN(br.ReadBD())) bad = true; }
        void RD() { if (double.IsNaN(br.ReadRD())) bad = true; }
        void BDs(int n) { for (int i = 0; i < n && !bad; i++) BD(); }
        void RDs(int n) { for (int i = 0; i < n && !bad; i++) RD(); }
        void BS() { br.ReadBS(); }

        var ins = new (long pos, string val)[5];
        int ns = 0;
        void TV()
        {
            long p = br.BitPosition;
            string v = sbr.ReadTV();
            ins[ns++] = (p, v);
        }

        TV();
        TV();
        BS();
        BDs(6);
        TV();
        BDs(2);
        BS(); BS(); BS();
        BDs(2);
        BDs(2);
        BD();
        BD();
        TV();
        BS();
        BD();
        BDs(2);
        BS(); BS(); BS();
        TV();
        BS();
        BS();
        BDs(3);
        RDs(2);
        RDs(2);
        BDs(3);
        BDs(3);
        BDs(3);
        BD();
        BS();
        BDs(3);
        BDs(3);
        br.ReadBL();

        bool dbg = System.Environment.GetEnvironmentVariable("DWGDBG") == "1";
        if (dbg)
            System.Console.Error.WriteLine(
                $"LAYDN h={Rec.Handle:X} t={Rec.LogicalType} src={Rec.SourceVersion} end={br.BitPosition} " +
                $"ss={ssStart} Δ={(int)(br.BitPosition - ssStart)} bad={bad} ns={ns} " +
                $"[{string.Join(",", System.Linq.Enumerable.Select(ins, x => $"'{x.val}'@{x.pos}"))}]");

        if (bad || ns != 5 || br.BitPosition != ssStart) return;
        long numericStart = ownStart;
        for (int i = 0; i < ns; i++)
            if (ins[i].pos < numericStart || ins[i].pos > ssStart) return;

        Cap.Downgrade.Clear();
        for (int i = 0; i < ns; i++)
            Cap.Downgrade.Add(new LayoutDowngradeString(ins[i].pos, ins[i].val));
        Cap.DowngradeCaptured = true;
    }
}

internal sealed class LayoutAcR2010 : LayoutAcR2010Plus
{
    public LayoutAcR2010(ulong handle, DwgObjectRecord rec, int objStart)
        : base(handle, rec, objStart) { }

    public override string Family => "LayoutAcR2010";
}

internal sealed class LayoutAcR2013 : LayoutAcR2010Plus
{
    public LayoutAcR2013(ulong handle, DwgObjectRecord rec, int objStart)
        : base(handle, rec, objStart) { }

    public override string Family => "LayoutAcR2013";

    protected override bool HasDsData => true;
}

internal sealed class LayoutAcR2018 : LayoutAcR2010Plus
{
    public LayoutAcR2018(ulong handle, DwgObjectRecord rec, int objStart)
        : base(handle, rec, objStart) { }

    public override string Family => "LayoutAcR2018";

    protected override bool HasDsData => true;
}

internal sealed class LayoutAcR2007 : LayoutAcR2010Plus
{
    public LayoutAcR2007(ulong handle, DwgObjectRecord rec, int objStart)
        : base(handle, rec, objStart) { }

    public override string Family => "LayoutAcR2007";

    internal override void Read(BitReader br)
    {
        if (!CentralFrame.TryRlFrame(Rec, ObjStart, br, out long ssStart, out _,
                out BitReader? strBr) || strBr is null) return;
        WalkAndCommit(br, strBr, ssStart, Rec.CarryStartBit + 32);
    }
}
