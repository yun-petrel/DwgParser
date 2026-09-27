using DwgParser.Core.IO;
using DwgParser.Core.Render;

namespace DwgParser.Core.Objects;

public sealed class TraceEntity : DwgEntity, ITrace
{

    public int CornerCount => Corners.Count;

    public void ReadBody(BitReader br)
    {
        SolidBodyReader.Read(br, this);
        Kind = DwgEntityType.Solid;
    }
}
