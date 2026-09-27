using DwgParser.Core.IO;

namespace DwgParser.Core.Objects.Records;

internal sealed class DimStyleAcR2007 : DimStyleRecord
{
    public override string Family => "R2007";

    public DimStyleAcR2007(ulong handle, DwgObjectRecord rec,
        DwgObjectReader.WalkResult result, int objStart) : base(handle, rec, result, objStart) { }

    protected override bool ReadFrameHead(BitReader br)
    {
        Bitsize = br.ReadRL();
        br.ReadHandle();
        if (!DwgObjectReader.TrySkipEed(br)) return false;
        DwgObjectReader.AttachStringStream(br, ObjStart, Bitsize, SourceVersion, Rec);
        return true;
    }

    protected override bool HasXdicMissing => true;

    protected override int ReadXref(BitReader br)
    {
        int v = br.ReadBS();
        XrefEnd = br.BitPosition;
        return v;
    }

    private long XrefEnd, R2007aHeadEnd, ArcSymEnd;

    protected override void ReadR2007aHead(BitReader br)
    {
        Dimfxl = br.ReadBD();
        Dimjogang = br.ReadBD();
        Dimtfill = br.ReadBS();
        Dimtfillclr = CmcRgb(br, out string fillName, out string fillBook);
        DimtfillclrName = fillName; DimtfillclrBookName = fillBook;
        R2007aHeadEnd = br.BitPosition;
    }

    protected override void ReadArcSym(BitReader br)
    {
        Dimarcsym = br.ReadBS();
        ArcSymEnd = br.BitPosition;
    }

    protected override void ReadTail(BitReader br)
    {
        if (!ReadTailFlags(br, (long)ObjStart * 8 + Bitsize - 40)) return;
        ReadTailGeometry(br);
        if (Rec.StringStreamStartBit <= 0 || Flag0End != Rec.StringStreamStartBit) return;
        Rec.HandleStreamStartBit = HsExpected;
        Rec.HasHandleStream = true;
        Cap.FxlonEndBit = FxlonEnd;
        Cap.Flag0EndBit = Flag0End;
        Cap.XrefResolved = XrefResolved;
        Cap.DownCaptured = true;
        Cap.DownXrefEndBit = XrefEnd;
        Cap.DownNineBdEndBit = NineBdEnd;
        Cap.DownHeadEndBit = R2007aHeadEnd;
        Cap.DownThreeBsEndBit = ThreeBsEnd;
        Cap.DownArcSymEndBit = ArcSymEnd;
        Cap.DownAtFitEndBit = LwdStart;
        Cap.DownBlockBits = 0;
        Cap.CmcStartBit = CmcStart;
        Cap.CmcEndBit = CmcEnd;
        Cap.Name = Name;
        Cap.Dimpost = Dimpost;
        Cap.Dimapost = Dimapost;
    }
}
