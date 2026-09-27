namespace DwgParser.Core.Objects.Records;

internal sealed class LtypeAcR2013 : LtypeAcR2010Plus
{
    public override string Family => "R2013";

    public LtypeAcR2013(ulong handle, DwgObjectRecord rec, int objStart)
        : base(handle, rec, objStart) { }

    protected override bool HasDsData => true;
}
