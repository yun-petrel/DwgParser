using System.Buffers.Binary;
using DwgParser.Core.Objects;

namespace DwgParser.Core.Format;

public static class AcisSab
{
    private static readonly byte[] SabMagic = "ACIS BinaryFile"u8.ToArray();

    private const byte TagInt = 0x0c;
    private const byte TagInt2 = 0x04;
    private const byte TagGeom = 0x13;
    private const byte TagStr = 0x07;
    private const byte TagName = 0x0d;
    private const byte TagEnd = 0x11;

    public static bool TryReadVertices(byte[]? sab, out Vec3[] vertices)
    {
        vertices = System.Array.Empty<Vec3>();
        try
        {
            var d = sab;
            if (d is null || d.Length < SabMagic.Length + 16) return false;

            var list = new List<Vec3>();
            int i = 0;
            while (i + 2 < d.Length)
            {
                if (d[i] != TagName) { i++; continue; }
                int len = d[i + 1];
                int nameStart = i + 2;
                if (nameStart + len > d.Length) break;
                if (len == 5 &&
                    d[nameStart] == 'p' && d[nameStart + 1] == 'o' && d[nameStart + 2] == 'i'
                    && d[nameStart + 3] == 'n' && d[nameStart + 4] == 't')
                {
                    if (TryReadPoint(d, nameStart + len, out var v))
                        list.Add(v);
                }
                i = nameStart + len;
            }

            if (list.Count == 0) return false;
            vertices = list.ToArray();
            return true;
        }
        catch (System.Exception ex) when (ex is not System.OperationCanceledException)
        {
            vertices = System.Array.Empty<Vec3>();
            return false;
        }
    }

    private static bool TryReadPoint(byte[] d, int bodyStart, out Vec3 v)
    {
        v = default;
        int end = bodyStart;
        while (end < d.Length && d[end] != TagEnd) end++;
        if (end >= d.Length) return false;

        int g = -1;
        for (int k = bodyStart; k < end; k++)
            if (d[k] == TagGeom) { if (g >= 0) return false; g = k; }
        if (g < 0) return false;
        if (g + 1 + 24 != end) return false;

        double x = System.BitConverter.Int64BitsToDouble((long)BinaryPrimitives.ReadUInt64LittleEndian(d.AsSpan(g + 1)));
        double y = System.BitConverter.Int64BitsToDouble((long)BinaryPrimitives.ReadUInt64LittleEndian(d.AsSpan(g + 9)));
        double z = System.BitConverter.Int64BitsToDouble((long)BinaryPrimitives.ReadUInt64LittleEndian(d.AsSpan(g + 17)));
        if (!double.IsFinite(x) || !double.IsFinite(y) || !double.IsFinite(z)) return false;

        v = new Vec3(x, y, z);
        return true;
    }
}
