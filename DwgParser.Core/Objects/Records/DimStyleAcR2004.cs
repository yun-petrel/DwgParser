using DwgParser.Core.IO;

namespace DwgParser.Core.Objects.Records;

internal sealed class DimStyleAcR2004 : DimStyleRecord
{
    public override string Family => "R2004";

    public DimStyleAcR2004(ulong handle, DwgObjectRecord rec,
        DwgObjectReader.WalkResult result, int objStart) : base(handle, rec, result, objStart) { }

    protected override bool ReadFrameHead(BitReader br)
    {
        Bitsize = br.ReadRL();
        br.ReadHandle();
        Cap.EedStartBit = br.BitPosition;
        bool ok = DwgObjectReader.TrySkipEed(br);
        if (ok) Cap.EedBitLen = (int)(br.BitPosition - Cap.EedStartBit);
        else { Cap.EedStartBit = 0; Cap.EedBitLen = 0; }
        return ok;
    }

    protected override bool HasXdicMissing => true;

    protected override void ReadTail(BitReader br)
    {
        if (!ReadTailFlags(br, (long)ObjStart * 8 + Bitsize - 40)) return;
        ReadTailGeometry(br);
        if (Flag0End != HsExpected) return;
        Rec.HandleStreamStartBit = HsExpected;
        Rec.HasHandleStream = true;
        Cap.NameStartBit = NameStart;
        Cap.BodyStartBit = BodyStart;
        Cap.NineBdEndBit = NineBdEnd;
        Cap.ThreeBsEndBit = ThreeBsEnd;
        Cap.CmcStartBit = CmcStart;
        Cap.FxlonEndBit = FxlonEnd;
        Cap.Flag0EndBit = Flag0End;
        Cap.LwdStartBit = LwdStart;
        Cap.XrefResolved = XrefResolved;
        Rec.StringFields.Add(new DwgObjectRecord.StringField(NameStart, (int)(NameEnd - NameStart), Name));
        Rec.StringFields.Add(new DwgObjectRecord.StringField(BodyStart, (int)(DimpostEnd - BodyStart), Dimpost));
        Rec.StringFields.Add(new DwgObjectRecord.StringField(DimpostEnd, (int)(DimapostEnd - DimpostEnd), Dimapost));
    }
}
