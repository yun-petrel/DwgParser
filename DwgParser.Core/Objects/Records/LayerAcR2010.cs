namespace DwgParser.Core.Objects.Records;

internal sealed class LayerAcR2010 : LayerAcR2010Plus
{
    public override string Family => "R2010";

    public LayerAcR2010(ulong handle, DwgObjectRecord rec,
        DwgObjectReader.WalkResult result, int objStart)
        : base(handle, rec, result, objStart) { }
}
