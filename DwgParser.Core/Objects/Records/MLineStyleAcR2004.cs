namespace DwgParser.Core.Objects.Records;

internal sealed class MLineStyleAcR2004 : MLineStyleRecord
{
    public MLineStyleAcR2004(ulong handle, DwgObjectRecord rec, int objStart)
        : base(handle, rec, objStart) { }

    public override string Family => "MLineStyleAcR2004";

    protected override bool HasXdicMissing => true;
}
