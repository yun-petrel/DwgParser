using DwgParser.Core.IO;
using DwgParser.Core.Render;

namespace DwgParser.Core.Objects;

public sealed class PointEntity : DwgEntity, IPoint
{
    protected override string WriteSvg(RenderContext ctx, Matrix m)
    {
        string fillColor = ctx.EntityColor(this);
        var (x, y) = m.Apply(Start.X, Start.Y);
        ctx.Expand(x, y);
        return $"    <circle cx=\"{RenderContext.F(x)}\" cy=\"{RenderContext.F(y)}\" r=\"0.5\" fill=\"{fillColor}\"/>\n";
    }

    public double PointAngle { get; private set; }

    public void ReadGeometry(BitReader br)
    {
        double x = br.ReadBD();
        double y = br.ReadBD();
        double z = br.ReadBD();
        double thickness = br.ReadBT();
        var extrusion = br.ReadBE();
        double xAng = br.ReadBD();
        Kind = DwgEntityType.Point;
        Start = new Vec2(x, y);
        Elevation = z;
        Thickness = thickness;
        ExtrusionZ = extrusion.Z;
        PointAngle = xAng;
    }
}
