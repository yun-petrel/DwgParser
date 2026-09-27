using DwgParser.Core.IO;

namespace DwgParser.Core.Objects;

public sealed class SeqEndEntity : DwgEntity, ISeqEnd
{
    public void ReadBody(BitReader br)
    {
        _ = br;
        Kind = DwgEntityType.SeqEnd;
    }
}
