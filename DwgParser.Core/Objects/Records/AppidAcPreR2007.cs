using DwgParser.Core.IO;

namespace DwgParser.Core.Objects.Records;

internal abstract class AppidAcPreR2007 : AppidRecord
{
    protected AppidAcPreR2007(ulong handle, DwgObjectRecord rec, int objStart)
        : base(handle, rec, objStart) { }

    internal override void Read(BitReader br)
    {
        DwgObjectRecord.SeekTo(br, Rec.CarryStartBit);
        uint bitsize = br.ReadRL();
        long hs = (long)ObjStart * 8 + bitsize;
        if (hs <= br.BitPosition || hs > (long)Rec.BodyEnd * 8) return;
        br.ReadHandle();
        if (!DwgObjectReader.TrySkipEed(br)) return;
        int numReactors = br.ReadBL();
        if (numReactors is < 0 or > 100_000) return;
        if (HasXdicMissing) br.ReadB();

        string name = br.ReadTV();
        if (name.Length == 0) return;
        br.ReadB();
        int xrefResolved = br.ReadBS();
        br.ReadB();
        int unknown71 = br.ReadRC();

        if (br.BitPosition != hs) return;
        if (xrefResolved is not (0 or 256)) return;

        Cap.Name = name;
        Cap.XrefResolved = xrefResolved;
        Cap.HasUnknown71 = true;
        Cap.Unknown71 = unknown71;
        Cap.Captured = true;
        Name = name;
        XrefResolved = xrefResolved;
        HasUnknown71 = true;
        Unknown71 = unknown71;
    }
}

internal sealed class AppidAcR2000 : AppidAcPreR2007
{
    public override string Family => "R2000";

    public AppidAcR2000(ulong handle, DwgObjectRecord rec, int objStart)
        : base(handle, rec, objStart) { }

    protected override bool HasXdicMissing => false;
}

internal sealed class AppidAcR2004 : AppidAcPreR2007
{
    public override string Family => "R2004";

    public AppidAcR2004(ulong handle, DwgObjectRecord rec, int objStart)
        : base(handle, rec, objStart) { }
}
