namespace DwgParser.Core.Objects.Records;

internal sealed class LayerAcR2000 : LayerRecord
{
    public override string Family => "R2000";

    public LayerAcR2000(ulong handle, DwgObjectRecord rec,
        DwgObjectReader.WalkResult result, int objStart)
        : base(handle, rec, result, objStart) { }
}
