namespace DwgParser.Core.Objects.Records;

internal sealed class TableControlAcR2007 : TableControlRecord
{
    public TableControlAcR2007(ulong handle, DwgObjectRecord rec, int objStart)
        : base(handle, rec, objStart) { }

    public override string Family => "R2007";

    protected override bool HasXdicMissing => true;

    protected override bool HasTrailingStrings => true;
}
