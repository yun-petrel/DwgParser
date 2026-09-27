namespace DwgParser.Core.Objects.Records;

internal sealed class LtypeAcR2004 : LtypeRecord
{
    public override string Family => "R2004";

    public LtypeAcR2004(ulong handle, DwgObjectRecord rec, int objStart)
        : base(handle, rec, objStart) { }
}
