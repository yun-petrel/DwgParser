namespace DwgParser.Core.Objects.Records;

internal sealed class ProxyObjectAcR2004 : ProxyObjectRecord
{
    public ProxyObjectAcR2004(ulong handle, DwgObjectRecord rec, int objStart,
        IReadOnlyList<DwgParser.Core.Format.R2000Class>? classes = null)
        : base(handle, rec, objStart, classes) { }

    public override string Family => "ProxyObjectAcR2004";
}
