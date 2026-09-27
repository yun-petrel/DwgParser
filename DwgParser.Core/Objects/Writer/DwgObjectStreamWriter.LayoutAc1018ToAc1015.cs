using DwgParser.Core.IO;

namespace DwgParser.Core.Objects;

public static partial class DwgObjectStreamWriter
{
    private static bool CarriesLayoutAc1018ToAc1015(DwgObjectRecord rec, Target t)
        => t.Version == DwgVersion.AC1015
           && rec.SourceVersion == DwgVersion.AC1018
           && rec.LogicalType == TypeLayout
           && rec.Layout is { Captured: true }
           && rec.HasHandleStream && rec.HandleStreamStartBit > 0;

    private static bool CarryLayoutAc1018ToAc1015(BitWriter body, DwgObjectRecord rec,
        byte[] src, out BitWriter? handles)
        => TryLayoutAc1015FromAc1018Form(body, src, rec.CarryStartBit + 32,
               rec.HandleStreamStartBit, rec.Handle, (long)rec.BodyEnd * 8, out handles);

    private static bool TryLayoutAc1015FromAc1018Form(BitWriter body, byte[] src,
        long start, long hs, ulong handle, long bodyEndBit, out BitWriter? handles)
    {
        handles = null;
        if (!TryLayoutDataAc1015FromAc1018Form(body, src, start, hs, handle,
                out int nr, out long dataEnd)) return false;
        if (!TryLayoutHandlesAc1015(src, hs, bodyEndBit, nr, out handles)) return false;
        _ = dataEnd;
        return true;
    }

    private static bool TryLayoutDataAc1015FromAc1018Form(BitWriter body, byte[] src,
        long start, long hs, ulong handle, out int nr, out long dataEnd)
    {
        nr = 0;
        dataEnd = 0;
        if (start < 0 || hs <= start) return false;
        try
        {
            var br = new BitReader(src, 0);
            DwgObjectRecord.SeekTo(br, start);
            br.ReadHandle();
            long eedStart = br.BitPosition;
            if (!SkipEedWriter(br)) return false;
            int eedLen = (int)(br.BitPosition - eedStart);
            nr = br.ReadBL();
            if (nr is < 0 or > 8) return false;
            long xdicBit = br.BitPosition;
            br.ReadB();

            br.ReadTV();
            br.ReadTV();
            br.ReadBS();
            for (int i = 0; i < 6; i++) br.ReadBD();
            br.ReadTV();
            br.ReadBD(); br.ReadBD();
            br.ReadBS(); br.ReadBS(); br.ReadBS();
            br.ReadBD(); br.ReadBD();
            br.ReadBD(); br.ReadBD();
            long puStart = br.BitPosition;
            br.ReadBD();
            br.ReadBD();
            br.ReadTV();
            br.ReadBS();
            br.ReadBD();
            br.ReadBD(); br.ReadBD();
            long shadeStart = br.BitPosition;
            br.ReadBS(); br.ReadBS(); br.ReadBS();
            long layoutStart = br.BitPosition;
            br.ReadTV();
            br.ReadBS(); br.ReadBS();
            br.Read3BD();
            br.Read2RD(); br.Read2RD();
            br.Read3BD(); br.Read3BD(); br.Read3BD();
            br.ReadBD();
            br.ReadBS();
            br.Read3BD(); br.Read3BD();
            long numVpStart = br.BitPosition;
            br.ReadBL();
            if (br.BitPosition != hs) return false;

            CopyBits(body, src, start, (int)(eedStart - start));
            CopyBits(body, src, eedStart, eedLen);
            CopyBits(body, src, eedStart + eedLen, (int)(xdicBit - eedStart - eedLen));
            CopyBits(body, src, xdicBit + 1, (int)(puStart - xdicBit - 1));
            body.WriteBS(0);
            CopyBits(body, src, puStart, (int)(shadeStart - puStart));
            CopyBits(body, src, layoutStart, (int)(numVpStart - layoutStart));
            dataEnd = body.BitPosition;

            if (DebugTrace)
                System.Console.Error.WriteLine(
                    $"LY18DN h={handle:X} 82→AC1015 nr={nr} 数据区 {hs - start}->{dataEnd - start - 1 - 30 - 2 + 2}位");
            return true;
        }
        catch { return false; }
    }

    private static bool TryLayoutHandlesAc1015(byte[] src, long hs, long bodyEndBit, int nr,
        out BitWriter? handles)
        => TryLayoutHandlesAc1015(src, hs, bodyEndBit, nr, false, out handles);

