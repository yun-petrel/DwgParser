using DwgParser.Core.IO;

namespace DwgParser.Core.Objects;

public static partial class DwgObjectStreamWriter
{
    private static long SkipBlockHeaderToBasePt(byte[] src, long afterXref, long limit)
    {
        if (afterXref <= 0 || afterXref + 6 > limit) return -1;
        try
        {
            var br = new BitReader(src);
            DwgObjectRecord.SeekTo(br, afterXref);
            for (int i = 0; i < 5; i++) br.ReadB();
            br.ReadBL();
            br.Read3BD();
            long p = br.BitPosition;
            return p > afterXref && p <= limit ? p : -1;
        }
        catch (Exception ex) when (ex is IndexOutOfRangeException or EndOfStreamException
                                      or OverflowException or ArgumentException)
        {
            return -1;
        }
    }

    private static bool TryCarryBlockHeaderTailAc1018(BitWriter body, byte[] src,
        long from, long afterBase, long limit, long dataEnd, out long srcNext)
    {
        srcNext = afterBase;
        if (from < 0 || afterBase < from || afterBase + 8 > limit) return false;
        try
        {
            var mbr = new BitReader(src);
            DwgObjectRecord.SeekTo(mbr, afterBase);
            int numIns = 0;
            bool markersOk = true;
            while (true)
            {
                int mk = mbr.ReadRC();
                if (mk == 0) break;
                numIns++;
                if (mbr.BitPosition > limit || numIns > 0x4000) { markersOk = false; break; }
            }
            long markersEnd = mbr.BitPosition;
            if (!markersOk || numIns is < 0 or > 0x4000) return false;
            const int maxRem = 32;
            var cbr = new BitReader(src);
            DwgObjectRecord.SeekTo(cbr, markersEnd);
            long pvBytes = cbr.ReadBL();
            long pvStart = cbr.BitPosition;
            if (pvBytes < 0) return false;
            if (pvBytes > 0)
            {
                long rem = dataEnd - (pvStart + pvBytes * 8);
                if (rem < 0 || rem > maxRem) return false;
            }
            CopyBits(body, src, from, (int)(afterBase - from));
            body.WriteTV(string.Empty, false);
            CopyBits(body, src, afterBase, (int)(markersEnd - afterBase));
            body.WriteTV(string.Empty, false);
            if (pvBytes > 0)
            {
                body.WriteBL((int)pvBytes);
                CopyBits(body, src, pvStart, (int)(pvBytes * 8));
            }
            else body.WriteBL(0);
            srcNext = limit;
            return true;
        }
        catch (Exception ex) when (ex is IndexOutOfRangeException or EndOfStreamException
                                      or OverflowException or ArgumentException)
        {
            return false;
        }
    }
}
