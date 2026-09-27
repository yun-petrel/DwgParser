using DwgParser.Core;
using DwgParser.Core.IO;

namespace DwgParser.Core.Objects;

public sealed class BlockEntity : DwgEntity, IBlock
{
    public string BlockName => Name;

    public void ReadBody(BitReader br, DwgObjectRecord? record, DwgVersion version, ulong handle, int objStart)
    {
        Kind = DwgEntityType.Block;
        Name = EntityHeadStringCapture.Capture(br, record, version, handle, objStart, "BLK4");
    }
}
