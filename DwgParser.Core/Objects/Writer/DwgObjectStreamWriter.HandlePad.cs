using DwgParser.Core.IO;

namespace DwgParser.Core.Objects;

public static partial class DwgObjectStreamWriter
{
    internal static bool SourceXdicRefIsNull(byte[] src, long hsStartBit, long hsEndBit)
    {
        long hp = hsStartBit, end = hsEndBit;
        if (hp <= 0 || end <= hp) return false;
        var br = new BitReader(src);
        while (hp + 8 <= end)
        {
            DwgObjectRecord.SeekTo(br, hp);
            int cc = br.ReadRC();
            int bits = 8 + (cc & 0xF) * 8;
            if (hp + bits > end) break;
            if ((cc >> 4) == 3)
            {
                bool nonZero = false;
                for (int i = 8; i < bits && !nonZero; i++)
                    if (ReadSrcBit(src, hp + i)) nonZero = true;
                if (!nonZero) return true;
            }
            hp += bits;
        }
        return false;
    }

    private static void CarryUnmodelledHandleTail(BitWriter w, byte[] src, DwgObjectRecord rec)
    {
        long from = rec.HandleWalkEndBit, dataEnd = (long)rec.BodyEnd * 8;
        if (from <= 0 || dataEnd <= from || from < rec.HandleStreamStartBit) return;
        long endEff = LastMeaningfulHandleEnd(src, rec.HandleStreamStartBit, dataEnd);
        if (endEff > from) CopyBits(w, src, from, (int)(endEff - from));
    }

    internal static long TrimPreR2004BodyHandlePad(byte[] src, DwgObjectRecord rec, Target t,
        long srcRlValue, long from, long bodyEndBit)
    {
        if (!((t.Version == DwgVersion.AC1018 && rec.SourceVersion < DwgVersion.AC1018)
              || (t.Version == DwgVersion.AC1015
                  && (rec.SourceVersion == DwgVersion.AC1018 && rec.LogicalType is 0x32 or 0x40
                      || rec.SourceVersion >= DwgVersion.AC1021)))) return bodyEndBit;
        if (rec.Kind == DwgObjectRecordKind.Entity) return bodyEndBit;
        if (rec.HasHandleStream) return bodyEndBit;
        if (srcRlValue == 0 || srcRlValue > (long)rec.BodyEnd * 8) return bodyEndBit;
        long dataEnd = (long)rec.BodyStart * 8 + srcRlValue;
        if (dataEnd <= from || dataEnd >= bodyEndBit) return bodyEndBit;
        long meaningful = LastMeaningfulHandleEnd(src, dataEnd, bodyEndBit);
        if (meaningful <= dataEnd || meaningful >= bodyEndBit) return bodyEndBit;
        if (bodyEndBit - meaningful < 8)
        {
            if (DebugTrace)
                System.Console.Error.WriteLine(
                    $"PADTRIM7 h={rec.Handle:X} t={rec.TypeCode} 裁源帧尾对齐位 {bodyEndBit - meaningful} 位"
                    + $"（子引用残段，值不必为零）");
            return meaningful;
        }
        for (long b = meaningful; b < bodyEndBit; b++)
            if (ReadSrcBit(src, b)) return bodyEndBit;
        if (DebugTrace)
            System.Console.Error.WriteLine(
                $"PADTRIM h={rec.Handle:X} t={rec.TypeCode} 裁源句柄区尾垫 {bodyEndBit - meaningful} 位"
                + $"（hs {bodyEndBit - dataEnd}→{meaningful - dataEnd} 位）");
        return meaningful;
    }

    internal static bool TryCopyRegionStrippingNullXdic(BitWriter body, byte[] src, long from, long to,
        DwgObjectRecord rec)
    {
        if (rec.HasHandleStream || rec.Kind == DwgObjectRecordKind.Entity) return false;
        if (!TrySourceDataEnd(src, rec, out long dataEnd)) return false;
        if (dataEnd < from || dataEnd >= to) return false;
        long stop = LastMeaningfulHandleEnd(src, dataEnd, to);
        if (stop <= dataEnd || stop > to) return false;

        var br = new BitReader(src);
        long p = dataEnd, nullStart = -1, nullEnd = -1;
        int code3 = 0;
        while (p + 8 <= stop)
        {
            DwgObjectRecord.SeekTo(br, p);
            int cc = br.ReadRC();
            int code = cc >> 4, cnt = cc & 0xF;
            long next = p + 8 + cnt * 8L;
            if (next > stop) return false;
            if (code == 3)
            {
                code3++;
                bool allZero = true;
                for (long b = p + 8; b < next && allZero; b++) if (ReadSrcBit(src, b)) allZero = false;
                if (allZero) { nullStart = p; nullEnd = next; }
            }
            p = next;
        }
        if (code3 != 1 || nullStart < 0) return false;

        CopyBits(body, src, from, (int)(nullStart - from));
        CopyBits(body, src, nullEnd, (int)(stop - nullEnd));
        if (DebugTrace)
            System.Console.Error.WriteLine(
                $"XDNULLSTRIP h={rec.Handle:X} t={rec.TypeCode} 剥空 xdic 槽 (3,0) {nullEnd - nullStart} 位"
                + $" + 尾 {to - stop} 位（句柄区 {to - dataEnd}→{stop - nullEnd} 位）");
        return true;
    }

