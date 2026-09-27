namespace DwgParser.Core.Objects.Records;

internal sealed class TableControlAcR2010 : TableControlRecord
{
    public TableControlAcR2010(ulong handle, DwgObjectRecord rec, int objStart)
        : base(handle, rec, objStart) { }

    public override string Family => "R2010";

    protected override bool HasInlineRl => false;

    protected override bool HasXdicMissing => true;

    protected override bool HasTrailingStrings => true;
}
