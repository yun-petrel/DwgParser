namespace DwgParser.Core.Objects.Records;

internal sealed class DictVarAcR2004 : DictVarRecord
{
    public DictVarAcR2004(ulong handle, DwgObjectRecord rec, int objStart)
        : base(handle, rec, objStart) { }

    public override string Family => "DictVarAcR2004";

    protected override bool HasXdicMissing => true;
}
