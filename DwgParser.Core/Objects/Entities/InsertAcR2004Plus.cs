using DwgParser.Core.IO;

namespace DwgParser.Core.Objects;

internal sealed class InsertAcR2004Plus : InsertEntity
{
    protected override void ReadScale(BitReader br) => ReadScaleFlag(br);

    protected override bool ReadsNumOwned => true;
}
