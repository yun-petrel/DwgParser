using DwgParser.Core.IO;

namespace DwgParser.Core.Objects;

internal static class SolidBodyReader
{
    public static void Read(BitReader br, DwgEntity e)
    {
        e.Thickness = br.ReadBT();
        e.Elevation = br.ReadBD();
        for (int i = 0; i < 4; i++)
        {
            var c = br.Read2RD();
            e.Corners.Add(new Vec2(c.X, c.Y));
        }
        e.ExtrusionZ = br.ReadBE().Z;
    }
}
