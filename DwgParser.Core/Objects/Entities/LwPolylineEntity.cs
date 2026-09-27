using System;
using System.IO;
using DwgParser.Core.IO;
using DwgParser.Core.Render;

namespace DwgParser.Core.Objects;

public abstract class LwPolylineEntity : DwgEntity, ILwPolyline
{

    public void ReadBody(BitReader br)
    {
        short flag = br.ReadBS();
        Closed = (flag & 512) != 0;
        PlineGen = (flag & 256) != 0;
        if ((flag & 4) != 0) ConstantWidth = br.ReadBD();
        if ((flag & 8) != 0) Elevation = br.ReadBD();
        if ((flag & 2) != 0) Thickness = br.ReadBD();
        if ((flag & 1) != 0) ExtrusionZ = br.Read3BD().Z;
        int numPoints = br.ReadBL();
        int numBulges = (flag & 16) != 0 ? br.ReadBL() : 0;
        int numWidths = (flag & 32) != 0 ? br.ReadBL() : 0;
        if (numPoints < 0 || numPoints > 20000 || numBulges < 0 || numWidths < 0)
            throw new EndOfStreamException();
        double px = 0, py = 0;
        for (int i = 0; i < numPoints; i++)
        {
            var (x, y) = ReadVertex(br, px, py, i == 0);
            Points.Add(new Vec2(x, y));
            px = x;
            py = y;
        }
        for (int i = 0; i < Math.Min(numBulges, numPoints); i++)
            Bulges.Add(br.ReadBD());
        bool uniformWidth = true;
        for (int i = 0; i < numWidths; i++)
        {
            double sw = br.ReadBD();
            double ew = br.ReadBD();
            VertexStartWidths.Add(sw);
            VertexEndWidths.Add(ew);
            if (i == 0 && (flag & 4) == 0) { ConstantWidth = sw; }
            else if (sw != ConstantWidth || ew != ConstantWidth) uniformWidth = false;
        }
        if ((flag & 4) == 0 && (numWidths == 0 || !uniformWidth)) ConstantWidth = 0;
        Kind = DwgEntityType.LwPolyline;
    }

    protected abstract (double X, double Y) ReadVertex(BitReader br, double prevX, double prevY, bool isFirst);
}
