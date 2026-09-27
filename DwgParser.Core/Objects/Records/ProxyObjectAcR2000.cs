namespace DwgParser.Core.Objects.Records;

internal sealed class ProxyObjectAcR2000 : ProxyObjectRecord
{
    public ProxyObjectAcR2000(ulong handle, DwgObjectRecord rec, int objStart,
        IReadOnlyList<DwgParser.Core.Format.R2000Class>? classes = null)
        : base(handle, rec, objStart, classes) { }

    public override string Family => "ProxyObjectAcR2000";

    protected override bool HasXdicMissing => false;

    protected override bool HasInlineName => false;
}