    private static bool TryLayoutHandlesAc1015(byte[] src, long hs, long bodyEndBit, int nr,
        bool hasShadeplotRef, out BitWriter? handles)
    {
        handles = null;
        if (hs <= 0 || bodyEndBit <= hs) return false;
        try
        {
            long hsEnd = LastMeaningfulHandleEnd(src, hs, bodyEndBit);
            if (hsEnd <= hs) return false;
            var ends = new List<long>(12);
            var codes = new List<int>(12);
            var vals = new List<ulong>(12);
            var hbr = new BitReader(src, 0);
            for (long p = hs; p + 8 <= hsEnd;)
            {
                DwgObjectRecord.SeekTo(hbr, p);
                int cc = hbr.ReadRC();
                int bits = 8 + (cc & 0xF) * 8;
                if (cc == 0 || p + bits > hsEnd) break;
                ulong val = 0;
                for (int i = 0; i < (cc & 0xF); i++) val = (val << 8) | hbr.ReadRC();
                ends.Add(p + bits);
                codes.Add(cc >> 4);
                vals.Add(val);
                p += bits;
            }
            int slot = 1 + nr;
            if (slot < codes.Count && codes[slot] == 3 && vals[slot] != 0)
                return TryLayoutHandlesAc1015_XdicPresent(src, hs, ends, codes, vals, slot, out handles);
            if (codes.Count > slot + 6 && slot >= 1
                && codes[slot] == 5 && vals[slot] == 0
                && codes[slot + 1] == 4 && vals[slot + 1] == 0
                && codes[slot + 2] == 4 && codes[slot + 3] == 4
                && codes[slot + 4] == 5 && codes[slot + 5] == 5)
            {
                var hwX = new BitWriter(16);
                CopyBits(hwX, src, hs, (int)(ends[slot - 1] - hs));
                hwX.WriteHandleRef(3, 0);
                CopyBits(hwX, src, ends[slot + 1], (int)(ends[slot + 5] - ends[slot + 1]));
                handles = hwX;
                if (DebugTrace)
                    System.Console.Error.WriteLine(
                        $"LY15XMH nr={nr} slot={slot} 源枚数={codes.Count} → {handles.BitPosition}位" +
                        $"（补 (3,0) + 丢 plotview/shadeplot + 丢尾 {codes.Count - (slot + 6)} 枚）");
                return true;
            }
            int want = hasShadeplotRef ? nr + 7 : nr + 6;
            if (codes.Count != want || slot >= codes.Count) return false;
            if (codes[slot] != 5 || vals[slot] != 0) return false;
            long tailFrom = ends[slot];
            if (hasShadeplotRef)
            {
                if (codes[slot + 1] != 4 || vals[slot + 1] != 0) return false;
                tailFrom = ends[slot + 1];
            }

            var hw = new BitWriter(16);
            CopyBits(hw, src, hs, (int)(ends[slot - 1] - hs));
            hw.WriteHandleRef(3, 0);
            CopyBits(hw, src, tailFrom, (int)(hsEnd - tailFrom));
            handles = hw;
            if (DebugTrace)
                System.Console.Error.WriteLine(
                    $"LY15HS nr={nr} slot={slot} 枚数={codes.Count} shadeplot={hasShadeplotRef} " +
                    $"@{hs}->{hsEnd} (5,0)->(3,0)");
            return true;
        }
        catch { return false; }
    }

    private static bool TryLayoutHandlesAc1015_XdicPresent(byte[] src, long hs, List<long> ends,
        List<int> codes, List<ulong> vals, int slot, out BitWriter? handles)
    {
        handles = null;
        if (slot + 6 >= codes.Count) return false;
        if (codes[slot + 1] != 5 || vals[slot + 1] != 0) return false;
        if (codes[slot + 2] != 4 || vals[slot + 2] != 0) return false;
        var hw = new BitWriter(16);
        CopyBits(hw, src, hs, (int)(ends[slot] - hs));
        CopyBits(hw, src, ends[slot + 2], (int)(ends[slot + 6] - ends[slot + 2]));
        handles = hw;
        if (DebugTrace)
            System.Console.Error.WriteLine(
                $"LY15XDH slot={slot} 源枚数={codes.Count} → {handles!.BitPosition}位（丢 plotview+shadeplot+尾 morehandle）");
        return true;
    }
}
