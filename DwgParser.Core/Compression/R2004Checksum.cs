namespace DwgParser.Core.Compression;

public static class R2004Checksum
{
    private const int ChunkMax = 0x15B0;
    private const uint Mod = 0xFFF1;

    public static uint Compute(uint seed, ReadOnlySpan<byte> data, bool skipChecksum = false)
    {
        uint sum1 = seed & 0xFFFF;
        uint sum2 = seed >> 16;
        int pos = 0;

        while (pos < data.Length)
        {
            int chunk = Math.Min(data.Length - pos, ChunkMax);
            for (int i = 0; i < chunk; i++, pos++)
            {
                if (!skipChecksum || pos < 16 || pos >= 20)
                    sum1 += data[pos];
                sum2 += sum1;
            }
            sum1 %= Mod;
            sum2 %= Mod;
        }
        return (sum2 << 16) | (sum1 & 0xFFFF);
    }
}
