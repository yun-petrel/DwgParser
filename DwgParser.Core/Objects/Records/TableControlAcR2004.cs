namespace DwgParser.Core.Objects.Records;

internal sealed class TableControlAcR2004 : TableControlRecord
{
    public TableControlAcR2004(ulong handle, DwgObjectRecord rec, int objStart)
        : base(handle, rec, objStart) { }

    public override string Family => "R2004";

    protected override bool HasXdicMissing => true;
}
