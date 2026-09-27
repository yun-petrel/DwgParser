using DwgParser.Core.IO;

namespace DwgParser.Core.Objects;

internal sealed class DimensionOrdinate : DimensionEntity
{
    protected override string SubtypeName => "Ordinate";

    protected override void ReadSubtypeGeometry(BitReader br)
    {
        var def = br.Read3BD();
        var feat = br.Read3BD();
        var lead = br.Read3BD();
        SetOrdinateAxisFlag(br.ReadRC());
        AddPt(def); AddPt(feat); AddPt(lead);
        DimDefPoint = new Vec2(def.X, def.Y);
    }
}
