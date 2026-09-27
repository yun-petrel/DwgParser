using DwgParser.Core.IO;

namespace DwgParser.Core.Format;

public static class R2000ObjectMap
{
    public static byte[] BuildObjectMap(IReadOnlyList<(ulong Handle, long Offset)> pairs)
    {
        var ordered = pairs
            .GroupBy(p => p.Handle)
            .Select(g => g.First())
            .OrderBy(p => p.Handle)
            .ToList();

        using var ms = new MemoryStream();
        int i = 0;

        var page = new MemoryStream();
        while (i < ordered.Count)
        {
            page.SetLength(0);
            ulong lastHandle = 0;
            long lastOffset = 0;
            while (i < ordered.Count)
            {
                ulong handleDelta = ordered[i].Handle - lastHandle;
                long offsetDelta = ordered[i].Offset - lastOffset;

                int need = UmcSize(handleDelta) + McSize(offsetDelta);
                if (page.Length + need > 2036)
                    break;

                WriteUmc(page, handleDelta);
                WriteMc(page, offsetDelta);
                lastHandle = ordered[i].Handle;
                lastOffset = ordered[i].Offset;
                i++;
            }

            var records = page.ToArray();
            int pageSize = records.Length + 2;
            ms.WriteByte((byte)(pageSize >> 8));
            ms.WriteByte((byte)pageSize);
            ms.Write(records, 0, records.Length);
            var crcBuf = new byte[2 + records.Length];
            crcBuf[0] = (byte)(pageSize >> 8);
            crcBuf[1] = (byte)pageSize;
            Buffer.BlockCopy(records, 0, crcBuf, 2, records.Length);
            ushort crc = Crc.Compute16Dwg(crcBuf, 0, crcBuf.Length);
            ms.WriteByte((byte)(crc >> 8));
            ms.WriteByte((byte)crc);
        }

        ms.WriteByte(0x00);
        ms.WriteByte(0x02);
        ushort termCrc = Crc.Compute16Dwg([0x00, 0x02], 0, 2);
        ms.WriteByte((byte)(termCrc >> 8));
        ms.WriteByte((byte)termCrc);
        return ms.ToArray();
    }

    private static int UmcSize(ulong value)
    {
        int size = 1;
        while (value >= 0x80) { value >>= 7; size++; }
        return size;
    }

    private static int McSize(long value)
    {
        ulong magnitude = (ulong)(value < 0 ? -value : value);
        int L = magnitude == 0 ? 0 : System.Numerics.BitOperations.Log2(magnitude) + 1;
        return L <= 6 ? 1 : 1 + (L + 6) / 7;
    }

    internal static void WriteUmc(MemoryStream ms, ulong value)
    {
        var bytes = new List<byte>();
        ulong v = value;
        do
        {
            byte b = (byte)(v & 0x7F);
            v >>= 7;
            if (v != 0) b |= 0x80;
            bytes.Add(b);
        } while (v != 0);
        foreach (var b in bytes)
            ms.WriteByte(b);
    }

    internal static void WriteMc(MemoryStream ms, long value)
    {
        bool negative = value < 0;
        ulong magnitude = (ulong)(negative ? -value : value);

        int L = magnitude == 0 ? 0 : System.Numerics.BitOperations.Log2(magnitude) + 1;
        int n = L <= 6 ? 1 : 1 + (L + 6) / 7;

        var parts = new byte[n];
        ulong m = magnitude;
        for (int j = 0; j < n; j++)
        {
            int w = j < n - 1 ? 7 : 6;
            parts[j] = (byte)(m & ((1UL << w) - 1));
            m >>= w;
        }
        for (int j = 0; j < n - 1; j++)
            parts[j] |= 0x80;
        if (negative)
            parts[n - 1] |= 0x40;
        foreach (var b in parts)
            ms.WriteByte(b);
    }

    internal static ulong ReadUmc(byte[] data, ref int pos)
    {
        ulong result = 0;
        for (int shift = 0; shift <= 49; shift += 7)
        {
            if (pos >= data.Length)
                throw new EndOfStreamException();
            byte b = data[pos++];
            int boundedShift = Math.Min(shift, 24);
            result |= (ulong)(b & 0x7FUL) << boundedShift;
            if ((b & 0x80) == 0)
                return result;
        }
        return result;
    }

    internal static long ReadMc(byte[] data, ref int pos)
    {
        uint result = 0;
        bool negative = false;
        for (int shift = 0; shift <= 28; shift += 7)
        {
            if (pos >= data.Length)
                throw new EndOfStreamException();
            byte b = data[pos++];
            if ((b & 0x80) == 0)
            {
                if ((b & 0x40) != 0)
                {
                    negative = true;
                    b &= 0xBF;
                }
                result |= (uint)b << shift;
                return negative ? unchecked(-(long)result) : result;
            }
            result |= (uint)(b & 0x7F) << shift;
        }
        return unchecked((int)result);
    }
}
