using DwgParser.Core.IO;

namespace DwgParser.Core.Objects.Records;

internal sealed class DimStyleAcR2000 : DimStyleRecord
{
    public override string Family => "R2000";

    public DimStyleAcR2000(ulong handle, DwgObjectRecord rec,
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

    private readonly System.Collections.Generic.List<short> _cmcIdx = new();

    protected override uint CmcRgb(BitReader br, out string colorName, out string colorBookName)
    {
        short idx = (short)br.ReadBS();
        _cmcIdx.Add(idx);
        colorName = "";
        colorBookName = "";
        return (uint)idx;
    }

    protected override void ReadTail(BitReader br)
    {
        if (!ReadTailFlags(br, (long)ObjStart * 8 + Bitsize - 40)) return;
        if (_cmcIdx.Count != 3) return;
        ReadTailGeometry(br);
        if (Flag0End != HsExpected) return;
        Cap.NameStartBit = NameStart;
        Cap.BodyStartBit = BodyStart;
        Cap.NineBdEndBit = NineBdEnd;
        Cap.ThreeBsEndBit = ThreeBsEnd;
        Cap.CmcStartBit = CmcStart;
        Cap.CmcEndBit = CmcEnd;
        Cap.FxlonEndBit = FxlonEnd;
        Cap.Flag0EndBit = Flag0End;
        Cap.LwdStartBit = LwdStart;
        Cap.XrefResolved = XrefResolved;
        Cap.HsBit = HsExpected;
        Cap.PreR2004CmcIndices = _cmcIdx.ToArray();
        Cap.DeferredStringFields = new System.Collections.Generic.List<DwgObjectRecord.StringField>
        {
            new(NameStart, (int)(NameEnd - NameStart), Name),
            new(BodyStart, (int)(DimpostEnd - BodyStart), Dimpost),
            new(DimpostEnd, (int)(DimapostEnd - DimpostEnd), Dimapost),
        };
    }
}
