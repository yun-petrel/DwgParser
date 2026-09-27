namespace DwgParser.Core.Objects.Records;

internal sealed class LayoutAcR2004 : LayoutRecord
{
    public LayoutAcR2004(ulong handle, DwgObjectRecord rec, int objStart)
        : base(handle, rec, objStart) { }

    public override string Family => "LayoutAcR2004";
}
