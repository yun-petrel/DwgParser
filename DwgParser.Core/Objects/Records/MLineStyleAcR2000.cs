namespace DwgParser.Core.Objects.Records;

internal sealed class MLineStyleAcR2000 : MLineStyleRecord
{
    public MLineStyleAcR2000(ulong handle, DwgObjectRecord rec, int objStart)
        : base(handle, rec, objStart) { }

    public override string Family => "MLineStyleAcR2000";

    protected override bool HasXdicMissing => false;
}
