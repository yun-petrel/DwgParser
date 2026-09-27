namespace DwgParser.Core.Objects.Records;

internal sealed class TableControlAcR2013Plus : TableControlRecord
{
    public TableControlAcR2013Plus(ulong handle, DwgObjectRecord rec, int objStart)
        : base(handle, rec, objStart) { }

    public override string Family => "R2013+";

    protected override bool HasInlineRl => false;

    protected override bool HasXdicMissing => true;

    protected override bool HasDsData => true;

    protected override bool HasTrailingStrings => true;
}
