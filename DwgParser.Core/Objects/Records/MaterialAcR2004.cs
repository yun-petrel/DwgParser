namespace DwgParser.Core.Objects.Records;

internal sealed class MaterialAcR2004 : MaterialRecord
{
    public MaterialAcR2004(ulong handle, DwgObjectRecord rec, int objStart)
        : base(handle, rec, objStart) { }

    public override string Family => "MaterialAcR2004";
}
