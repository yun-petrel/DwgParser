using DwgParser.Core.IO;

namespace DwgParser.Core.Objects;

internal sealed class TextAcR2000Plus : TextEntity
{
    public override void ReadText(BitReader br, DwgObjectRecord? record)
    {
        TextBodyReader.ReadR2000Plus(br, record).AssignText(this);
        Kind = DwgEntityType.Text;
    }
}
