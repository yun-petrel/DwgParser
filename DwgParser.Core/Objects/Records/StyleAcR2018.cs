namespace DwgParser.Core.Objects.Records;

internal sealed class StyleAcR2018 : StyleAcR2010Plus
{
    public override string Family => "R2018";

    public StyleAcR2018(ulong handle, DwgObjectRecord rec,
        DwgObjectReader.WalkResult result, int objStart) : base(handle, rec, result, objStart) { }

    protected override bool HasDsData => true;
}
