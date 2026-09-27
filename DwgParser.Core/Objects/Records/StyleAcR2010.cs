namespace DwgParser.Core.Objects.Records;

internal sealed class StyleAcR2010 : StyleAcR2010Plus
{
    public override string Family => "R2010";

    public StyleAcR2010(ulong handle, DwgObjectRecord rec,
        DwgObjectReader.WalkResult result, int objStart) : base(handle, rec, result, objStart) { }
}
