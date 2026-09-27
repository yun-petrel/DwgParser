namespace DwgParser.Core.Objects.Records;

internal sealed class StyleAcR2013 : StyleAcR2010Plus
{
    public override string Family => "R2013";

    public StyleAcR2013(ulong handle, DwgObjectRecord rec,
        DwgObjectReader.WalkResult result, int objStart) : base(handle, rec, result, objStart) { }

    protected override bool HasDsData => true;
}
