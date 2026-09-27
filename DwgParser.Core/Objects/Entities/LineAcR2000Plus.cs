using DwgParser.Core.IO;

namespace DwgParser.Core.Objects;

internal sealed class LineAcR2000Plus : LineEntity
{
    public override void ReadGeometry(BitReader br)
    {
        bool zIsZero = br.ReadB();
        double sx = br.ReadRD();
        double ex = br.ReadDD(sx);
        double sy = br.ReadRD();
        double ey = br.ReadDD(sy);
        double sz = 0.0, ez = 0.0;
        if (!zIsZero)
        {
            sz = br.ReadRD();
            ez = br.ReadDD(sz);
        }
        Kind = DwgEntityType.Line;
        Start = new Vec2(sx, sy);
        End = new Vec2(ex, ey);
        StartZ = sz;
        EndZ = ez;
    }
}
