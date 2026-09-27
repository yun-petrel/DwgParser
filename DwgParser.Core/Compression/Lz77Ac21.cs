namespace DwgParser.Core.Compression;

public static class Lz77Ac21
{
    public static bool TryDecompress(byte[] source, int offset, int count,
        int decompressedSize, out byte[] output)
        => TryDecompress(source, offset, count, decompressedSize, out output, out _);

    public static bool TryDecompress(byte[] source, int offset, int count,
        int decompressedSize, out byte[] output, out string? error)
    {
        output = Array.Empty<byte>();
        error = null;

        if (source is null || decompressedSize <= 0 || decompressedSize > RsDecoder.MaxReasonableSize)
        {
            error = "解压错误：目标尺寸非法";
            return false;
        }
        if (!TryResolveSource(source, offset, count, out int srcStart, out int srcEnd, ref error))
            return false;

        var dst = new byte[decompressedSize];
        int dp = 0;
        if (!DecompressCore(dst, decompressedSize, dst.Length, source, srcStart, srcEnd, ref dp, ref error))
            return false;

        if (dp != decompressedSize)
        {
            error = "解压错误：产出未恰好填满 size_uncomp";
            return false;
        }
        output = dst;
        return true;
    }

    public static int Decode(byte[] source, int offset, int count, int maxOutputSize, out byte[] output)
        => Decode(source, offset, count, maxOutputSize, out output, out _);

    public static int Decode(byte[] source, int offset, int count, int maxOutputSize,
        out byte[] output, out string? error)
    {
        output = Array.Empty<byte>();
        error = null;

        if (source is null || maxOutputSize < 0 || maxOutputSize > RsDecoder.MaxReasonableSize)
            return 0;
        if (!TryResolveSource(source, offset, count, out int srcStart, out int srcEnd, ref error))
            return 0;

        var dst = new byte[maxOutputSize];
        int dp = 0;
        DecompressCore(dst, maxOutputSize, dst.Length, source, srcStart, srcEnd, ref dp, ref error);
        output = dst;
        return dp;
    }

    private static bool TryResolveSource(byte[] source, int offset, int count,
        out int srcStart, out int srcEnd, ref string? error)
    {
        srcStart = offset;
        srcEnd = 0;
        if (offset < 0 || offset > source.Length)
        {
            error = "解压错误：源偏移越界";
            return false;
        }
        long stop = count < 0 ? source.Length : (long)offset + count;
        if (stop > source.Length)
        {
            error = "解压错误：源长度越界";
            return false;
        }
        srcEnd = (int)stop;
        return true;
    }

    private static bool DecompressCore(byte[] dst, int dstSize, int dstEnd,
        byte[] src, int sp, int srcEnd, ref int dp, ref string? error)
    {
        uint length = 0;
        uint offset = 0;

        if (dstSize == 0 || srcEnd - sp < 2)
        {
            error = "解压错误：参数非法（dst_size 为 0 或压缩流不足 2 字节）";
            return false;
        }

        byte opcode = src[sp++];
        if ((opcode & 0xf0) == 0x20)
        {
            if (srcEnd - sp < 3)
            {
                error = "解压错误：原文转义头越界";
                return false;
            }
            sp += 2;
            length = (uint)(src[sp++] & 0x07);
            if (length == 0)
            {
                error = "解压错误：零长度";
                return false;
            }
        }

        while (sp < srcEnd)
        {
            if (length == 0)
            {
                if (!ReadLiteralLength(src, ref sp, srcEnd, opcode, out length, ref error))
                    return false;
            }

            if ((long)dp + length > dstEnd || (long)sp + length > srcEnd)
            {
                error = "解压错误：长度溢出";
                return false;
            }

            dp = CopyCompressedBytes(src, sp, dst, dp, (int)length);
            sp += (int)length;
            length = 0;

            if (sp >= srcEnd)
                return true;

            opcode = src[sp++];
            if (!ReadInstructions(src, ref sp, srcEnd, ref opcode, ref offset, ref length, ref error))
                return false;

            while (true)
            {
                if ((long)dp + length > dstEnd)
                {
                    error = "解压错误：长度溢出";
                    return false;
                }
                if (offset > (uint)dp)
                {
                    error = "解压错误：偏移下溢";
                    return false;
                }

                int len = (int)length;
                int from = dp - (int)offset;
                for (int i = 0; i < len; i++)
                    dst[dp + i] = dst[from + i];

                dp += len;
                length = (uint)(opcode & 7);

                if (length != 0 || sp >= srcEnd)
                    break;

                opcode = src[sp++];
                if ((opcode >> 4) == 0)
                    break;
                if ((opcode >> 4) == 0x0f)
                    opcode &= 0xf;

                if (!ReadInstructions(src, ref sp, srcEnd, ref opcode, ref offset, ref length, ref error))
                    return false;
            }
        }
        return true;
    }

