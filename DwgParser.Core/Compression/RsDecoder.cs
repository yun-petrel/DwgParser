using System.Text;

namespace DwgParser.Core.Compression;

public static class RsDecoder
{
    public const int MaxReasonableSize = 256 * 1024 * 1024;

    public static bool TryDecompress(byte[] source, int sourceOffset, int sourceCount,
        int decompressedSize, out byte[] output)
    {
        output = Array.Empty<byte>();
        if (decompressedSize < 0 || decompressedSize > MaxReasonableSize)
            return false;
        if (sourceOffset < 0 || sourceCount < 0 || sourceOffset + sourceCount > source.Length)
            return false;

        var dst = new byte[decompressedSize];
        bool ok = DecodeCore(source, sourceOffset, sourceOffset + sourceCount, dst);
        if (!ok)
            return false;
        output = dst;
        return true;
    }

    public static bool TryDecompressSection(byte[] source, int sourceOffset, int sourceCount,
        int maxOutputSize, out byte[] output, out int produced)
    {
        output = Array.Empty<byte>();
        produced = 0;
        if (maxOutputSize < 0 || maxOutputSize > MaxReasonableSize)
            return false;
        if (sourceOffset < 0 || sourceCount < 0 || sourceOffset + sourceCount > source.Length)
            return false;

        var dst = new byte[Math.Max(maxOutputSize, 0)];
        int op = 0;
        bool ok = DecodeCoreInner(source, sourceOffset, sourceOffset + sourceCount, dst, ref op);
        produced = op;
        if (!ok && produced == 0)
            return false;
        output = dst;
        return true;
    }

    public static byte[] DecodeLenient(byte[] source, int offset, int maxLength)
    {
        int budget = Math.Min(maxLength, MaxReasonableSize);
        var dst = new byte[Math.Max(budget, 0)];
        try
        {
            DecodeCore(source, offset, source.Length, dst);
        }
        catch (Exception ex) when (ex is IndexOutOfRangeException or EndOfStreamException)
        {
        }
        return dst;
    }

    private static bool DecodeCore(byte[] src, int ip, int ipEnd, byte[] dst)
    {
        int op = 0;
        var ok = DecodeCoreInner(src, ip, ipEnd, dst, ref op);
        return ok && op == dst.Length;
    }

    private static bool DecodeCoreInner(byte[] src, int ip, int ipEnd, byte[] dst, ref int op)
    {
        int limit = dst.Length;
        if (ip >= ipEnd || limit == 0)
            return true;

        int opcode = src[ip++];
        if ((opcode & 0xF0) == 0)
            opcode = CopyLiterals(ReadLiteralLength(opcode, src, ref ip), src, ref ip, dst, ref op);

        while (ip < ipEnd && op < limit && opcode != 0x11)
        {
            int matchBytes;
            int matchDistance;
            if (opcode < 0x10 || opcode >= 0x40)
            {
                matchBytes = (opcode >> 4) - 1;
                if (ip >= ipEnd) return false;
                int b2 = src[ip++];
                matchDistance = (((opcode >> 2) & 3) | (b2 << 2)) + 1;
            }
            else if (opcode < 0x20)
            {
                matchBytes = ReadExtendedCount(opcode & 7, 7, src, ref ip) + 2;
                matchDistance = (opcode & 8) << 11;
                opcode = ReadTwoByteOffset(src, ref ip, 0x4000, ref matchDistance);
            }
            else
            {
                matchBytes = ReadExtendedCount(opcode & 0x1F, 0x1F, src, ref ip) + 2;
                matchDistance = 0;
                opcode = ReadTwoByteOffset(src, ref ip, 1, ref matchDistance);
            }

            if (matchBytes < 0 || matchDistance > op || op + matchBytes > limit)
                return false;

            int srcPos = op - matchDistance;
            for (int i = 0; i < matchBytes; i++)
                dst[op + i] = dst[srcPos + i];
            op += matchBytes;

            int litLen = opcode & 3;
            if (litLen == 0)
            {
                if (ip >= ipEnd)
                    break;
                opcode = src[ip++];
                if ((opcode & 0xF0) == 0)
                    litLen = ReadLiteralLength(opcode, src, ref ip);
            }

            if (litLen > 0)
            {
                if (op + litLen > limit)
                    break;
                opcode = CopyLiterals(litLen, src, ref ip, dst, ref op);
            }
        }

        return true;
    }

    private static int ReadLiteralLength(int opcode, byte[] src, ref int ip)
    {
        int lowBits = opcode & 0xF;
        if (lowBits == 0)
        {
            byte last = 0;
            while (ip < src.Length && (last = src[ip++]) == 0)
                lowBits += 0xFF;
            lowBits += 0xF + last;
        }
        return lowBits + 3;
    }

    private static int ReadExtendedCount(int initial, int addBits, byte[] src, ref int ip)
    {
        int count = initial & addBits;
        if (count == 0)
        {
            byte last = 0;
            while (ip < src.Length && (last = src[ip++]) == 0)
                count += 0xFF;
            count += last + addBits;
        }
        return count;
    }

    private static int ReadTwoByteOffset(byte[] src, ref int ip, int plus, ref int distance)
    {
        int first = src[ip++];
        int second = src[ip++];
        distance |= first >> 2;
        distance |= second << 6;
        distance += plus;
        return first;
    }

    private static int CopyLiterals(int count, byte[] src, ref int ip, byte[] dst, ref int op)
    {
        int limit = dst.Length;
        int srcEnd = src.Length;
        if (count > 0)
        {
            int n = count;
            if (ip + n > srcEnd) n = srcEnd - ip;
            if (op + n > limit) n = limit - op;
            if (n > 0)
            {
                Buffer.BlockCopy(src, ip, dst, op, n);
                ip += n;
                op += n;
            }
            else if (ip >= srcEnd)
            {
                int skip = count;
                int avail = srcEnd - ip;
                ip += Math.Min(skip, avail);
            }
        }
        return ip < srcEnd ? src[ip++] : (byte)0x11;
    }
}