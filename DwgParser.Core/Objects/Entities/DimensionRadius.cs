using DwgParser.Core.IO;

namespace DwgParser.Core.Objects;

internal sealed class DimensionRadius : DimensionEntity
{
    protected override string SubtypeName => "Radial";

    protected override void ReadSubtypeGeometry(BitReader br)
    {
        var def = br.Read3BD();
        var arc = br.Read3BD();
        SetLeaderLength(br.ReadBD());
        AddPt(def); AddPt(arc);
        DimDefPoint = new Vec2(def.X, def.Y);
    }
}
