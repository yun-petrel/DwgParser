using DwgParser.Core.IO;

namespace DwgParser.Core.Objects.Records;

internal sealed class StyleAcR2007 : StyleRecord
{
    public override string Family => "R2007";

    public StyleAcR2007(ulong handle, DwgObjectRecord rec,
        DwgObjectReader.WalkResult result, int objStart) : base(handle, rec, result, objStart) { }

    protected override bool AttachCentralStringStream => true;

    protected override bool HasXdicMissing => true;

    protected override int ReadXref(BitReader br) => br.ReadBS();

    protected override bool SubmitsCentralValues => true;
}
