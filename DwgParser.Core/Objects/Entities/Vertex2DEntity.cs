using DwgParser.Core.IO;

namespace DwgParser.Core.Objects;

public sealed class Vertex2DEntity : DwgEntity, IVertex2D
{
    public double Bulge => Radius;

    public int Flag { get; private set; }

    public double StartWidth { get; private set; }

    public double EndWidth { get; private set; } = -1;

    public double TangentDir { get; private set; }

    public void ReadGeometry(BitReader br)
    {
        Flag = br.ReadRC();
        var pt = br.Read3BD();
        double startWidth = br.ReadBD();
        double endWidth = -1;
        if (startWidth >= 0)
            endWidth = br.ReadBD();
        double bulge = br.ReadBD();
        TangentDir = br.ReadBD();
        StartWidth = startWidth;
        EndWidth = endWidth;
        Kind = DwgEntityType.Vertex2D;
        Start = new Vec2(pt.X, pt.Y);
        Radius = bulge;
    }
}
