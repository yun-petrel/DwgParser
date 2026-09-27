using DwgParser.Core.IO;

namespace DwgParser.Core.Objects.Records;

internal abstract class LtypeAcR2010Plus : LtypeRecord
{
    protected LtypeAcR2010Plus(ulong handle, DwgObjectRecord rec, int objStart)
        : base(handle, rec, objStart) { }

    protected override bool ReadFrameHead(BitReader br)
    {
        br.ReadHandle();
        return DwgObjectReader.TrySkipEed(br);
    }

    protected override int ReadXref(BitReader br) => br.ReadBS();

    protected override bool StringsAreaInline => false;
}

internal sealed class LtypeAcR2010 : LtypeAcR2010Plus
{
    public override string Family => "R2010";

    public LtypeAcR2010(ulong handle, DwgObjectRecord rec, int objStart)
        : base(handle, rec, objStart) { }
}
