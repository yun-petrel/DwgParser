namespace DwgParser.Core.Objects.Records;

internal sealed class LayerAcR2013 : LayerAcR2010Plus
{
    public override string Family => "R2013";

    public LayerAcR2013(ulong handle, DwgObjectRecord rec,
        DwgObjectReader.WalkResult result, int objStart)
        : base(handle, rec, result, objStart) { }

    protected override bool HasDsData => true;
}
