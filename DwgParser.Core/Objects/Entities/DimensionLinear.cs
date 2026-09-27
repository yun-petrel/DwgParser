using DwgParser.Core.IO;

namespace DwgParser.Core.Objects;

internal sealed class DimensionLinear : DimensionEntity
{
    protected override string SubtypeName => "Linear";

    protected override void ReadSubtypeGeometry(BitReader br)
    {
        var x1 = br.Read3BD();
        var x2 = br.Read3BD();
        var def = br.Read3BD();
        ObliqueAngle = br.ReadBD();
        DimLineRotation = br.ReadBD();
        AddPt(x1); AddPt(x2); AddPt(def);
        DimXLine1 = new Vec2(x1.X, x1.Y);
        DimXLine2 = new Vec2(x2.X, x2.Y);
        DimDefPoint = new Vec2(def.X, def.Y);
    }
}
