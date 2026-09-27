using DwgParser.Core.IO;

namespace DwgParser.Core.Objects;

internal sealed class InsertAcR2000 : InsertEntity
{
    protected override void ReadScale(BitReader br) => ReadScaleFlag(br);
}