    private static bool ReadLiteralLength(byte[] src, ref int sp, int srcEnd,
        byte opcode, out uint length, ref string? error)
    {
        length = (uint)opcode + 8;

        if (length == 0x17)
        {
            if (sp >= srcEnd)
            {
                error = "解压错误：字面长度读越界";
                return false;
            }
            int n = src[sp++];
            length += (uint)n;

            if (n == 0xff)
            {
                do
                {
                    if (srcEnd - sp < 2)
                    {
                        error = "解压错误：字面长度读越界";
                        return false;
                    }
                    n = src[sp++];
                    n |= src[sp++] << 8;
                    length += (uint)n;
                }
                while (n == 0xFFFF);
            }
        }
        return true;
    }

    private static bool ReadInstructions(byte[] src, ref int sp, int srcEnd, ref byte opcode,
        ref uint offset, ref uint length, ref string? error)
    {
        switch (opcode >> 4)
        {
            case 0:
                length = (uint)((opcode & 0xf) + 0x13);
                if (srcEnd - sp < 2)
                    return FailInstruction(ref error);
                offset = src[sp++];
                opcode = src[sp++];
                length = (uint)(((opcode >> 3) & 0x10) + length);
                offset = (uint)((((opcode & 0x78) << 5) + 1) + offset);
                break;

            case 1:
                length = (uint)((opcode & 0xf) + 3);
                if (srcEnd - sp < 2)
                    return FailInstruction(ref error);
                offset = src[sp++];
                opcode = src[sp++];
                offset = (uint)((((opcode & 0xf8) << 5) + 1) + offset);
                break;

            case 2:
                if (srcEnd - sp < 2)
                    return FailInstruction(ref error);
                offset = src[sp++];
                offset = (((uint)src[sp++] << 8) & 0xff00u) | offset;
                length = (uint)(opcode & 7);

                if ((opcode & 8) == 0)
                {
                    if (srcEnd - sp < 1)
                        return FailInstruction(ref error);
                    opcode = src[sp++];
                    length = (uint)((opcode & 0xf8) + length);
                }
                else
                {
                    offset++;
                    if (srcEnd - sp < 1)
                        return FailInstruction(ref error);
                    length = (uint)((src[sp++] << 3) + length);
                    if (srcEnd - sp < 1)
                        return FailInstruction(ref error);
                    opcode = src[sp++];
                    length = (uint)((((opcode & 0xf8) << 8) + length) + 0x100);
                }
                break;

            default:
                length = (uint)(opcode >> 4);
                offset = (uint)(opcode & 15);
                if (srcEnd - sp < 1)
                    return FailInstruction(ref error);
                opcode = src[sp++];
                offset = (uint)((((opcode & 0xf8) << 1) + offset) + 1);
                break;
        }
        return true;
    }

    private static bool FailInstruction(ref string? error)
    {
        error = "解压错误：指令读越界";
        return false;
    }

