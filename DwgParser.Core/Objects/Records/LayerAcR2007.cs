using DwgParser.Core.IO;

namespace DwgParser.Core.Objects.Records;

internal sealed class LayerAcR2007 : LayerRecord
{
    public override string Family => "R2007";

    public LayerAcR2007(ulong handle, DwgObjectRecord rec,
        DwgObjectReader.WalkResult result, int objStart)
        : base(handle, rec, result, objStart) { }

    protected override bool AttachCentralStringStream => true;

    protected override bool HasXdicMissing => true;

    protected override void ReadXref(BitReader br) => ReadXrefSingleBs(br);

    protected override short ReadColor(BitReader br, out uint trueColor,
        out string colorName, out string colorBookName)
        => DwgObjectReader.ReadCmc(br, out trueColor, out colorName, out colorBookName);

    protected override bool UsesCmcColor => true;
}
