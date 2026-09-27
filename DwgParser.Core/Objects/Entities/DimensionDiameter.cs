using DwgParser.Core.IO;

namespace DwgParser.Core.Objects;

internal sealed class DimensionDiameter : DimensionEntity
{
    protected override string SubtypeName => "Diametric";

    protected override void ReadSubtypeGeometry(BitReader br)
    {
        var arc = br.Read3BD();
        var def = br.Read3BD();
        SetLeaderLength(br.ReadBD());
        AddPt(arc); AddPt(def);
        DimDefPoint = new Vec2(def.X, def.Y);
    }
}