    private static bool FoldBlockHeaderOwned(BitWriter handles, byte[] src, long from, long to,
        DwgObjectRecord rec)
    {
        if (to <= from) return false;
        var br = new BitReader(src);
        var sb = new BitWriter(16);
        long hp = from;
        int prevCode = 3;
        bool folded = false, skipLast = false;
        while (hp + 8 <= to)
        {
            DwgObjectRecord.SeekTo(br, hp);
            int cc = br.ReadRC();
            if (cc == 0) break;
            int bits = 8 + (cc & 0xF) * 8;
            if (hp + bits > to) break;
            int code = cc >> 4;
            if (!folded && code == 4 && prevCode == 3)
            {
                foreach (var oh in rec.BlockOwnedHandles!)
                    sb.WriteHandleRef(3, oh);
                folded = true;
                skipLast = true;
                prevCode = code;
                hp += bits;
                continue;
            }
            if (skipLast && code == 4)
            {
                skipLast = false;
                prevCode = code;
                hp += bits;
                continue;
            }
            prevCode = code;
            CopyBits(sb, src, hp, bits);
            hp += bits;
        }
        if (!folded) return false;
        Append(handles, sb);
        if (DebugTrace)
            System.Console.Error.WriteLine(
                $"BHFOFOLD h={rec.Handle:X} t={rec.TypeCode} n={rec.BlockOwnedHandles!.Count}"
                + $" 尾段 {to - from}→{sb.BitPosition} 位");
        return true;
    }

    private static bool CarryBlockHeaderOwnedWithHandles(BitWriter body, byte[] src,
        long from, long to, long insertAt, long hsStart, DwgObjectRecord rec, out int blBits)
    {
        blBits = 0;
        var owned = rec.BlockOwnedHandles!;
        var tmp = new BitWriter(16);
        CopyBits(tmp, src, from, (int)(insertAt - from));
        long blAt = tmp.BitPosition;
        tmp.WriteBL(owned.Count);
        blBits = (int)(tmp.BitPosition - blAt);
        CopyBits(tmp, src, insertAt, (int)(hsStart - insertAt));
        var br = new BitReader(src);
        var starts = new List<long>();
        var ends = new List<long>();
        var codes = new List<int>();
        for (long p = hsStart; p + 8 <= to; )
        {
            DwgObjectRecord.SeekTo(br, p);
            int cc = br.ReadRC();
            int bits = 8 + (cc & 0xF) * 8;
            if (cc == 0 || p + bits > to) break;
            starts.Add(p);
            ends.Add(p + bits);
            codes.Add(cc >> 4);
            p += bits;
        }
        int first = -1;
        for (int i = 1; i < codes.Count; i++)
            if (codes[i] == 4 && codes[i - 1] == 3) { first = i; break; }
        if (first < 0 || first + 1 >= codes.Count || codes[first + 1] != 4) return false;
        CopyBits(tmp, src, hsStart, (int)(starts[first] - hsStart));
        foreach (var oh in owned)
            tmp.WriteHandleRef(3, oh);
        long tailEnd = ends.Count > 0 ? ends[^1] : ends[first + 1];
        if (tailEnd < ends[first + 1]) tailEnd = ends[first + 1];
        CopyBits(tmp, src, ends[first + 1], (int)(tailEnd - ends[first + 1]));
        if (DebugTrace && tailEnd != to)
            System.Console.Error.WriteLine(
                $"BHNOPLAD h={rec.Handle:X} t={rec.TypeCode} 尾段止于最后引用之末 {tailEnd}"
                + $"（不搬源帧垫 {to - tailEnd} 位）");
        Append(body, tmp);
        if (DebugTrace)
            System.Console.Error.WriteLine(
                $"BHNOWNED18HS h={rec.Handle:X} t={rec.TypeCode} n={owned.Count}"
                + $" 句柄区就地折叠 @{starts[first]}（+(4,·)×2 → [3]×{owned.Count}）");
        return true;
    }
}
