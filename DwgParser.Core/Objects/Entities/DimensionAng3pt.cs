using DwgParser.Core.IO;

namespace DwgParser.Core.Objects;

internal sealed class DimensionAng3pt : DimensionEntity
{
    protected override string SubtypeName => "Ang3pt";

    protected override void ReadSubtypeGeometry(BitReader br)
    {
        var def = br.Read3BD();
        var x1 = br.Read3BD();
        var x2 = br.Read3BD();
        var cen = br.Read3BD();
        AddPt(def); AddPt(x1); AddPt(x2); AddPt(cen);
        DimXLine1 = new Vec2(x1.X, x1.Y);
        DimXLine2 = new Vec2(x2.X, x2.Y);
        DimCenter = new Vec2(cen.X, cen.Y);
    }
}
