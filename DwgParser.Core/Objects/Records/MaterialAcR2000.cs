namespace DwgParser.Core.Objects.Records;

internal sealed class MaterialAcR2000 : MaterialRecord
{
    public MaterialAcR2000(ulong handle, DwgObjectRecord rec, int objStart)
        : base(handle, rec, objStart) { }

    public override string Family => "MaterialAcR2000";

    protected override bool HasXdicMissing => false;
}
