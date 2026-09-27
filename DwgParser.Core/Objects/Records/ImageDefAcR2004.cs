namespace DwgParser.Core.Objects.Records;

internal sealed class ImageDefAcR2004 : ImageDefRecord
{
    public ImageDefAcR2004(ulong handle, DwgObjectRecord rec, int objStart)
        : base(handle, rec, objStart) { }

    public override string Family => "ImageDefAcR2004";
}
