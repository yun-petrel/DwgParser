using DwgParser.Core.IO;

namespace DwgParser.Core.Objects;

internal sealed class AttDefAcR2000Plus : AttDefEntity
{
    public override void ReadBody(BitReader br, DwgObjectRecord? record)
    {
        TextBodyReader.ReadR2000Plus(br, record).AssignText(this);
        AttributeTailReader.Read(br, this, withPrompt: true, HasLockPosition, HasKeepDuplicate,
            HasLockedInBlock, HasMtextType, record);
        Kind = DwgEntityType.AttDef;
    }
}
