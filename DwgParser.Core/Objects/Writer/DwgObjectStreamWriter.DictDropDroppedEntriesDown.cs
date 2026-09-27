using System.Collections.Generic;
using DwgParser.Core.IO;

namespace DwgParser.Core.Objects;

internal static partial class DictionaryCarry
{
    internal static bool CarriesDictDropDroppedEntriesDown(DwgObjectRecord rec,
        DwgObjectStreamWriter.Target t)
        => t.Version == DwgVersion.AC1018
           && rec.SourceVersion.IsR2010Plus()
           && rec.LogicalType == TypeDictionaryLogical
           && rec.HasHandleStream && rec.HandleStreamStartBit > rec.CarryStartBit
           && rec.DictionaryNumItems > 0
           && rec.DictionaryEntries.Count == rec.DictionaryNumItems
           && DwgObjectStreamWriter.MLeaderCtxDroppedHandles.Count > 0
           && HasDroppedEntry(rec);

    internal static bool CarryDictDropDroppedEntriesDown(BitWriter body, DwgObjectRecord rec, byte[] src,
        out BitWriter? handles)
    {
        handles = null;
        long start = rec.CarryStartBit, dataEnd = rec.HandleStreamStartBit;
        if (start <= 0 || dataEnd <= start) return false;

        var kept = new List<(string Key, ulong ValueHandle)>();
        foreach (var e in rec.DictionaryEntries)
        {
            if (DwgObjectStreamWriter.MLeaderCtxDroppedHandles.Contains(e.ValueHandle)) continue;
            if (string.IsNullOrEmpty(e.Key) || e.Key.Length > 255) return false;
            kept.Add(e);
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

        long xdic = FindXdBitR2010(rec, src);
        if (xdic <= start || xdic >= ss) return false;
        long afterHead = xdic + 1 + (rec.SourceVersion >= DwgVersion.AC1027 ? 1 : 0);
        if (afterHead > ss) return false;
        long cloning, hardOwner;
        try
        {
            var br = new BitReader(src, 0);
            DwgObjectRecord.SeekTo(br, afterHead);
            long n = br.ReadBL();
            if (n != rec.DictionaryNumItems) return false;
            cloning = br.ReadBS();
            hardOwner = br.ReadRC();
            if (br.BitPosition != ss) return false;
        }
        catch { return false; }

        if (hasStrings)
        {
            try
            {
                var sr = new BitReader(src) { UnicodeStrings = true };
                DwgObjectRecord.SeekTo(sr, ss);
                for (int i = 0; i < rec.DictionaryNumItems; i++)
                    if (sr.ReadTV() != rec.DictionaryEntries[i].Key) return false;
                sr.ReadRS();
                if (!sr.ReadB() || sr.BitPosition != dataEnd) return false;
            }
            catch { return false; }
        }

        if (!TryFilteredHandlesTail(rec, src, dataEnd, out handles)) return false;

        long entry = body.BitPosition;
        try
        {
            DwgObjectStreamWriter.CopyHeadWindowWithEedDown(body, src, start, xdic + 1, rec);
            body.WriteBL(kept.Count);
            body.WriteBS(unchecked((short)cloning));
            body.WriteRC(unchecked((byte)hardOwner));
            foreach (var e in kept) body.WriteTV(e.Key);
            if (DwgObjectStreamWriter.DebugTrace)
                System.Console.Error.WriteLine(
                    $"DICTDROP h={rec.Handle:X} t={rec.TypeCode} {rec.SourceVersion}→AC1018 " +
                    $"n={rec.DictionaryNumItems}→{kept.Count} 剥{rec.DictionaryNumItems - kept.Count}条目 " +
                    $"out={body.BitPosition} hs={handles!.BitPosition}位@{dataEnd}");
            return true;
        }
        catch
        {
            body.TrimLastBits(body.BitPosition - entry);
            handles = null;
            return false;
        }
    }

    private static bool HasDroppedEntry(DwgObjectRecord rec)
    {
        foreach (var e in rec.DictionaryEntries)
            if (DwgObjectStreamWriter.MLeaderCtxDroppedHandles.Contains(e.ValueHandle)) return true;
        return false;
    }

    private static bool TryFilteredHandlesTail(DwgObjectRecord rec, byte[] src, long dataEnd,
        out BitWriter? handles)
    {
        handles = null;
        long hsEnd = DwgObjectStreamWriter.LastMeaningfulHandleEnd(src, dataEnd, (long)rec.BodyEnd * 8);
        if (hsEnd <= dataEnd) return false;
        var hs = new BitWriter(16);
        var br = new BitReader(src, 0);
        long p = dataEnd;
        while (p + 8 <= hsEnd)
        {
            DwgObjectRecord.SeekTo(br, p);
            int cc = br.ReadRC();
            int size = cc & 0xF;
            long nb = 8 + size * 8L;
            if (p + nb > hsEnd) break;
            ulong val = 0;
            for (int i = 0; i < size; i++) val = (val << 8) | (byte)br.ReadRC();
            bool drop = cc >> 4 is >= 2 and <= 5
                        && DwgObjectStreamWriter.MLeaderCtxDroppedHandles.Contains(val);
            if (!drop) DwgObjectStreamWriter.CopyBits(hs, src, p, (int)nb);
            p += nb;
        }
        if (hs.BitPosition == 0) return false;
        handles = hs;
        return true;
    }
}
