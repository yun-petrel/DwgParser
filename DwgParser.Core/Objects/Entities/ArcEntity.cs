using DwgParser.Core.IO;
using DwgParser.Core.Render;

namespace DwgParser.Core.Objects;

public sealed class ArcEntity : DwgEntity, IArc
{

    public void ReadGeometry(BitReader br)
    {
        var c = br.Read3BD();
        double r = br.ReadBD();
        if (!double.IsFinite(r) || r > 1e12)
            throw new System.IO.EndOfStreamException();
        double thickness = br.ReadBT();
        var extrusion = br.ReadBE();
        double startAngle = br.ReadBD();
        double endAngle = br.ReadBD();
        Kind = DwgEntityType.Arc;
        Center = new Vec2(c.X, c.Y);
        Radius = r;
        StartAngle = startAngle;
        EndAngle = endAngle;
        Thickness = thickness;
        ExtrusionZ = extrusion.Z;
        Elevation = c.Z;
    }
}
