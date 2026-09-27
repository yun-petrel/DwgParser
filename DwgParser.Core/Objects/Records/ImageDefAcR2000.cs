namespace DwgParser.Core.Objects.Records;

internal sealed class ImageDefAcR2000 : ImageDefRecord
{
    public ImageDefAcR2000(ulong handle, DwgObjectRecord rec, int objStart)
        : base(handle, rec, objStart) { }

    public override string Family => "ImageDefAcR2000";

    protected override bool HasXdicMissing => false;
}
