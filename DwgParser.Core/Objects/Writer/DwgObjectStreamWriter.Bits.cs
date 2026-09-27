using DwgParser.Core.IO;

namespace DwgParser.Core.Objects;

public static partial class DwgObjectStreamWriter
{
    internal static bool PrefixHasRl(DwgVersion v)
        => v >= DwgVersion.AC1015 && !v.IsR2010Plus();

    private static long FindAfterNumReactors(DwgObjectRecord rec, byte[] src, bool prefixHasRl,
                                             out uint srcRl, out uint numReactors)
    {
        srcRl = 0;
        numReactors = 0;
        int stage = 0;
        try
        {
            var br = new BitReader(src, 0);
            DwgObjectRecord.SeekTo(br, rec.CarryStartBit);
            if (prefixHasRl) srcRl = br.ReadRL();
            stage = 1;
            br.ReadHandle();
            stage = 2;
            if (!DwgObjectReader.TrySkipEed(br)) { Fail(rec, stage); return -1; }
            stage = 3;
            numReactors = (uint)br.ReadBL();
            return br.BitPosition;
        }
        catch (Exception ex)
        {
            Fail(rec, stage, ex.GetType().Name);
            srcRl = 0;
            return -1;
        }
    }

    private static bool TryMeasureEedRange(byte[] src, DwgObjectRecord rec,
        out long eedStart, out int eedBitLen)
    {
        eedStart = -1;
        eedBitLen = 0;
        try
        {
            var br = new BitReader(src, 0);
            DwgObjectRecord.SeekTo(br, rec.CarryStartBit);
            if (PrefixHasRl(rec.SourceVersion)) br.ReadRL();
            br.ReadHandle();
            eedStart = br.BitPosition;
            if (!DwgObjectReader.TrySkipEed(br)) { eedStart = -1; return false; }
            eedBitLen = (int)(br.BitPosition - eedStart);
            return true;
        }
        catch
        {
            eedStart = -1;
            eedBitLen = 0;
            return false;
        }
    }

    private static void Fail(DwgObjectRecord rec, int stage, string? ex = null)
    {
        if (System.Environment.GetEnvironmentVariable("DWGDBG") == "1")
            System.Console.Error.WriteLine($"  FAIL h={rec.Handle:X} t={rec.TypeCode} stage={stage} {ex}");
    }

    private static uint ReadRlAt(byte[] src, long bitPos)
    {
        var br = new BitReader(src, 0);
        br.SetPosition((int)(bitPos / 8), (int)(bitPos % 8));
        return br.ReadRL();
    }

    internal static bool ReadSrcBit(byte[] src, long bitPos)
    {
        int idx = (int)(bitPos >> 3);
        if (idx < 0 || idx >= src.Length) return false;
        int shift = 7 - (int)(bitPos & 7);
        return ((src[idx] >> shift) & 1) != 0;
    }

    internal static long LastMeaningfulHandleEnd(byte[] src, long startBit, long endBit)
    {
        long pos = startBit;
        long lastMeaningful = startBit;
        while (pos + 8 <= endBit)
        {
            int code = 0;
            for (int k = 0; k < 4; k++) code = (code << 1) | (ReadSrcBit(src, pos + k) ? 1 : 0);
            int counter = 0;
            for (int k = 0; k < 4; k++) counter = (counter << 1) | (ReadSrcBit(src, pos + 4 + k) ? 1 : 0);
            long refEnd = pos + 8 + counter * 8L;
            if (refEnd > endBit)
                break;
            if (code != 0 || counter != 0)
                lastMeaningful = refEnd;
            pos = refEnd;
        }
        return lastMeaningful;
    }

    internal static (int Count, long EndBit) WalkMeaningfulHandleRefs(byte[] src, long startBit, long endBit)
    {
        long pos = startBit;
        long last = startBit;
        int count = 0;
        while (pos + 8 <= endBit)
        {
            int code = 0;
            for (int k = 0; k < 4; k++) code = (code << 1) | (ReadSrcBit(src, pos + k) ? 1 : 0);
            int counter = 0;
            for (int k = 0; k < 4; k++) counter = (counter << 1) | (ReadSrcBit(src, pos + 4 + k) ? 1 : 0);
            long refEnd = pos + 8 + counter * 8L;
            if (refEnd > endBit)
                break;
            if (code != 0 || counter != 0) { count++; last = refEnd; }
            pos = refEnd;
        }
        return (count, last);
    }

