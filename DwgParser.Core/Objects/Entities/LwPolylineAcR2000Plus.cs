using DwgParser.Core.IO;

namespace DwgParser.Core.Objects;

internal sealed class LwPolylineAcR2000Plus : LwPolylineEntity
{
    protected override (double X, double Y) ReadVertex(BitReader br, double prevX, double prevY, bool isFirst)
    {
        if (isFirst)
        {
            var first = br.Read2RD();
            return (first.X, first.Y);
        }
        return (br.ReadDD(prevX), br.ReadDD(prevY));
    }
}
