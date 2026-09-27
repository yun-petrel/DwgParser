namespace DwgParser.Core.Objects.Records;

internal sealed class LayerAcR2018 : LayerAcR2010Plus
{
    public override string Family => "R2018";

    public LayerAcR2018(ulong handle, DwgObjectRecord rec,
        DwgObjectReader.WalkResult result, int objStart)
        : base(handle, rec, result, objStart) { }

    protected override bool HasDsData => true;
}
