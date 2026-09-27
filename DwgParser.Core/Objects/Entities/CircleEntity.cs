using DwgParser.Core.IO;
using DwgParser.Core.Render;

namespace DwgParser.Core.Objects;

public sealed class CircleEntity : DwgEntity, ICircle
{

    public void ReadGeometry(BitReader br)
    {
        var c = br.Read3BD();
        double r = br.ReadBD();
        if (!double.IsFinite(r) || r > 1e12)
            throw new System.IO.EndOfStreamException();
        double thickness = br.ReadBT();
        var extrusion = br.ReadBE();
        Kind = DwgEntityType.Circle;
        Center = new Vec2(c.X, c.Y);
        Radius = r;
        Thickness = thickness;
        ExtrusionZ = extrusion.Z;
        Elevation = c.Z;
    }
}
