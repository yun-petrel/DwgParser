using DwgParser.Core.IO;
using DwgParser.Core.Render;

namespace DwgParser.Core.Objects;

public sealed class Polyline2DEntity : DwgEntity, IPolyline2D
{

    public int CurveType { get; private set; }

    public double StartWidth { get; private set; }

    public double EndWidth { get; private set; }

    public void ReadGeometry(BitReader br)
    {
        short flag = br.ReadBS();
        Closed = (flag & 1) != 0;
        PlineGen = (flag & 2) != 0;
        CurveType = br.ReadBS();
        StartWidth = br.ReadBD();
        EndWidth = br.ReadBD();
        Thickness = br.ReadBT();
        Elevation = br.ReadBD();
        ExtrusionZ = br.ReadBE().Z;
        int numOwned = br.ReadBL();
        if (numOwned < 0 || numOwned > 100000)
            throw new System.IO.EndOfStreamException();
        Kind = DwgEntityType.Polyline2D;
    }
}
