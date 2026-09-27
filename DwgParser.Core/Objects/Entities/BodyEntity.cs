using DwgParser.Core.IO;

namespace DwgParser.Core.Objects;

public sealed class BodyEntity : ModelerGeometryEntity, IBody
{
    public void ReadBody(BitReader br, DwgObjectRecord? record)
    {
        Kind = DwgEntityType.Body;
        ReadModelerGeometry(br, record);
    }
}
