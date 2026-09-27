using DwgParser.Core.IO;

namespace DwgParser.Core.Objects;

public sealed class EndBlkEntity : DwgEntity, IEndBlk
{
    public void ReadBody(BitReader br)
    {
        _ = br;
        Kind = DwgEntityType.EndBlk;
    }
}
