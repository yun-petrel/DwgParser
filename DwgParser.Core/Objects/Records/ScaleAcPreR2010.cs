using DwgParser.Core.IO;

namespace DwgParser.Core.Objects.Records;

internal abstract class ScaleAcPreR2010 : ScaleRecord
{
    protected ScaleAcPreR2010(ulong handle, DwgObjectRecord rec, int objStart)
        : base(handle, rec, objStart) { }

    internal override void Read(BitReader br)
    {
        DwgObjectRecord.SeekTo(br, Rec.CarryStartBit);
        uint bitsize = br.ReadRL();
        long hs = (long)ObjStart * 8 + bitsize;
        if (hs <= br.BitPosition || hs > (long)Rec.BodyEnd * 8) return;
        br.ReadHandle();
        if (!DwgObjectReader.TrySkipEed(br)) return;
        if (br.ReadBL() is < 0 or > 100_000) return;
        if (HasXdicMissing) br.ReadB();

        int flag = br.ReadBS();
        string name = br.ReadTV();
        double paper = br.ReadBD();
        double drawing = br.ReadBD();
        bool unitScale = br.ReadB();

        if (br.BitPosition != hs) return;
        if (name.Length == 0) return;
        if (double.IsNaN(paper) || double.IsNaN(drawing)
            || double.IsInfinity(paper) || double.IsInfinity(drawing)) return;

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
}

internal sealed class ScaleAcR2000 : ScaleAcPreR2010
{
    public ScaleAcR2000(ulong handle, DwgObjectRecord rec, int objStart)
        : base(handle, rec, objStart) { }

    public override string Family => "R2000";

    protected override bool HasXdicMissing => false;
}

internal sealed class ScaleAcR2004 : ScaleAcPreR2010
{
    public ScaleAcR2004(ulong handle, DwgObjectRecord rec, int objStart)
        : base(handle, rec, objStart) { }

    public override string Family => "R2004";
}
