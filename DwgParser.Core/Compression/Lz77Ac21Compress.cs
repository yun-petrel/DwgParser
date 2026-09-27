namespace DwgParser.Core.Compression;

public static class Lz77Ac21Compress
{
    const int HashSize = 1 << 15;
    const int HashMask = HashSize - 1;
    const int MinMatch = 3;
    const int MaxChain = 64;
    const int MaxOffset = 65536;
    const int WindowMask = MaxOffset - 1;

    static readonly byte[][] Reorder = BuildReorderTables();

    public static byte[] Compress(byte[] source)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        if (source.Length == 0) return new byte[] { 0x00 };

        var c = new Compressor(source);
        return c.Run();
    }

    static byte[][] BuildReorderTables()
    {
        var tables = new byte[32][];
        for (int n = 0; n < 32; n++)
            tables[n] = BuildInversePermutation(n);
        return tables;
    }

    static byte[] BuildInversePermutation(int n)
    {
        var f = new byte[32];

        void A1(int si, int di) => f[di] = (byte)si;
        void A2(int si, int di) { f[di] = (byte)(si + 1); f[di + 1] = (byte)si; }
        void A3(int si, int di) { f[di] = (byte)(si + 2); f[di + 1] = (byte)(si + 1); f[di + 2] = (byte)si; }
        void A4(int si, int di) { for (int i = 0; i < 4; i++) f[di + i] = (byte)(si + i); }
        void A8(int si, int di) { A4(si, di); A4(si + 4, di + 4); }
        void A16(int si, int di) { A8(si + 8, di); A8(si, di + 8); }

        switch (n)
        {
            case 0: break;
            case 1: A1(0, 0); break;
            case 2: A2(0, 0); break;
            case 3: A3(0, 0); break;
            case 4: A4(0, 0); break;
            case 5: A1(4, 0); A4(0, 1); break;
            case 6: A1(5, 0); A4(1, 1); A1(0, 5); break;
            case 7: A2(5, 0); A4(1, 2); A1(0, 6); break;
            case 8: A8(0, 0); break;
            case 9: A1(8, 0); A8(0, 1); break;
            case 10: A1(9, 0); A8(1, 1); A1(0, 9); break;
            case 11: A2(9, 0); A8(1, 2); A1(0, 10); break;
            case 12: A4(8, 0); A8(0, 4); break;
            case 13: A1(12, 0); A4(8, 1); A8(0, 5); break;
            case 14: A1(13, 0); A4(9, 1); A8(1, 5); A1(0, 13); break;
            case 15: A2(13, 0); A4(9, 2); A8(1, 6); A1(0, 14); break;
            case 16: A16(0, 0); break;
            case 17: A8(9, 0); A1(8, 8); A8(0, 9); break;
            case 18: A1(17, 0); A16(1, 1); A1(0, 17); break;
            case 19: A3(16, 0); A16(0, 3); break;
            case 20: A4(16, 0); A8(8, 4); A8(0, 12); break;
            case 21: A1(20, 0); A4(16, 1); A8(8, 5); A8(0, 13); break;
            case 22: A2(20, 0); A4(16, 2); A8(8, 6); A8(0, 14); break;
            case 23: A3(20, 0); A4(16, 3); A8(8, 7); A8(0, 15); break;
            case 24: A8(16, 0); A16(0, 8); break;
            case 25: A8(17, 0); A1(16, 8); A16(0, 9); break;
            case 26: A1(25, 0); A8(17, 1); A1(16, 9); A16(0, 10); break;
            case 27: A2(25, 0); A8(17, 2); A1(16, 10); A16(0, 11); break;
            case 28: A4(24, 0); A8(16, 4); A8(8, 12); A8(0, 20); break;
            case 29: A1(28, 0); A4(24, 1); A8(16, 5); A8(8, 13); A8(0, 21); break;
            case 30: A2(28, 0); A4(24, 2); A8(16, 6); A8(8, 14); A8(0, 22); break;
            case 31: A1(30, 0); A4(26, 1); A8(18, 5); A8(10, 13); A8(2, 21); A2(0, 29); break;
        }
        return f;
    }

    readonly struct MatchEncoding
    {
        public readonly byte B0, B1, B2, B3, B4;
        public readonly int Len;
        public MatchEncoding(byte b0, byte b1, byte b2, byte b3, byte b4, int len)
        { B0 = b0; B1 = b1; B2 = b2; B3 = b3; B4 = b4; Len = len; }
        public byte this[int i] => i == 0 ? B0 : i == 1 ? B1 : i == 2 ? B2 : i == 3 ? B3 : B4;
    }

    static MatchEncoding EncodeCompact(int length, int offset, int trail)
    {
        int o = offset - 1;
        return new MatchEncoding(
            (byte)((length << 4) | (o & 0x0F)),
            (byte)(((o >> 4) << 3) | (trail & 7)),
            0, 0, 0, 2);
    }

    static MatchEncoding EncodeShort(int length, int offset, int trail)
    {
        int o = offset - 1;
        return new MatchEncoding(
            (byte)(0x10 | ((length - 3) & 0x0F)),
            (byte)(o & 0xFF),
            (byte)((((o >> 8) & 0x1F) << 3) | (trail & 7)),
            0, 0, 3);
    }

    static MatchEncoding EncodeLong(int length, int offset, int trail)
    {
        int o = offset - 1;
        int lenBase = length - 19;
        int extra = lenBase >= 16 ? 0x80 : 0;
        return new MatchEncoding(
            (byte)(lenBase & 0x0F),
            (byte)(o & 0xFF),
            (byte)(extra | (((o >> 8) & 0x0F) << 3) | (trail & 7)),
            0, 0, 3);
    }

    static MatchEncoding EncodeExtended(int length, int offset, int trail)
    {
        return new MatchEncoding(
            (byte)(0x20 | (length & 7)),
            (byte)(offset & 0xFF),
            (byte)((offset >> 8) & 0xFF),
            (byte)((length & 0xF8) | (trail & 7)),
            0, 4);
    }

    static MatchEncoding EncodeExtendedLong(int length, int offset, int trail)
    {
        int o = offset - 1;
        int adj = length - 0x100;
        return new MatchEncoding(
            (byte)(0x28 | (adj & 7)),
            (byte)(o & 0xFF),
            (byte)((o >> 8) & 0xFF),
            (byte)((adj >> 3) & 0xFF),
            (byte)((((adj >> 11) & 0x1F) << 3) | (trail & 7)),
            5);
    }

    static MatchEncoding EncodeMatch(int length, int offset, int trail)
    {
        if (length >= 3 && length <= 14 && offset >= 1 && offset <= 512)
            return EncodeCompact(length, offset, trail);
        if (length >= 19 && length <= 50 && offset >= 1 && offset <= 4096)
            return EncodeLong(length, offset, trail);
        if (length >= 3 && length <= 18 && offset >= 1 && offset <= 8192)
            return EncodeShort(length, offset, trail);
        if (length <= 255 && offset <= 0xFFFF)
            return EncodeExtended(length, offset, trail);
        if (length >= 256 && offset >= 1 && offset <= 65536)
            return EncodeExtendedLong(length, offset, trail);
        return EncodeExtendedLong(Math.Min(length, 65791), Math.Max(1, Math.Min(offset, 65536)), trail);
    }

    static void EncodeLiteralLength(List<byte> dest, int length)
    {
        int basis = length - 8;
        if (basis < 0x0F)
        {
            dest.Add((byte)basis);
        }
        else
        {
            dest.Add(0x0F);
            int remaining = basis - 0x0F;
            if (remaining < 0xFF)
            {
                dest.Add((byte)remaining);
            }
            else
            {
                dest.Add(0xFF);
                remaining -= 0xFF;
                while (true)
                {
                    if (remaining < 0xFFFF)
                    {
                        dest.Add((byte)(remaining & 0xFF));
                        dest.Add((byte)((remaining >> 8) & 0xFF));
                        break;
                    }
                    dest.Add(0xFF);
                    dest.Add(0xFF);
                    remaining -= 0xFFFF;
                }
            }
        }
    }

    sealed class Compressor
    {
        readonly byte[] src;
        readonly List<byte> output = new();
        readonly int[] head = new int[HashSize];
        readonly int[] prev = new int[MaxOffset];

        public Compressor(byte[] source)
        {
            src = source;
            Array.Fill(head, -1);
            Array.Fill(prev, -1);
        }

        void InsertHash(int pos)
        {
            if (pos + 2 >= src.Length) return;
            int h = Hash3(pos);
            prev[pos & WindowMask] = head[h];
            head[h] = pos;
        }

        int Hash3(int pos) =>
            (src[pos] * 961 + src[pos + 1] * 31 + src[pos + 2]) & HashMask;

        (int len, int off) FindMatch(int pos)
        {
            if (pos + 2 >= src.Length) return (0, 0);

            int h = Hash3(pos);
            int chainPos = head[h];
            int bestLen = 0, bestOff = 0, chainCount = 0;
            int maxLen = src.Length - pos;
            int maxMatch = Math.Min(maxLen, 65791);

            while (chainPos >= 0 && chainCount < MaxChain)
            {
                int candidate = chainPos;
                int offset = pos - candidate;

                if (offset >= MaxOffset || offset == 0)
                {
                    chainPos = prev[candidate & WindowMask];
                    chainCount++;
                    continue;
                }

                if (bestLen >= MinMatch && pos + bestLen < src.Length &&
                    src[candidate + bestLen] != src[pos + bestLen])
                {
                    chainPos = prev[candidate & WindowMask];
                    chainCount++;
                    continue;
                }

                int len = 0;
                while (len < maxMatch && src[candidate + len] == src[pos + len])
                    len++;

                if (len > bestLen)
                {
                    bestLen = len;
                    bestOff = offset;
                    if (len == maxMatch) break;
                }

                chainPos = prev[candidate & WindowMask];
                chainCount++;
            }

            return bestLen < MinMatch ? (0, 0) : (bestLen, bestOff);
        }

        void EmitLiterals(int start, int count)
        {
            int baseIdx = output.Count;
            for (int i = 0; i < count; i++) output.Add(0);

            int offset = 0;
            while (offset + 32 <= count)
            {
                int di = baseIdx + offset, si = start + offset;
                for (int i = 0; i < 4; i++)
                {
                    output[di + i] = src[si + 24 + i];
                    output[di + 4 + i] = src[si + 28 + i];
                    output[di + 8 + i] = src[si + 16 + i];
                    output[di + 12 + i] = src[si + 20 + i];
                    output[di + 16 + i] = src[si + 8 + i];
                    output[di + 20 + i] = src[si + 12 + i];
                    output[di + 24 + i] = src[si + i];
                    output[di + 28 + i] = src[si + 4 + i];
                }
                offset += 32;
            }

            int remaining = count - offset;
            if (remaining > 0)
            {
                var table = Reorder[remaining];
                for (int d = 0; d < remaining; d++)
                    output[baseIdx + offset + table[d]] = src[start + offset + d];
            }
        }

        void EmitTrailingLiterals(int start, int count)
        {
            var table = Reorder[count];
            int baseIdx = output.Count;
            for (int i = 0; i < count; i++) output.Add(0);
            for (int d = 0; d < count; d++)
                output[baseIdx + table[d]] = src[start + d];
        }

        void EmitPaddedLiteral(int start, int count)
        {
            int paddedLen = Math.Max(count, 8);
            EncodeLiteralLength(output, paddedLen);
            int baseIdx = output.Count;
            for (int i = 0; i < paddedLen; i++) output.Add(0);
            var table = Reorder[paddedLen];
            for (int d = 0; d < count; d++)
                output[baseIdx + table[d]] = src[start + d];
        }

        void EmitMatch(int length, int offset)
        {
            var enc = EncodeMatch(length, offset, 0);
            for (int i = 0; i < enc.Len; i++) output.Add(enc[i]);
        }

        void EmitChainedMatch(int length, int offset)
        {
            var enc = EncodeMatch(length, offset, 0);
            if ((enc.B0 >> 4) == 0)
                enc = EncodeExtended(length, offset, 0);
            for (int i = 0; i < enc.Len; i++) output.Add(enc[i]);
        }

        void PatchTrailingLiteral(int count)
        {
            output[^1] = (byte)((output[^1] & 0xF8) | (count & 7));
        }

        int FindFirstMatchAfterMinLiteral(int start)
        {
            int srcLen = src.Length;
            if (srcLen <= 8) return srcLen;

            for (int i = start; i < start + Math.Min(8, srcLen); i++)
                InsertHash(i);

            int pos = start + 8;
            while (pos + 2 < srcLen)
            {
                InsertHash(pos);
                var (ml, _) = FindMatch(pos);
                if (ml >= MinMatch) return pos;
                pos++;
            }
            return srcLen;
        }

        public byte[] Run()
        {
            int srcLen = src.Length;

            if (srcLen < 8)
            {
                EncodeLiteralLength(output, 8);
                for (int i = 0; i < 8; i++) output.Add(i < srcLen ? src[i] : (byte)0);
                return output.ToArray();
            }

            int pos = 0;
            bool lastWasMatch = false;

            int literalEnd = FindFirstMatchAfterMinLiteral(0);
            int literalCount = Math.Min(literalEnd, srcLen);
            EncodeLiteralLength(output, literalCount);
            EmitLiterals(0, literalCount);
            pos = literalCount;

            while (pos < srcLen)
            {
                var (matchLen, matchOff) = FindMatch(pos);

                if (matchLen < MinMatch || pos + matchLen > srcLen)
                {
                    int literalStart = pos;
                    InsertHash(pos);
                    pos++;
                    while (pos < srcLen)
                    {
                        var (ml, _) = FindMatch(pos);
                        if (ml >= MinMatch && pos - literalStart >= 8) break;
                        InsertHash(pos);
                        pos++;
                    }

                    int litLen = pos - literalStart;
                    if (litLen >= 8)
                    {
                        EncodeLiteralLength(output, litLen);
                        EmitLiterals(literalStart, litLen);
                        lastWasMatch = false;
                    }
                    else if (pos >= srcLen && lastWasMatch && litLen <= 7)
                    {
                        PatchTrailingLiteral(litLen);
                        EmitTrailingLiterals(literalStart, litLen);
                    }
                    else if (pos >= srcLen)
                    {
                        EmitPaddedLiteral(literalStart, litLen);
                        lastWasMatch = false;
                    }
                    else
                    {
                        EmitPaddedLiteral(literalStart, litLen);
                        lastWasMatch = false;
                    }
                    continue;
                }

                EmitMatch(matchLen, matchOff);
                lastWasMatch = true;
                int end = pos + matchLen;
                for (int i = pos; i < end; i++) InsertHash(i);
                pos = end;

                while (true)
                {
                    if (pos >= srcLen) break;

                    var (nextLen, nextOff) = FindMatch(pos);
                    if (nextLen >= MinMatch)
                    {
                        EmitChainedMatch(nextLen, nextOff);
                        int e = pos + nextLen;
                        for (int i = pos; i < e; i++) InsertHash(i);
                        pos = e;
                        continue;
                    }

                    bool foundAfterGap = false;
                    for (int gap = 1; gap <= 7 && pos + gap < srcLen; gap++)
                    {
                        var (gl, go) = FindMatch(pos + gap);
                        if (gl >= MinMatch)
                        {
                            PatchTrailingLiteral(gap);
                            EmitTrailingLiterals(pos, gap);
                            for (int i = pos; i < pos + gap; i++) InsertHash(i);
                            pos += gap;
                            EmitMatch(gl, go);
                            int e = pos + gl;
                            for (int i = pos; i < e; i++) InsertHash(i);
                            pos = e;
                            foundAfterGap = true;
                            break;
                        }
                    }

                    if (!foundAfterGap)
                    {
                        int remaining = srcLen - pos;
                        if (remaining > 0 && remaining <= 7)
                        {
                            PatchTrailingLiteral(remaining);
                            EmitTrailingLiterals(pos, remaining);
                            for (int i = pos; i < srcLen; i++) InsertHash(i);
                            pos = srcLen;
                        }
                        break;
                    }
                }
            }

            return output.ToArray();
        }
    }
}
