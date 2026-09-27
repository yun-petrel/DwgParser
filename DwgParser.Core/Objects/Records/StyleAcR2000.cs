namespace DwgParser.Core.Objects.Records;

internal sealed class StyleAcR2000 : StyleRecord
{
    public override string Family => "R2000";

    public StyleAcR2000(ulong handle, DwgObjectRecord rec,
        DwgObjectReader.WalkResult result, int objStart) : base(handle, rec, result, objStart) { }

    protected override bool SubmitsCapture => true;
}
