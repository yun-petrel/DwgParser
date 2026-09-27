namespace DwgParser.Core.Objects.Records;

internal sealed class LtypeAcR2000 : LtypeRecord
{
    public override string Family => "R2000";

    protected override bool HasXdicMissing => false;

    public LtypeAcR2000(ulong handle, DwgObjectRecord rec, int objStart)
        : base(handle, rec, objStart) { }
}
