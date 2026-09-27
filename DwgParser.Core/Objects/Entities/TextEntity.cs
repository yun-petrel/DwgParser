using DwgParser.Core.IO;
using DwgParser.Core.Render;

namespace DwgParser.Core.Objects;

public abstract class TextEntity : DwgEntity, IText
{

    public abstract void ReadText(BitReader br, DwgObjectRecord? record);
}
