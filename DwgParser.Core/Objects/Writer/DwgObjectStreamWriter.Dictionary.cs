using DwgParser.Core.IO;

namespace DwgParser.Core.Objects;

internal static partial class DictionaryCarry
{
    internal const short TypeDictionaryLogical = 0x2A;

    internal const short TypeDictionaryVarLogical = 0x1F7;

    internal const short TypeDictWdfltLogical = 500;

    internal static bool CarriesAc1021Down(DwgObjectRecord rec, DwgObjectStreamWriter.Target t)
        => t.Version == DwgVersion.AC1018 && rec.SourceVersion == DwgVersion.AC1021
           && rec.LogicalType == TypeDictionaryLogical;

    internal static bool CarriesNoStringFrameAc1021Down(DwgObjectRecord rec,
        DwgObjectStreamWriter.Target t)
        => t.Version == DwgVersion.AC1018 && rec.SourceVersion == DwgVersion.AC1021
           && !rec.HasHandleStream;

    internal static bool CarryNoStringFrameAc1021Down(BitWriter body, DwgObjectRecord rec, byte[] src,
        out BitWriter? handles)
        => CarryCentralTailAc1021(body, rec, src, System.Array.Empty<string>(), 1, out handles);

    internal static bool CarryDictionaryAc1021Down(BitWriter body, DwgObjectRecord rec, byte[] src,
        out BitWriter? handles)
    {
        handles = null;
        int n = rec.DictionaryNumItems;
        if (n is < 0 or > 1_000_000 || rec.DictionaryEntries.Count != n) return false;
        var keys = new string[n];
        for (int i = 0; i < n; i++)
        {
            var k = rec.DictionaryEntries[i].Key;
            if (string.IsNullOrEmpty(k) || k.Length > 255) return false;
            keys[i] = k;
        }
        return CarryCentralTailAc1021(body, rec, src, keys, n + 1, out handles);
    }

    internal static bool CarriesCentralStringAc1021Down(DwgObjectRecord rec,
        DwgObjectStreamWriter.Target t)
        => t.Version == DwgVersion.AC1018 && rec.SourceVersion == DwgVersion.AC1021
           && (rec.LogicalType == TypeDictionaryVarLogical ? rec.DictVar is { Captured: true }
               : rec.LogicalType == TypeDictWdfltLogical && WdfltPayload(rec) is not null);

    internal static string CentralStringValue(DwgObjectRecord rec)
        => rec.DictVar is { Captured: true } v ? v.StrValue
           : rec.DictWdflt is { Captured: true } w ? w.EntryName : "";

    private static IReadOnlyList<string>? WdfltPayload(DwgObjectRecord rec)
    {
        var w = rec.DictWdflt;
        if (w is null || w.EntryNames.Count == 0) return null;
        if (!w.Captured && !w.ValuesCommitted) return null;
        foreach (var s in w.EntryNames) if (string.IsNullOrEmpty(s)) return null;
        return w.EntryNames;
    }

    internal static bool CarryCentralStringAc1021Down(BitWriter body, DwgObjectRecord rec, byte[] src,
        string value, out BitWriter? handles)
        => CarryCentralTailAc1021(body, rec, src,
            rec.LogicalType == TypeDictWdfltLogical && WdfltPayload(rec) is { } names
                ? names : new[] { value ?? "" }, 1, out handles);

    internal static long ScanOwnerReactorPrefix(byte[] hsBytes)
    {
        try
        {
            var br = new BitReader(hsBytes, 0);
            long p = 0, end = (long)hsBytes.Length * 8;
            int seen = 0;
            while (p + 8 <= end)
            {
                DwgObjectRecord.SeekTo(br, p);
                int cc = br.ReadRC();
                int code = cc >> 4, n = cc & 0xF;
                long nb = 8 + n * 8L;
                if (p + nb > end) break;
                if (code != 4) return seen > 0 ? p : -1;
                p += nb;
                seen++;
            }
            return -1;
        }
        catch { return -1; }
    }

    internal static long FindXdBitAc1021(DwgObjectRecord rec, byte[] src)
    {
        try
        {
            var br = new BitReader(src, 0);
            DwgObjectRecord.SeekTo(br, rec.CarryStartBit);
            br.ReadRL();
            br.ReadHandle();
            if (!DwgObjectReader.TrySkipEed(br)) return -1;
            br.ReadBL();
            return br.BitPosition;
        }
        catch { return -1; }
    }

