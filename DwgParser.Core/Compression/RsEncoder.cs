namespace DwgParser.Core.Compression;

public static partial class RsEncoder
{
    public static byte[] Store(byte[] data)
    {
        if (data.Length < 4)
            throw new ArgumentOutOfRangeException(nameof(data),
                $"RS store 无法编码 {data.Length} 字节（需 ≥ 4）。");

        var outLen = HeaderBytes(data.Length) + data.Length + 1;
        var result = new byte[outLen];
        int w = 0;

        if (data.Length <= 18)
        {
            result[w++] = (byte)(data.Length - 3);
        }
        else
        {
            result[w++] = 0x00;
            int r = data.Length - 0x12;
            while (r > 0xFF)
            {
                result[w++] = 0x00;
                r -= 0xFF;
            }
            result[w++] = (byte)r;
        }

        Buffer.BlockCopy(data, 0, result, w, data.Length);
        w += data.Length;
        result[w] = 0x11;
        return result;
    }

    private static int HeaderBytes(int len)
    {
        if (len <= 18) return 1;
        int bytes = 1;
        int r = len - 0x12;
        while (r > 0xFF)
        {
            bytes++;
            r -= 0xFF;
        }
        return bytes + 1;
    }

    public static int StoredSize(int decompressedSize)
    {
        if (decompressedSize < 4)
            throw new ArgumentOutOfRangeException(nameof(decompressedSize));
        return HeaderBytes(decompressedSize) + decompressedSize + 1;
    }
}
