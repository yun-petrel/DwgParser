namespace DwgParser.Core.Objects.Records;

internal sealed class DictVarAcR2000 : DictVarRecord
{
    public DictVarAcR2000(ulong handle, DwgObjectRecord rec, int objStart)
        : base(handle, rec, objStart) { }

    public override string Family => "DictVarAcR2000";

    protected override bool HasXdicMissing => false;
}