    private static bool CarryCentralTailAc1021(BitWriter body, DwgObjectRecord rec, byte[] src,
        IReadOnlyList<string> expect, int minHsRefs, out BitWriter? handles)
    {
        handles = null;
        long start = rec.CarryStartBit;
        try
        {
            if (start <= 0 || !TryAc1021Spans(src, rec, out long dataEnd, out long ss,
                    out bool hasStrings)) return false;
            if (hasStrings)
            {
                var sr = new BitReader(src) { UnicodeStrings = true };
                DwgObjectRecord.SeekTo(sr, ss);
                for (int i = 0; i < expect.Count; i++)
                    if (sr.ReadTV() != expect[i]) return false;
                sr.ReadRS();
                if (!sr.ReadB() || sr.BitPosition != dataEnd) return false;
            }
            else if (expect.Count != 0) return false;

            if (!TryAc1021HandlesTail(rec, src, dataEnd, minHsRefs, out handles)) return false;

            DwgObjectStreamWriter.CopyHeadWindowWithEedDown(body, src, start + 32, ss, rec);
            for (int i = 0; i < expect.Count; i++) body.WriteTV(expect[i]);
            if (DwgObjectStreamWriter.DebugTrace)
                System.Console.Error.WriteLine(
                    $"C21DN h={rec.Handle:X} t={rec.TypeCode} AC1021→AC1018 截SS至{ss}" +
                    $"+{expect.Count}串TV out={body.BitPosition} hs={handles!.BitPosition}位@{dataEnd}");
            return true;
        }
        catch { return false; }
    }

    internal static bool CarriesCentralStringAc1015Down(DwgObjectRecord rec,
        DwgObjectStreamWriter.Target t)
        => t.Version == DwgVersion.AC1015 && rec.SourceVersion >= DwgVersion.AC1021
           && (rec.LogicalType == TypeDictionaryVarLogical ? rec.DictVar is { Captured: true }
               : rec.LogicalType == TypeDictWdfltLogical && WdfltPayload(rec) is not null);

    internal static bool CarryCentralStringAc1015Down(BitWriter body, DwgObjectRecord rec, byte[] src,
        out BitWriter? handles)
    {
        handles = null;
        long start = rec.CarryStartBit;
        try
        {
            if (start <= 0 || !TryAc1021Spans(src, rec, out long dataEnd, out long ss,
                    out bool hasStrings)) return false;
            IReadOnlyList<string> values =
                rec.LogicalType == TypeDictWdfltLogical && WdfltPayload(rec) is { } col
                    ? col : new[] { CentralStringValue(rec) };
            if (hasStrings)
            {
                var sr = new BitReader(src) { UnicodeStrings = true };
                DwgObjectRecord.SeekTo(sr, ss);
                foreach (var v in values)
                    if (sr.ReadTV() != v) return false;
                sr.ReadRS();
                if (!sr.ReadB() || sr.BitPosition != dataEnd) return false;
            }
            else if (values.Count != 1 || !string.IsNullOrEmpty(values[0])) return false;

            long xdicBit = FindXdBitAc1021(rec, src);
            if (xdicBit <= start + 32 || xdicBit >= ss) return false;
            bool srcXdicMissing = DwgObjectStreamWriter.ReadSrcBit(src, xdicBit);
            if (!srcXdicMissing) return false;

            if (!TryAc1021HandlesTail(rec, src, dataEnd, 1, out handles)) return false;

            DwgObjectStreamWriter.CopyBits(body, src, start + 32, (int)(xdicBit - start - 32));
            DwgObjectStreamWriter.CopyBits(body, src, xdicBit + 1, (int)(ss - xdicBit - 1));
            foreach (var v in values) body.WriteTV(v);
            handles!.WriteHandleRef(3, 0);
            if (DwgObjectStreamWriter.DebugTrace)
                System.Console.Error.WriteLine(
                    $"C21DN15 h={rec.Handle:X} t={rec.TypeCode} AC1021→AC1015 剥xdic位({xdicBit})" +
                    $"截SS至{ss}+串TV[{string.Join("|", values)}] out={body.BitPosition} hs={handles.BitPosition}位@{dataEnd}");
            return true;
        }
        catch { return false; }
    }

    internal static bool TryAc1021HandlesTail(DwgObjectRecord rec, byte[] src, long dataEnd,
        int minHsRefs, out BitWriter? handles)
    {
        handles = null;
        long hsEnd = DwgObjectStreamWriter.LastMeaningfulHandleEnd(src, dataEnd, (long)rec.BodyEnd * 8);
        int refs = CountHandleRefs(src, dataEnd, hsEnd);
        if (hsEnd <= dataEnd || refs < minHsRefs) return false;
        var hs = new BitWriter(16);
        DwgObjectStreamWriter.CopyBits(hs, src, dataEnd, (int)(hsEnd - dataEnd));
        handles = hs;
        return true;
    }

