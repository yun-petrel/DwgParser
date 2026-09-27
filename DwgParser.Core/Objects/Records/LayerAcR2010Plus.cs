using DwgParser.Core.IO;

namespace DwgParser.Core.Objects.Records;

internal abstract class LayerAcR2010Plus : LayerRecord
{
    protected LayerAcR2010Plus(ulong handle, DwgObjectRecord rec,
        DwgObjectReader.WalkResult result, int objStart)
        : base(handle, rec, result, objStart) { }

    protected override bool ReadFrameHead(BitReader br)
    {
        br.ReadHandle();
        if (!DwgObjectReader.TrySkipEed(br)) return false;
        return true;
    }

    protected override bool HasXdicMissing => true;

    protected override void ReadXref(BitReader br) => ReadXrefSingleBs(br);

    protected override short ReadColor(BitReader br, out uint trueColor,
        out string colorName, out string colorBookName)
        => DwgObjectReader.ReadCmc(br, out trueColor, out colorName, out colorBookName);

    protected override bool UsesCmcColor => true;
}
