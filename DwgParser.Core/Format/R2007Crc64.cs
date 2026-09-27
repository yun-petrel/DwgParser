namespace DwgParser.Core.Format;

public static class R2007Crc64
{
    const ulong PolyNormal = 0x42F0E1EBA9EA3693UL;
    const ulong PolyMirrored = 0x95AC9329AC4BC9B5UL;

    static readonly ulong[] TableNormal = BuildTableNormal();
    static readonly ulong[] TableMirrored = BuildTableMirrored();

    static ulong[] BuildTableNormal()
    {
        var t = new ulong[256];
        for (ulong i = 0; i < 256; i++)
        {
            ulong c = i << 56;
            for (int k = 0; k < 8; k++)
                c = (c & 0x8000000000000000UL) != 0 ? (c << 1) ^ PolyNormal : c << 1;
            t[i] = c;
        }
        return t;
    }

    static ulong[] BuildTableMirrored()
    {
        var t = new ulong[256];
        for (ulong i = 0; i < 256; i++)
        {
            ulong c = i;
            for (int k = 0; k < 8; k++)
                c = (c & 1) != 0 ? (c >> 1) ^ PolyMirrored : c >> 1;
            t[i] = c;
        }
        return t;
    }

    static ulong Normal(ulong crc, byte[] data)
    {
        foreach (byte b in data)
        {
            int idx = (int)((b ^ (crc >> 56)) & 0xFF);
            crc = TableNormal[idx] ^ (crc << 8);
        }
        return crc;
    }

    static ulong Mirrored(ulong crc, byte[] data)
    {
        foreach (byte b in data)
        {
            int idx = (int)((crc ^ b) & 0xFF);
            crc = (crc >> 8) ^ TableMirrored[idx];
        }
        return crc;
    }

    public static byte[] ReorderForCrc(byte[] data)
    {
        var result = new byte[data.Length];
        int fullBlocks = data.Length / 8;
        for (int b = 0; b < fullBlocks; b++)
        {
            int o = b * 8;
            result[o] = data[o + 6];
            result[o + 1] = data[o + 7];
            result[o + 2] = data[o + 4];
            result[o + 3] = data[o + 5];
            result[o + 4] = data[o + 2];
            result[o + 5] = data[o + 3];
            result[o + 6] = data[o];
            result[o + 7] = data[o + 1];
        }
        int baseIdx = fullBlocks * 8;
        int rem = data.Length - baseIdx;
        if (rem >= 4)
        {
            result[baseIdx] = data[baseIdx + 2];
            result[baseIdx + 1] = data[baseIdx + 3];
            result[baseIdx + 2] = data[baseIdx];
            result[baseIdx + 3] = data[baseIdx + 1];
            for (int j = 4; j < rem; j++)
                result[baseIdx + j] = data[baseIdx + j];
        }
        else
        {
            for (int j = 0; j < rem; j++)
                result[baseIdx + j] = data[baseIdx + j];
        }
        return result;
    }

    public static ulong UpdateSeed2(ulong initialSeed, uint dataLength)
    {
        ulong len = dataLength;
        ulong seed = (initialSeed + len) * 0x343FDUL + 0x269EC3UL;
        seed = seed * 0x1000343FDUL + (len + 0x269EC3UL);
        return ~seed;
    }

    public static ulong UpdateSeed1(ulong initialSeed, uint dataLength)
    {
        ulong len = dataLength;
        ulong seed = (initialSeed + len) * 0x343FDUL + 0x269EC3UL;
        seed |= seed * (0x343FDUL << 32) + (0x269EC3UL << 32);
        return ~seed;
    }

    public static ulong NormalCrc64(ulong seed, uint dataLength, byte[] data)
        => ~Normal(UpdateSeed2(seed, dataLength), ReorderForCrc(data));

    public static ulong NormalCrc64Seed1(ulong seed, uint dataLength, byte[] data)
        => ~Normal(UpdateSeed1(seed, dataLength), ReorderForCrc(data));

    public static ulong MirroredCrc64(ulong seed, uint dataLength, byte[] data)
        => Mirrored(UpdateSeed1(seed, dataLength), ReorderForCrc(data));

    public static ulong CheckDataNormalCrc64(ulong random2, byte[] data)
        => ~Normal(~random2, ReorderForCrc(data));

    public static ulong CheckDataMirroredCrc64(ulong random1, byte[] data)
        => Mirrored(~random1, ReorderForCrc(data));

    public static ulong HeaderCrc64(byte[] metadata)
    {
        var buf = new byte[0x110];
        int copy = Math.Min(metadata.Length, 0x110);
        Buffer.BlockCopy(metadata, 0, buf, 0, copy);
        Array.Clear(buf, 0x108, 8);
        return ~Normal(UpdateSeed2(0, 0x110), ReorderForCrc(buf));
    }

