namespace DwgParser.Core.Objects.Records;

internal sealed class VportAcR2004 : VportRecord
{
    public override string Family => "R2004";

    public VportAcR2004(ulong handle, DwgVersion sourceVersion,
        DwgObjectRecord record, int objStart)
        : base(handle, sourceVersion, record, objStart) { }
}