    private static long LocateHandleRef(byte[] src, long from, long end, int index)
    {
        if (index <= 0 || from < 0) return -1;
        try
        {
            var br = new BitReader(src, 0);
            br.SetPosition((int)(from / 8), (int)(from % 8));
            long pos = from;
            for (int k = 0; k < index; k++)
            {
                if (br.BitPosition + 8 > (long)src.Length * 8 || br.BitPosition >= end) return -1;
                pos = br.BitPosition;
                br.ReadHandle();
            }
            return pos;
        }
        catch { return -1; }
    }

    private static bool RefIsNullXdic(byte[] src, long bit)
    {
        if (bit < 0 || bit + 8 > (long)src.Length * 8) return false;
        try
        {
            var br = new BitReader(src, 0);
            br.SetPosition((int)(bit / 8), (int)(bit % 8));
            byte codeAndCounter = br.ReadRC();
            int counter = codeAndCounter & 0x0F;
            return counter == 0;
        }
        catch { return false; }
    }

    internal static void CopyBits(BitWriter dst, byte[] src, long startBit, int bitLen)
    {
        long available = (long)src.Length * 8 - startBit;
        bitLen = Math.Min(bitLen, (int)Math.Max(available, 0));
        if (bitLen <= 0 || startBit < 0) return;
        var br = new BitReader(src, 0);
        br.SetPosition((int)(startBit / 8), (int)(startBit % 8));
        dst.WriteBitsFrom(br, bitLen);
    }

    private static void CopyBitsInsertingOne(BitWriter dst, byte[] src, long startBit, long endBit,
        long insertOneAt, bool insertOneValue, int insertCount = 1,
        IReadOnlyList<BitInsert>? extraInserts = null)
    {
        if (extraInserts is { Count: > 0 })
        {
            long pos = startBit;
            foreach (var ins in extraInserts)
            {
                if (ins.At < pos || ins.At > endBit) continue;
                CopyBits(dst, src, pos, (int)(ins.At - pos));
                WriteBitString(dst, ins.Bits);
                pos = ins.At;
            }
            CopyBitsInsertingOne(dst, src, pos, endBit, insertOneAt, insertOneValue, insertCount);
            return;
        }
        if (insertOneAt > startBit && insertOneAt <= endBit)
        {
            CopyBits(dst, src, startBit, (int)(insertOneAt - startBit));
            for (int i = 0; i < Math.Max(insertCount, 1); i++) dst.WriteB(insertOneValue);
            CopyBits(dst, src, insertOneAt, (int)(endBit - insertOneAt));
            return;
        }
        CopyBits(dst, src, startBit, (int)(endBit - startBit));
    }

    internal readonly struct BitInsert
    {
        public readonly long At;
        public readonly string Bits;
        public BitInsert(long at, string bits) { At = at; Bits = bits; }
    }

    private static void WriteBitString(BitWriter dst, string bits)
    {
        foreach (char c in bits) dst.WriteB(c == '1');
    }

    private static void CopyBitsWithMle(BitWriter dst, byte[] src, long startBit, long endBit,
        long skipBit, long insertBit)
    {
        if (insertBit > startBit && insertBit < endBit)
        {
            CopyBits(dst, src, startBit, (int)(insertBit - startBit));
            dst.WriteB(false);
            CopyBits(dst, src, insertBit, (int)(endBit - insertBit));
        }
        else if (skipBit > startBit && skipBit < endBit)
        {
            CopyBits(dst, src, startBit, (int)(skipBit - startBit));
            CopyBits(dst, src, skipBit + 1, (int)(endBit - skipBit - 1));
        }
        else
        {
            CopyBits(dst, src, startBit, (int)(endBit - startBit));
        }
    }

    internal static void Append(BitWriter dst, BitWriter from)
    {
        int bits = (int)from.BitPosition;
        if (bits == 0) return;
        dst.WriteBitsFrom(new BitReader(from.ToArray()), bits);
    }

    private static void PadToByte(BitWriter w, long writtenBits, uint size,
        DwgObjectRecord rec, byte[] src, Target t, bool reencode)
    {
        int rem = (int)(writtenBits % 8);
        if (rem == 0) return;
        int pad = 8 - rem;
        long padStart = (long)rec.BodyEnd * 8 - pad;
        if (size == rec.SizeBytes && CarriesSourceFrameCrc(rec, src, t, reencode)
            && padStart >= 0)
        {
            CopyBits(w, src, padStart, pad);
            return;
        }
        w.SkipBits(pad);
    }
}