    public static uint PageChecksum(uint seed, byte[] data)
    {
        ulong seed64 = ((ulong)seed + (ulong)data.Length) * 0x343FDUL + 0x269EC3UL;
        uint sum1 = (uint)(seed64 & 0xFFFF);
        uint sum2 = (uint)((seed64 >> 16) & 0xFFFF);

        const int ChunkSize = 0x15B0;
        const uint Modulus = 0xFFF1;

        int offset = 0;
        while (offset < data.Length)
        {
            int chunkEnd = Math.Min(offset + ChunkSize, data.Length);

            int i = offset;
            while (i + 8 <= chunkEnd)
            {
                sum1 += data[i + 6]; sum2 += sum1;
                sum1 += data[i + 7]; sum2 += sum1;
                sum1 += data[i + 4]; sum2 += sum1;
                sum1 += data[i + 5]; sum2 += sum1;
                sum1 += data[i + 2]; sum2 += sum1;
                sum1 += data[i + 3]; sum2 += sum1;
                sum1 += data[i]; sum2 += sum1;
                sum1 += data[i + 1]; sum2 += sum1;
                i += 8;
            }

            int remaining = chunkEnd - i;
            if (remaining >= 4)
            {
                sum1 += data[i + 2]; sum2 += sum1;
                sum1 += data[i + 3]; sum2 += sum1;
                sum1 += data[i]; sum2 += sum1;
                sum1 += data[i + 1]; sum2 += sum1;
                for (int j = 4; j < remaining; j++)
                {
                    sum1 += data[i + j]; sum2 += sum1;
                }
            }
            else
            {
                for (int j = 0; j < remaining; j++)
                {
                    sum1 += data[i + j]; sum2 += sum1;
                }
            }

            sum1 %= Modulus;
            sum2 %= Modulus;
            offset = chunkEnd;
        }

        return (sum2 << 16) | (sum1 & 0xFFFF);
    }
}

public sealed class CrcRandomEncoder
{
    readonly uint[] table = new uint[0x270];
    readonly uint[] padding = new uint[0x80];
    int index;

    public CrcRandomEncoder(ulong seed)
    {
        table[0] = (uint)seed * 0x343FDu + 0x269EC3u;
        table[1] = (uint)(seed >> 32) * 0x343FDu + 0x269EC3u;
        uint value = table[1];
        for (uint i = 2; i < 0x270; i++)
        {
            value = ((value >> 30) ^ value) * 0x6C078965u + i;
            table[i] = value;
        }
        for (int i = 0; i < 0x80; i++)
        {
            UpdateIndex();
            padding[i] = table[index];
            index++;
        }
    }

    void UpdateIndex()
    {
        if (index < 0x270) return;
        for (int i = 0; i < 0x270; i++)
        {
            uint y = (table[i] & 0x80000000u) | (table[(i + 1) % 0x270] & 0x7FFFFFFFu);
            table[i] = table[(i + 0x18D) % 0x270] ^ (y >> 1);
            if ((y & 1) != 0) table[i] ^= 0x9908B0DFu;
        }
        index = 0;
    }

    public ulong NextU64()
    {
        index += 2;
        UpdateIndex();
        ulong lo = table[index];
        ulong hi = table[index + 1];
        return lo | (hi << 32);
    }

    public ulong Encode(uint value)
    {
        ulong random = NextU64();
        uint lo = (uint)random & 0xDF7DF7DFu;
        uint hi = (uint)(random >> 32) & 0xF7DF7DF7u;

        if ((value & 0x200) != 0) lo |= 0x20u;
        if ((value & 0x100) != 0) lo |= 0x800u;
        if ((value & 0x080) != 0) lo |= 0x20000u;
        if ((value & 0x040) != 0) lo |= 0x800000u;
        if ((value & 0x020) != 0) lo |= 0x20000000u;
        if ((value & 0x010) != 0) hi |= 0x08u;
        if ((value & 0x008) != 0) hi |= 0x200u;
        if ((value & 0x004) != 0) hi |= 0x8000u;
        if ((value & 0x002) != 0) hi |= 0x200000u;
        if ((value & 0x001) != 0) hi |= 0x8000000u;

        return lo | ((ulong)hi << 32);
    }

    public ulong EncodeCrcSeed(ulong seed) => Encode((uint)seed);

    public void FillRandom(byte[] buffer)
    {
        int bufIdx = 0, padIdx = 0;
        while (bufIdx + 4 <= buffer.Length)
        {
            uint val;
            if (padIdx < padding.Length)
            {
                val = padding[padIdx++];
            }
            else
            {
                index++;
                UpdateIndex();
                val = table[index];
            }
            buffer[bufIdx++] = (byte)val;
            buffer[bufIdx++] = (byte)(val >> 8);
            buffer[bufIdx++] = (byte)(val >> 16);
            buffer[bufIdx++] = (byte)(val >> 24);
        }
        if (bufIdx < buffer.Length)
        {
            uint val;
            if (padIdx < padding.Length) val = padding[padIdx];
            else
            {
                index++;
                UpdateIndex();
                val = table[index];
            }
            var bytes = BitConverter.GetBytes(val);
            for (int j = 0; bufIdx < buffer.Length; j++)
                buffer[bufIdx++] = bytes[j];
        }
    }
}