    internal static bool TryAc1021Spans(byte[] src, DwgObjectRecord rec, out long dataEnd,
        out long ssStart, out bool hasStrings)
    {
        dataEnd = ssStart = 0;
        hasStrings = false;
        long start = rec.CarryStartBit;
        var br = new BitReader(src, 0);
        DwgObjectRecord.SeekTo(br, start);
        uint bitsize = br.ReadRL();
        dataEnd = (long)rec.BodyStart * 8 + bitsize;
        if (bitsize < 34 || dataEnd <= start + 32 || dataEnd > (long)rec.BodyEnd * 8) return false;
        var fr = new BitReader(src, 0);
        DwgObjectRecord.SeekTo(fr, dataEnd - 1);
        if (!fr.ReadB()) { ssStart = dataEnd - 1; return true; }
        hasStrings = true;
        DwgObjectRecord.SeekTo(fr, dataEnd - 17);
        uint dataSize = fr.ReadRS();
        int sizeFieldBits = 17;
        if ((dataSize & 0x8000) != 0)
        {
            DwgObjectRecord.SeekTo(fr, dataEnd - 33);
            dataSize = (dataSize & 0x7FFF) | ((uint)fr.ReadRS() << 15);
            sizeFieldBits = 33;
        }
        ssStart = dataEnd - sizeFieldBits - dataSize;
        return ssStart > start + 32;
    }

    private static int CountHandleRefs(byte[] src, long from, long to)
    {
        var br = new BitReader(src, 0);
        int cnt = 0;
        for (long p = from; p + 8 <= to;)
        {
            DwgObjectRecord.SeekTo(br, p);
            int cc = br.ReadRC();
            long bits = 8 + (cc & 0xF) * 8L;
            if (p + bits > to) break;
            p += bits;
            cnt++;
        }
        return cnt;
    }

    private static bool KeysReinlinable(DwgObjectRecord rec)
    {
        int n = rec.DictionaryNumItems;
        if (n is <= 0 or > 1_000_000 || rec.DictionaryEntries.Count != n) return false;
        for (int i = 0; i < n; i++)
            if (string.IsNullOrEmpty(rec.DictionaryEntries[i].Key)) return false;
        return true;
    }

    internal static void AppendDowngradedKeys(BitWriter body, DwgObjectRecord rec,
        DwgObjectStreamWriter.Target t, bool ac1015BodyCapped = false)
    {
        if (!rec.SourceVersion.IsR2010Plus()) return;
        if (rec.LogicalType != TypeDictionaryLogical) return;
        if (CarriesDictDropDroppedEntriesDown(rec, t)) return;
        if (t.Version != DwgVersion.AC1018
            && !(ac1015BodyCapped && t.Version == DwgVersion.AC1015)) return;
        if (!KeysReinlinable(rec)) return;
        int n = rec.DictionaryNumItems;
        for (int i = 0; i < n; i++)
            body.WriteTV(rec.DictionaryEntries[i].Key);
        if (DwgObjectStreamWriter.DebugTrace)
            System.Console.Error.WriteLine(
                $"DICTDOWN h={rec.Handle:X} {rec.SourceVersion}→{t.Version} 键再内联 {n} 枚 TV");
    }

    internal static bool TryCapBodyAtCentralStringsAc1015(DwgObjectRecord rec,
        DwgObjectStreamWriter.Target t, long start, ref long bodyEndBit, ref int len)
    {
        if (t.Version != DwgVersion.AC1015 || !rec.SourceVersion.IsR2010Plus()) return false;
        if (!CentralPayloadReinlinableAc1015(rec)) return false;
        long ss = rec.StringStreamStartBit;
        if (ss <= start || ss >= bodyEndBit) return false;
        bodyEndBit = ss;
        len = (int)(ss - start);
        if (DwgObjectStreamWriter.DebugTrace)
            System.Console.Error.WriteLine(
                $"C15CAP h={rec.Handle:X} {rec.SourceVersion}→AC1015 搬运窗收到 SS={ss} len={len}" +
                $"（原 bodyEnd={rec.HandleStreamStartBit}，type=0x{rec.LogicalType:X} 待 TV 内联）");
        return true;
    }

    private static bool CentralPayloadReinlinableAc1015(DwgObjectRecord rec)
        => rec.LogicalType switch
        {
            TypeDictionaryLogical => KeysReinlinable(rec),
            TypeDictionaryVarLogical => rec.DictVar is not null && rec.DictVar.Captured,
            TypeDictWdfltLogical => WdfltPayload(rec) is not null,
            _ => false,
        };

