namespace DwgParser.Core.Compression;

public static class ReedSolomonAc21
{
    public const int N = 255;
    public const int DataBlockSize = 251;
    public const int DataPrimPoly = 0x11D;
    public const int SystemBlockSize = 239;
    public const int SystemPrimPoly = 0x169;

    public static byte[] Encode(byte[] data, int factor, int blockSize, int primPoly)
    {
        int nsym = N - blockSize;
        var gf = new GfTables(primPoly);
        var gen = GeneratorPoly(nsym, blockSize % N, gf);

        var padded = new byte[factor * blockSize];
        int copyLen = Math.Min(data.Length, padded.Length);
        Buffer.BlockCopy(data, 0, padded, 0, copyLen);

        var buffer = new byte[factor * N];
        for (int s = 0; s < factor; s++)
        {
            var codeword = EncodeBlock(padded, s * blockSize, blockSize, nsym, gen, gf);
            for (int i = 0; i < N; i++)
                buffer[i * factor + s] = codeword[i];
        }
        return buffer;
    }

    sealed class GfTables
    {
        public readonly byte[] Exp = new byte[510];
        public readonly byte[] Log = new byte[256];

        public GfTables(int primPoly)
        {
            ushort x = 1;
            for (int i = 0; i < 255; i++)
            {
                Exp[i] = (byte)x;
                Log[x] = (byte)i;
                x <<= 1;
                if ((x & 0x100) != 0) x ^= (ushort)primPoly;
            }
            for (int i = 255; i < 510; i++)
                Exp[i] = Exp[i - 255];
        }

        public byte Mul(byte a, byte b)
        {
            if (a == 0 || b == 0) return 0;
            return Exp[Log[a] + Log[b]];
        }
    }

    static byte[] GeneratorPoly(int nsym, int fcr, GfTables gf)
    {
        var gen = new byte[nsym + 1];
        gen[0] = 1;
        for (int i = 0; i < nsym; i++)
        {
            byte alphaI = gf.Exp[(fcr + i) % N];
            for (int j = i + 1; j >= 1; j--)
                gen[j] ^= gf.Mul(gen[j - 1], alphaI);
        }
        return gen;
    }

    static byte[] EncodeBlock(byte[] data, int dataStart, int k, int nsym, byte[] gen, GfTables gf)
    {
        var cw = new byte[N];
        for (int i = 0; i < k; i++)
        {
            int si = dataStart + i;
            cw[i] = si < data.Length ? data[si] : (byte)0;
        }

        for (int i = 0; i < k; i++)
        {
            byte feedback = (byte)(cw[i] ^ cw[k]);
            if (feedback != 0)
            {
                for (int j = 0; j < nsym - 1; j++)
                    cw[k + j] = (byte)(cw[k + j + 1] ^ gf.Mul(feedback, gen[j + 1]));
                cw[k + nsym - 1] = gf.Mul(feedback, gen[nsym]);
            }
            else
            {
                for (int j = 0; j < nsym - 1; j++)
                    cw[k + j] = cw[k + j + 1];
                cw[k + nsym - 1] = 0;
            }
        }
        return cw;
    }
}
