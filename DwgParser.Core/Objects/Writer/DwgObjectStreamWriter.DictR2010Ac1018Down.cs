using DwgParser.Core.IO;

namespace DwgParser.Core.Objects;

internal static partial class DictionaryCarry
{
    internal static bool CarriesDictR2010Ac1018Down(DwgObjectRecord rec,
        DwgObjectStreamWriter.Target t)
        => t.Version == DwgVersion.AC1018 && rec.SourceVersion.IsR2010Plus()
           && rec.LogicalType == TypeDictionaryLogical
           && rec.HasHandleStream && rec.HandleStreamStartBit > rec.CarryStartBit
           && rec.DictionaryNumItems == 0;

    internal static bool CarryDictR2010Ac1018Down(BitWriter body, DwgObjectRecord rec, byte[] src,
        out BitWriter? handles)
    {
        handles = null;
        long start = rec.CarryStartBit, dataEnd = rec.HandleStreamStartBit;
        if (start <= 0 || dataEnd <= start) return false;

        int n = rec.DictionaryNumItems;
        if (n is < 0 or > 1_000_000 || rec.DictionaryEntries.Count != n) return false;
        var keys = new string[n];
        for (int i = 0; i < n; i++)
        {
            var k = rec.DictionaryEntries[i].Key;
            if (string.IsNullOrEmpty(k) || k.Length > 255) return false;
            keys[i] = k;
        }

        long ss; bool hasStrings;
        try
        {
            var fr = new BitReader(src, 0);
            DwgObjectRecord.SeekTo(fr, dataEnd - 1);
            if (!fr.ReadB()) { ss = dataEnd - 1; hasStrings = false; }
            else
            {
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
                ss = dataEnd - sizeFieldBits - dataSize;
            }
        }
        catch { return false; }
        if (ss <= start || ss > dataEnd) return false;

        if (hasStrings && n > 0)
        {
            try
            {
                var sr = new BitReader(src) { UnicodeStrings = true };
                DwgObjectRecord.SeekTo(sr, ss);
                for (int i = 0; i < n; i++) if (sr.ReadTV() != keys[i]) return false;
                sr.ReadRS();
                if (!sr.ReadB() || sr.BitPosition != dataEnd) return false;
            }
            catch { return false; }
        }
        else if (hasStrings)
        {
            try
            {
                var fr2 = new BitReader(src, 0);
                DwgObjectRecord.SeekTo(fr2, dataEnd - 17);
                if (fr2.ReadRS() != 0) return false;
            }
            catch { return false; }
        }

        long xdic = FindXdBitR2010(rec, src);
        if (xdic <= start || xdic >= ss) return false;
        long afterHead = xdic + 1 + (rec.SourceVersion >= DwgVersion.AC1027 ? 1 : 0);
        if (afterHead > ss) return false;

        if (!TryAc1021HandlesTail(rec, src, dataEnd, n + 1, out handles)) return false;

        DwgObjectStreamWriter.CopyHeadWindowWithEedDown(body, src, start, xdic + 1, rec);
        DwgObjectStreamWriter.CopyBits(body, src, afterHead, (int)(ss - afterHead));
        for (int i = 0; i < n; i++) body.WriteTV(keys[i]);
        if (DwgObjectStreamWriter.DebugTrace)
            System.Console.Error.WriteLine(
                $"D10DN h={rec.Handle:X} t={rec.TypeCode} {rec.SourceVersion}→AC1018 截SS至{ss}" +
                $"+{n}串TV 剥ds={(rec.SourceVersion >= DwgVersion.AC1027 ? 1 : 0)} " +
                $"out={body.BitPosition} hs={handles!.BitPosition}位@{dataEnd}");
        return true;
    }

    private static long FindXdBitR2010(DwgObjectRecord rec, byte[] src)
    {
        try
        {
            var br = new BitReader(src, 0);
            DwgObjectRecord.SeekTo(br, rec.CarryStartBit);
            br.ReadHandle();
            if (!DwgObjectReader.TrySkipEed(br)) return -1;
            br.ReadBL();
            return br.BitPosition;
        }
        catch { return -1; }
    }
}
