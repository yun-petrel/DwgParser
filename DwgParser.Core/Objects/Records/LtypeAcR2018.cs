namespace DwgParser.Core.Objects.Records;

internal sealed class LtypeAcR2018 : LtypeAcR2010Plus
{
    public override string Family => "R2018";

    public LtypeAcR2018(ulong handle, DwgObjectRecord rec, int objStart)
        : base(handle, rec, objStart) { }

    protected override bool HasDsData => true;
}
