namespace DwgParser.Core.Objects.Records;

internal sealed class DictWdfltAcR2000 : DictWdfltRecord
{
    public override string Family => "R2000";

    public DictWdfltAcR2000(ulong handle, DwgObjectRecord rec, int objStart)
        : base(handle, rec, objStart) { }

    protected override bool HasXdicMissing => false;
}
