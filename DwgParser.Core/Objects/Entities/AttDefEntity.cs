using DwgParser.Core.IO;
using DwgParser.Core.Render;

namespace DwgParser.Core.Objects;

public abstract class AttDefEntity : DwgEntity, IAttDef
{

    internal bool HasLockPosition { get; set; }

    internal bool HasKeepDuplicate { get; set; }

    internal bool HasLockedInBlock { get; set; }

    internal bool HasMtextType { get; set; }

    public abstract void ReadBody(BitReader br, DwgObjectRecord? record);
}