    private static int CopyCompressedBytes(byte[] src, int sp, byte[] dst, int dp, int length)
    {
        void Copy1(int offset)
        {
            dst[dp] = src[sp + offset];
            dp++;
        }

        void Copy2(int offset)
        {
            dst[dp] = src[sp + offset + 1];
            dst[dp + 1] = src[sp + offset];
            dp += 2;
        }

        void Copy3(int offset)
        {
            dst[dp] = src[sp + offset + 2];
            dst[dp + 1] = src[sp + offset + 1];
            dst[dp + 2] = src[sp + offset];
            dp += 3;
        }

        void CopyN(int n, int offset)
        {
            Buffer.BlockCopy(src, sp + offset, dst, dp, n);
            dp += n;
        }

        void Copy4(int offset) => CopyN(4, offset);
        void Copy8(int offset) => CopyN(8, offset);

        void Copy16(int offset)
        {
            Buffer.BlockCopy(src, sp + offset + 8, dst, dp, 8);
            Buffer.BlockCopy(src, sp + offset, dst, dp + 8, 8);
            dp += 16;
        }

        while (length >= 32)
        {
            Copy16(16);
            Copy16(0);

            sp += 32;
            length -= 32;
        }

        switch (length)
        {
            case 0:
                break;
            case 1:
                Copy1(0);
                break;
            case 2:
                Copy2(0);
                break;
            case 3:
                Copy3(0);
                break;
            case 4:
                Copy4(0);
                break;
            case 5:
                Copy1(4);
                Copy4(0);
                break;
            case 6:
                Copy1(5);
                Copy4(1);
                Copy1(0);
                break;
            case 7:
                Copy2(5);
                Copy4(1);
                Copy1(0);
                break;
            case 8:
                Copy8(0);
                break;
            case 9:
                Copy1(8);
                Copy8(0);
                break;
            case 10:
                Copy1(9);
                Copy8(1);
                Copy1(0);
                break;
            case 11:
                Copy2(9);
                Copy8(1);
                Copy1(0);
                break;
            case 12:
                Copy4(8);
                Copy8(0);
                break;
            case 13:
                Copy1(12);
                Copy4(8);
                Copy8(0);
                break;
            case 14:
                Copy1(13);
                Copy4(9);
                Copy8(1);
                Copy1(0);
                break;
            case 15:
                Copy2(13);
                Copy4(9);
                Copy8(1);
                Copy1(0);
                break;
            case 16:
                Copy16(0);
                break;
            case 17:
                Copy8(9);
                Copy1(8);
                Copy8(0);
                break;
            case 18:
                Copy1(17);
                Copy16(1);
                Copy1(0);
                break;
            case 19:
                Copy3(16);
                Copy16(0);
                break;
            case 20:
                Copy4(16);
                Copy16(0);
                break;
            case 21:
                Copy1(20);
                Copy4(16);
                Copy16(0);
                break;
            case 22:
                Copy2(20);
                Copy4(16);
                Copy16(0);
                break;
            case 23:
                Copy3(20);
                Copy4(16);
                Copy16(0);
                break;
            case 24:
                Copy8(16);
                Copy16(0);
                break;
            case 25:
                Copy8(17);
                Copy1(16);
                Copy16(0);
                break;
            case 26:
                Copy1(25);
                Copy8(17);
                Copy1(16);
                Copy16(0);
                break;
            case 27:
                Copy2(25);
                Copy8(17);
                Copy1(16);
                Copy16(0);
                break;
            case 28:
                Copy4(24);
                Copy8(16);
                Copy16(0);
                break;
            case 29:
                Copy1(28);
                Copy4(24);
                Copy8(16);
                Copy16(0);
                break;
            case 30:
                Copy2(28);
                Copy4(24);
                Copy8(16);
                Copy16(0);
                break;
            case 31:
                Copy1(30);
                Copy4(26);
                Copy8(18);
                Copy16(2);
                Copy2(0);
                break;
            default:
                break;
        }

        return dp;
    }
}