    internal static void AppendDowngradedStrValue(BitWriter body, DwgObjectRecord rec,
        DwgObjectStreamWriter.Target t, bool ac1015BodyCapped = false)
    {
        if (!rec.SourceVersion.IsR2010Plus()) return;
        if (rec.LogicalType != TypeDictionaryVarLogical) return;
        if (t.Version != DwgVersion.AC1018
            && !(ac1015BodyCapped && t.Version == DwgVersion.AC1015)) return;
        var cap = rec.DictVar;
        if (cap is null || !cap.Captured) return;
        body.WriteTV(cap.StrValue);
        if (DwgObjectStreamWriter.DebugTrace)
            System.Console.Error.WriteLine(
                $"DVARDOWN h={rec.Handle:X} {rec.SourceVersion}→{t.Version} strvalue 再内联 " +
                $"\"{cap.StrValue}\" schema={cap.Schema}");
    }

    internal static void AppendDowngradedEntryName(BitWriter body, DwgObjectRecord rec,
        DwgObjectStreamWriter.Target t, bool ac1015BodyCapped = false)
    {
        if (!rec.SourceVersion.IsR2010Plus()) return;
        if (rec.LogicalType != TypeDictWdfltLogical) return;
        if (t.Version != DwgVersion.AC1018
            && !(ac1015BodyCapped && t.Version == DwgVersion.AC1015)) return;
        var names = WdfltPayload(rec);
        if (names is null) return;
        foreach (var name in names) body.WriteTV(name);
        if (DwgObjectStreamWriter.DebugTrace)
            System.Console.Error.WriteLine(
                $"WDFLTDOWN h={rec.Handle:X} {rec.SourceVersion}→{t.Version} 默认条目名再内联 " +
                $"[{string.Join("|", names)}] nent={rec.DictWdflt!.NumEntries} " +
                $"dc={rec.DictWdflt.DuplicateCloning} ho={rec.DictWdflt.HardOwner}");
    }

    internal static int? PatchDictVarNullTailAc1015(BitWriter handles, DwgObjectRecord rec,
        DwgObjectStreamWriter.Target t)
    {
        if (t.Version != DwgVersion.AC1015 || rec.LogicalType != TypeDictionaryVarLogical) return null;
        if (rec.SourceVersion < DwgVersion.AC1018) return null;

        if (handles.TrimTrailingZeroBits() != 0)
        {
            handles.WriteHandleRef(3, 0);
            if (DwgObjectStreamWriter.DebugTrace)
                System.Console.Error.WriteLine(
                    $"DVTAIL h={rec.Handle:X} {rec.SourceVersion}→{t.Version} 裁零垫后补 (3,0) hs={handles.BitPosition}位");
            return (int)handles.BitPosition;
        }

        long bp = handles.BitPosition;
        if (bp % 8 == 0) return null;
        byte[] hb = handles.ToArray();
        long lastEnd = DwgObjectStreamWriter.LastMeaningfulHandleEnd(hb, 0, bp);
        if (lastEnd <= 0 || lastEnd % 8 != 0 || lastEnd >= bp) return null;
        int lastCode = -1;
        {
            long q = 0;
            while (q + 8 <= lastEnd)
            {
                int code = 0;
                for (int k = 0; k < 4; k++) code = (code << 1) | (DwgObjectStreamWriter.ReadSrcBit(hb, q + k) ? 1 : 0);
                int cnt = 0;
                for (int k = 0; k < 4; k++) cnt = (cnt << 1) | (DwgObjectStreamWriter.ReadSrcBit(hb, q + 4 + k) ? 1 : 0);
                long e = q + 8 + cnt * 8L;
                if (e > lastEnd) break;
                lastCode = code;
                q = e;
            }
        }
        if (lastCode == 3) return null;
        handles.TrimLastBits(bp - lastEnd);
        handles.WriteHandleRef(3, 0);
        if (DwgObjectStreamWriter.DebugTrace)
            System.Console.Error.WriteLine(
                $"DVTAIL h={rec.Handle:X} {rec.SourceVersion}→{t.Version} 退非零半字节尾→末完整引用 {lastEnd} 位 + 补 (3,0) hs={handles.BitPosition}位");
        return (int)handles.BitPosition;
    }

    internal static void AppendDowngradedStrings(BitWriter body, DwgObjectRecord rec,
        DwgObjectStreamWriter.Target t, bool ac1015BodyCapped = false)
    {
        AppendDowngradedKeys(body, rec, t, ac1015BodyCapped);
        AppendDowngradedStrValue(body, rec, t, ac1015BodyCapped);
        AppendDowngradedEntryName(body, rec, t, ac1015BodyCapped);
    }
}
