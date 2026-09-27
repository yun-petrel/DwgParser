using DwgParser.Core.IO;

namespace DwgParser.Core.Objects;

internal sealed class AttributeAcR2000Plus : AttributeEntity
{
    public override void ReadBody(BitReader br, DwgObjectRecord? record)
    {
        TextBodyReader.ReadR2000Plus(br, record).AssignText(this);
        AttributeTailReader.Read(br, this, withPrompt: false, HasLockPosition, HasKeepDuplicate,
            HasLockedInBlock, HasMtextType, record);
        Kind = DwgEntityType.Attrib;
    }
}
