namespace DwgParser.Core.Objects.Records;

internal sealed class VportAcR2000 : VportRecord
{
    public override string Family => "R2000";

    public VportAcR2000(ulong handle, DwgVersion sourceVersion,
        DwgObjectRecord record, int objStart)
        : base(handle, sourceVersion, record, objStart) { }

    protected override bool HasXdicMissing => false;
}
