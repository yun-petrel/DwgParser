using DwgParser.Core.IO;

namespace DwgParser.Core.Objects;

public static partial class DwgObjectStreamWriter
{
    private const int EedDowngradeCodepage = 39;

    internal static bool TryCarryEedPreR2007(BitWriter body, byte[] src, long eedStart, long eedEnd,
        bool carryUnparseableTail = false)
    {
        try
        {
            var br = new BitReader(src);
            DwgObjectRecord.SeekTo(br, eedStart);
            while (br.BitPosition < eedEnd)
            {
                int size = br.ReadBS();
                if (size == 0) { body.WriteBS(0); break; }
                if (size < 0) return false;
                var h = br.ReadHandle();
                long payloadEnd = br.BitPosition + size * 8L;
                if (payloadEnd > eedEnd) return false;
                var tw = new BitWriter(16);
                bool ok = true;
                while (br.BitPosition < payloadEnd && ok)
                {
                    long subStart = br.BitPosition;
                    int code = br.ReadRC();
                    switch (code)
                    {
                        case 0:
                        {
                            if (br.BitPosition + 16 > payloadEnd) { ok = false; break; }
                            int units = br.ReadRS();
                            if (units < 0 || br.BitPosition + units * 16L > payloadEnd)
                            {
                                if (!carryUnparseableTail) { ok = false; break; }
                                CarryItemTailOpaque(tw, src, subStart, br, payloadEnd);
                                break;
                            }
                            var chars = new char[units];
                            for (int i = 0; i < units; i++) chars[i] = (char)br.ReadRS();
                            int n = units;
                            while (n > 0 && chars[n - 1] == '\0') n--;
                            string text = new string(chars, 0, n);
                            var bytes = EncodeCodepageText(text, EedDowngradeCodepage);
                            if (bytes.Length + 1 > 255) { ok = false; break; }
                            tw.WriteRC(0);
                            tw.WriteRC((byte)(bytes.Length + 1));
                            tw.WriteRC((byte)((EedDowngradeCodepage >> 8) & 0xFF));
                            tw.WriteRC((byte)(EedDowngradeCodepage & 0xFF));
                            foreach (var b in bytes) tw.WriteRC(b);
                            tw.WriteRC(0);
                            break;
                        }
                        case 1: ok = CopySub(tw, src, br, payloadEnd, 16); break;
                        case 2: ok = CopySub(tw, src, br, payloadEnd, 8); break;
                        case 3: ok = CopySub(tw, src, br, payloadEnd, 32); break;
                        case 4:
                        {
                            if (br.BitPosition + 8 > payloadEnd) { ok = false; break; }
                            int lenc = br.ReadRC();
                            if (subStart + 8 + lenc * 8L > payloadEnd) { ok = false; break; }
                            CopyBits(tw, src, subStart, 8 + lenc * 8);
                            DwgObjectRecord.SeekTo(br, subStart + 8 + lenc * 8);
                            break;
                        }
                        case 5: ok = CopySub(tw, src, br, payloadEnd, 32); break;
                        case 10 or 11 or 12 or 13 or 14 or 15:
                            ok = CopySub(tw, src, br, payloadEnd, 192); break;
                        case 40 or 41 or 42:
                            ok = CopySub(tw, src, br, payloadEnd, 64); break;
                        case 70: ok = CopySub(tw, src, br, payloadEnd, 16); break;
                        case 71: ok = CopySub(tw, src, br, payloadEnd, 32); break;
                        default: ok = false; break;
                    }
                }
                if (!ok || br.BitPosition != payloadEnd) return false;
                tw.AlignByte();
                int newSize = (int)(tw.BitPosition / 8);
                if (newSize > short.MaxValue) return false;
                body.WriteBS((short)newSize);
                body.WriteHandleRef(h.Code, h.Value);
                Append(body, tw);
            }
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void CarryItemTailOpaque(BitWriter tw, byte[] src, long groupStart,
        BitReader br, long payloadEnd)
    {
        long start = groupStart;
        if (payloadEnd > start)
            CopyBits(tw, src, start, (int)(payloadEnd - start));
        DwgObjectRecord.SeekTo(br, payloadEnd);
    }

    private static bool CopySub(BitWriter tw, byte[] src, BitReader br, long payloadEnd, int bits)
    {
        long subStart = br.BitPosition - 8;
        if (subStart + 8 + bits > payloadEnd) return false;
        CopyBits(tw, src, subStart, 8 + bits);
        DwgObjectRecord.SeekTo(br, subStart + 8 + bits);
        return true;
    }

    internal static bool TryWriteEedPreR2007Range(BitWriter body, byte[] src,
        long eedStart, int eedBitLen, bool carryUnparseableTail = false)
    {
        if (eedStart <= 0 || eedBitLen <= 2) return false;
        if (!TryCarryEedPreR2007(new BitWriter(16), src, eedStart, eedStart + eedBitLen,
                carryUnparseableTail))
            return false;
        return TryCarryEedPreR2007(body, src, eedStart, eedStart + eedBitLen, carryUnparseableTail);
    }

    internal static bool TryWriteEedPreR2007Range(BitWriter body, byte[] src,
        long eedStart, int eedBitLen, out bool failed, bool carryUnparseableTail = false)
    {
        failed = false;
        if (eedStart <= 0 || eedBitLen <= 2) return false;
        if (!TryCarryEedPreR2007(new BitWriter(16), src, eedStart, eedStart + eedBitLen,
                carryUnparseableTail))
        {
            failed = true;
            return false;
        }
        return TryCarryEedPreR2007(body, src, eedStart, eedStart + eedBitLen, carryUnparseableTail);
    }

    internal static bool TryWriteEedPreR2007ForEntity(BitWriter body, byte[] src,
        DwgObjectRecord rec, Target t, out bool failed, bool carryUnparseableTail = false)
    {
        failed = false;
        if (t.R2007Plus || rec.SourceVersion < DwgVersion.AC1021) return false;
        return TryWriteEedPreR2007Range(body, src, rec.EedStartBit, rec.EedBitLen, out failed,
            carryUnparseableTail);
    }

    internal static bool TryWriteEedPreR2007ForEntity(BitWriter body, byte[] src,
        DwgObjectRecord rec, Target t)
    {
        if (t.R2007Plus || rec.SourceVersion < DwgVersion.AC1021) return false;
        return TryWriteEedPreR2007Range(body, src, rec.EedStartBit, rec.EedBitLen);
    }

    private static byte[] EncodeCodepageText(string text, int cpLib)
    {
        if (text.Length == 0) return System.Array.Empty<byte>();
        int win = cpLib switch
        {
            30 => 1252,
            31 => 936,
            32 => 950,
            38 => 932,
            39 => 936,
            40 => 949,
            41 => 950,
            28 => 1250, 29 => 1251, 44 => 1258,
            25 => 949, 26 => 1361, 27 => 866,
            _ => 936,
        };
        try
        {
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
            return System.Text.Encoding.GetEncoding(win).GetBytes(text);
        }
        catch
        {
            return System.Text.Encoding.UTF8.GetBytes(text);
        }
    }
}
