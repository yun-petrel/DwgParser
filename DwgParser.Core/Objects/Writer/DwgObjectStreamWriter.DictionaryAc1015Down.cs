using DwgParser.Core.IO;

namespace DwgParser.Core.Objects;

internal static partial class DictionaryCarry
{
    internal static bool CarriesDictionaryAc1015Down(DwgObjectRecord rec,
        DwgObjectStreamWriter.Target t)
        => t.Version == DwgVersion.AC1015
           && rec.SourceVersion >= DwgVersion.AC1021
           && rec.LogicalType == TypeDictionaryLogical;

    internal static bool CarryDictionaryAc1015Down(BitWriter body, DwgObjectRecord rec, byte[] src,
        DwgObjectStreamWriter.Target t, out BitWriter? handles)
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

        long start = rec.CarryStartBit;
        if (start <= 0
            || !TryAc1021Spans(src, rec, out long dataEnd, out long ss, out bool hasStrings))
            return false;
        if (hasStrings)
        {
            var sr = new BitReader(src) { UnicodeStrings = true };
            DwgObjectRecord.SeekTo(sr, ss);
            for (int i = 0; i < n; i++)
                if (sr.ReadTV() != keys[i]) return false;
            sr.ReadRS();
            if (!sr.ReadB() || sr.BitPosition != dataEnd) return false;
        }
        else if (n != 0) return false;

        long xdicBit = FindXdBitAc1021(rec, src);
        if (xdicBit <= start + 32 || xdicBit >= ss) return false;
        bool srcXdicMissing = DwgObjectStreamWriter.ReadSrcBit(src, xdicBit);

        if (!TryAc1021HandlesTail(rec, src, dataEnd, n + 1, out handles)) return false;
        if (srcXdicMissing)
        {
            var hb = handles!;
            long total = hb.BitPosition;
            long insertAt = ScanOwnerReactorPrefix(hb.ToArray());
            if (insertAt <= 0)
            {
                var (prefCnt, prefEnd) = DwgObjectStreamWriter.WalkMeaningfulHandleRefs(hb.ToArray(), 0, total);
                if (prefCnt == 0 || prefEnd <= 0 || prefEnd > total) return false;
                insertAt = prefEnd;
            }
            if (insertAt <= 0 || insertAt > total) return false;
            var shifted = new BitWriter(16);
            DwgObjectStreamWriter.CopyBits(shifted, hb.ToArray(), 0, (int)insertAt);
            shifted.WriteHandleRef(3, 0);
            DwgObjectStreamWriter.CopyBits(shifted, hb.ToArray(), (int)insertAt,
                (int)(total - insertAt));
            handles = shifted;
        }

        DwgObjectStreamWriter.CopyCommonHeadWithEedDown(body, src, start + 32, xdicBit, rec, t);
        DwgObjectStreamWriter.CopyBits(body, src, xdicBit + 1, (int)(ss - xdicBit - 1));
        for (int i = 0; i < n; i++) body.WriteTV(keys[i]);
        if (DwgObjectStreamWriter.DebugTrace)
            System.Console.Error.WriteLine(
                $"C15DN h={rec.Handle:X} t={rec.TypeCode} {rec.SourceVersion}→AC1015 截SS至{ss}" +
                $"+{n}键TV 剥xdic@{xdicBit}(missing={srcXdicMissing}) out={body.BitPosition}" +
                $" hs={handles!.BitPosition}位@{dataEnd}");
        return true;
    }

}
