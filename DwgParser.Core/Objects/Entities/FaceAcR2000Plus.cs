using DwgParser.Core.IO;

namespace DwgParser.Core.Objects;

internal sealed class FaceAcR2000Plus : FaceEntity
{
    public override void ReadBody(BitReader br)
    {
        bool hasNoFlags = br.ReadB();
        bool zIsZero = br.ReadB();
        double x1 = br.ReadRD();
        double y1 = br.ReadRD();
        double z1 = 0.0;
        if (!zIsZero)
            z1 = br.ReadRD();
        CornerZIsZero = zIsZero;
        CornerZsList.Clear();
        CornerZsList.Add(z1);
        Corners.Add(new Vec2(x1, y1));
        for (int i = 1; i < 4; i++)
        {
            double cx = i == 1 ? x1 : Corners[^1].X;
            double cy = i == 1 ? y1 : Corners[^1].Y;
            double px = Corners[^1].X;
            double py = Corners[^1].Y;
            cx = br.ReadDD(px);
            cy = br.ReadDD(py);
            double cz = br.ReadDD(0.0);
            CornerZsList.Add(cz);
            Corners.Add(new Vec2(cx, cy));
        }
        if (!hasNoFlags)
            InvisFlags = br.ReadBS();
        Kind = DwgEntityType.Solid;
    }
}
