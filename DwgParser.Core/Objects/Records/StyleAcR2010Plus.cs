using DwgParser.Core.IO;

namespace DwgParser.Core.Objects.Records;

internal abstract class StyleAcR2010Plus : StyleRecord
{
    protected StyleAcR2010Plus(ulong handle, DwgObjectRecord rec,
        DwgObjectReader.WalkResult result, int objStart) : base(handle, rec, result, objStart) { }

    protected override bool ReadFrameHead(BitReader br)
    {
        br.ReadHandle();
        Rec.EedStartBit = br.BitPosition;
        if (!DwgObjectReader.TrySkipEed(br)) return false;
        Rec.EedBitLen = (int)(br.BitPosition - Rec.EedStartBit);
        return true;
    }

    protected override bool HasXdicMissing => true;

    protected override int ReadXref(BitReader br) => br.ReadBS();

    protected override bool SubmitsCentralValues => true;
}
