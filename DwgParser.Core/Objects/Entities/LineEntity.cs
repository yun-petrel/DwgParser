using DwgParser.Core.IO;
using DwgParser.Core.Render;

namespace DwgParser.Core.Objects;

public abstract class LineEntity : DwgEntity, ILine
{

    public abstract void ReadGeometry(BitReader br);
}
