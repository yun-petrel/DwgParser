using DwgParser.Core.IO;

namespace DwgParser.Core.Objects.Records;

internal sealed class LtypeAcR2007 : LtypeRecord
{
    public override string Family => "R2007";

    public LtypeAcR2007(ulong handle, DwgObjectRecord rec, int objStart)
        : base(handle, rec, objStart) { }

    protected override bool AttachCentralStringStream => true;

    protected override int ReadXref(BitReader br) => br.ReadBS();

    protected override bool StringsAreaInline => false;
}
