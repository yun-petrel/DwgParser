using DwgParser.Core.IO;

namespace DwgParser.Core.Objects;

public sealed class Solid3dEntity : ModelerGeometryEntity, ISolid3d
{
    public void ReadBody(BitReader br, DwgObjectRecord? record)
    {
        Kind = DwgEntityType.Solid3d;
        ReadModelerGeometry(br, record);
    }
}
