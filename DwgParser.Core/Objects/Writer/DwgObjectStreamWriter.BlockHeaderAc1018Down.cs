using DwgParser.Core.IO;

namespace DwgParser.Core.Objects;

public static partial class DwgObjectStreamWriter
{
    private static bool CarriesBlockHeaderFrameAc1018ToAc1015(DwgObjectRecord rec, Target t)
        => t.Version == DwgVersion.AC1015
           && rec.SourceVersion == DwgVersion.AC1018
           && rec.LogicalType == TypeBlockHeader
           && rec.BlockOwnedHandles is { } owned
           && rec.BlockOwnedSourceCount >= 0
           && owned.Count == rec.BlockOwnedSourceCount;

    private static bool CarryBlockHeaderFrameAc1018ToAc1015(BitWriter body, DwgObjectRecord rec,
        byte[] src, out BitWriter? handles)
    {
        handles = null;
        var owned = rec.BlockOwnedHandles!;
        long start = rec.CarryStartBit + 32;
        if (!TryBlockHeaderHandleStreamStart(rec, src, out long hs) || hs <= start) return false;
        try
        {
            var br = new BitReader(src, 0);
            DwgObjectRecord.SeekTo(br, start);
            br.ReadHandle();
            long eedStart = br.BitPosition;
            if (!SkipEedWriter(br)) return false;
            int eedLen = (int)(br.BitPosition - eedStart);
            int nr = br.ReadBL();
            if (nr is < 0 or > 8) return false;
            long xdicBit = br.BitPosition;
            bool xdicMissing = br.ReadB();
            br.ReadTV();
            br.ReadB(); _ = br.ReadBS(); br.ReadB();
            br.ReadB(); br.ReadB(); br.ReadB(); br.ReadB();
            br.ReadB();
            long nOwnStart = br.BitPosition;
            int numOwned = br.ReadBL();
            long nOwnEnd = br.BitPosition;
            if (numOwned != owned.Count) return false;
            br.Read3BD();
            br.ReadTV();
            for (int i = 0; ; i++)
            {
                if (i > 0xF000) return false;
                if (br.ReadRC() == 0) break;
            }
            br.ReadTV();
            int psz = (int)br.ReadBL();
            if (psz is < 0 or > 0xA00000) return false;
            for (int i = 0; i < psz; i++) _ = br.ReadRC();
            if (br.BitPosition != hs) return false;

            long hsEnd = LastMeaningfulHandleEnd(src, hs, (long)rec.BodyEnd * 8);
            if (hsEnd <= hs) return false;
            var ends = new List<long>(24);
            var codes = new List<int>(24);
            var hbr = new BitReader(src, 0);
            for (long p = hs; p + 8 <= hsEnd;)
            {
                DwgObjectRecord.SeekTo(hbr, p);
                int cc = hbr.ReadRC();
                int bits = 8 + (cc & 0xF) * 8;
                if (cc == 0 || p + bits > hsEnd) break;
                ends.Add(p + bits);
                codes.Add(cc >> 4);
                p += bits;
            }
            int entityIdx = -1;
            for (int i = 1; i < codes.Count; i++)
                if (codes[i] == 3 && codes[i - 1] == 5) { entityIdx = i; break; }
            if (entityIdx < 0 || nr >= codes.Count || entityIdx + owned.Count >= codes.Count)
                return false;
            for (int k = 0; k < owned.Count; k++)
                if (codes[entityIdx + 1 + k] != 3) return false;
            if (codes[entityIdx + owned.Count] != 3) return false;

            CopyBits(body, src, start, (int)(eedStart - start));
            if (!TryWriteEedPreR2007Range(body, src, eedStart, eedLen))
                CopyBits(body, src, eedStart, eedLen);
            body.WriteBL(nr);
            CopyBits(body, src, xdicBit + 1, (int)(nOwnStart - xdicBit - 1));
            CopyBits(body, src, nOwnEnd, (int)(hs - nOwnEnd));

            var hw = new BitWriter(16);
            long headEnd = ends[entityIdx];
            if (xdicMissing)
            {
                long insAt = ends[nr];
                CopyBits(hw, src, hs, (int)(insAt - hs));
                hw.WriteHandleRef(3, 0);
                CopyBits(hw, src, insAt, (int)(headEnd - insAt));
            }
            else
            {
                CopyBits(hw, src, hs, (int)(headEnd - hs));
            }
            hw.WriteHandleRef(4, owned.Count > 0 ? owned[0] : 0);
            hw.WriteHandleRef(4, owned.Count > 0 ? owned[^1] : 0);
            long tailFrom = ends[entityIdx + owned.Count];
            CopyBits(hw, src, tailFrom, (int)(hsEnd - tailFrom));
            handles = hw;

            if (DebugTrace)
                System.Console.Error.WriteLine(
                    $"BH18DN h={rec.Handle:X} t={rec.TypeCode} 49 AC1018→AC1015 owned={owned.Count} " +
                    $"nr={nr} xdicMiss={xdicMissing} 数据区 {hs - start}→{hs - start - 1 - (nOwnEnd - nOwnStart)}位 " +
                    $"body={body.BitPosition} hs={hw.BitPosition}@{hs}");
            return true;
        }
        catch { return false; }
    }
}
