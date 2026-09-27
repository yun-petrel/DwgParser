namespace DwgParser.Core.Objects.Records;

internal sealed class TableControlAcR2000 : TableControlRecord
{
    public TableControlAcR2000(ulong handle, DwgObjectRecord rec, int objStart)
        : base(handle, rec, objStart) { }

    public override string Family => "R2000";
}
