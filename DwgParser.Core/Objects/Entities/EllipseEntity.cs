using DwgParser.Core.IO;
using DwgParser.Core.Render;

namespace DwgParser.Core.Objects;

public sealed class EllipseEntity : DwgEntity, IEllipse
{
    protected override string WriteSvg(RenderContext ctx, Matrix m)
        => RenderEllipse(ctx, m, ctx.EntityColor(this));

    public void ReadGeometry(BitReader br)
    {
        var center = br.Read3BD();
        var smAxis = br.Read3BD();
        var extrusion = br.Read3BD();
        double axisRatio = br.ReadBD();
        double startAngle = br.ReadBD();
        double endAngle = br.ReadBD();
        Kind = DwgEntityType.Ellipse;
        Center = new Vec2(center.X, center.Y);
        Start = new Vec2(smAxis.X, smAxis.Y);
        Radius = Math.Sqrt(smAxis.X * smAxis.X + smAxis.Y * smAxis.Y);
        WidthFactor = axisRatio;
        StartAngle = startAngle;
        EndAngle = endAngle;
        ExtrusionZ = extrusion.Z;
        Elevation = center.Z;
    }
}
