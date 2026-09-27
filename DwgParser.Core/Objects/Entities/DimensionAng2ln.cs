using DwgParser.Core.IO;

namespace DwgParser.Core.Objects;

internal sealed class DimensionAng2ln : DimensionEntity
{
    protected override string SubtypeName => "Ang2ln";

    protected override void ReadSubtypeGeometry(BitReader br)
    {
        var def = br.Read2RD();
        var a = br.Read3BD();
        var b = br.Read3BD();
        var c = br.Read3BD();
        var d = br.Read3BD();
        AddPt(a); AddPt(b); AddPt(c); AddPt(d);
        AddPt((def.X, def.Y, 0.0));
        DimXLine1 = new Vec2(a.X, a.Y);
        DimXLine2 = new Vec2(c.X, c.Y);
        DimDefPoint = new Vec2(def.X, def.Y);
    }
}
