namespace DwgParser.Core.Objects.Records;

internal sealed class StyleAcR2004 : StyleRecord
{
    public override string Family => "R2004";

    public StyleAcR2004(ulong handle, DwgObjectRecord rec,
        DwgObjectReader.WalkResult result, int objStart) : base(handle, rec, result, objStart) { }

    protected override bool HasXdicMissing => true;

    protected override bool SubmitsCapture => true;
}
