namespace DwgParser.Core.Objects.Records;

internal sealed class LayoutAcR2000 : LayoutRecord
{
    public LayoutAcR2000(ulong handle, DwgObjectRecord rec, int objStart)
        : base(handle, rec, objStart) { }

    public override string Family => "LayoutAcR2000";

    protected override bool HasXdicMissing => false;
    protected override bool ReadsPlotViewName => true;
    protected override bool ReadsStylesheet => false;
    protected override int ExpectedStringCount => 4;
    protected override bool ReadsShadeplot => false;
    protected override bool ReadsNumViewports => false;
    protected override bool SplitHandleStream => false;
}
