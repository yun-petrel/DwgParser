using DwgParser.Core.IO;
using DwgParser.Core.Render;

namespace DwgParser.Core.Objects;

public sealed class RayEntity : DwgEntity, IRay
{

    public void ReadGeometry(BitReader br)
    {
        var p = br.Read3BD();
        var v = br.Read3BD();
        Kind = DwgEntityType.Ray;
        Start = new Vec2(p.X, p.Y);
        const double len = 1e6;
        double dx = v.X * len, dy = v.Y * len;
        End = new Vec2(p.X + dx, p.Y + dy);
        Rotation = Math.Atan2(dy, dx);
    }
}

public sealed class XLineEntity : DwgEntity, IXLine
{

    public void ReadGeometry(BitReader br)
    {
        var p = br.Read3BD();
        var v = br.Read3BD();
        Kind = DwgEntityType.XLine;
        Start = new Vec2(p.X, p.Y);
        const double len = 1e6;
        double dx = v.X * len, dy = v.Y * len;
        End = new Vec2(p.X + dx, p.Y + dy);
        Rotation = Math.Atan2(dy, dx);
    }
}
