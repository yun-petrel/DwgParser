namespace DwgParser.Core.Objects.Records;

internal sealed class DictWdfltAcR2004 : DictWdfltRecord
{
    public override string Family => "R2004";

    public DictWdfltAcR2004(ulong handle, DwgObjectRecord rec, int objStart)
        : base(handle, rec, objStart) { }
}
