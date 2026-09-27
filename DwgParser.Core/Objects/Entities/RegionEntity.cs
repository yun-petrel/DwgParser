using DwgParser.Core.IO;

namespace DwgParser.Core.Objects;

public sealed class RegionEntity : ModelerGeometryEntity, IRegion
{
    public void ReadBody(BitReader br, DwgObjectRecord? record)
    {
        Kind = DwgEntityType.Region;
        ReadModelerGeometry(br, record);
    }
}
