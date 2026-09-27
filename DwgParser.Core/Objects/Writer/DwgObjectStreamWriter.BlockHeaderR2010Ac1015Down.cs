using System;
using System.Collections.Generic;
using DwgParser.Core.IO;

namespace DwgParser.Core.Objects;

public static partial class DwgObjectStreamWriter
{
    private static bool CarriesBlockHeaderR2010ToAc1015(DwgObjectRecord rec, Target t)
        => t.Version == DwgVersion.AC1015
           && rec.SourceVersion >= DwgVersion.AC1021
           && rec.LogicalType == TypeBlockHeader
           && rec.StringStreamStartBit > 0
           && rec.BlockOwnedHandles is { } owned
           && rec.BlockOwnedSourceCount >= 0
           && owned.Count == rec.BlockOwnedSourceCount;

    private static bool CarryBlockHeaderR2010ToAc1015(BitWriter body, DwgObjectRecord rec,
        byte[] src, out BitWriter? handles)
    {
        handles = null;
        var owned = rec.BlockOwnedHandles!;
        long start = rec.CarryStartBit
            + (rec.SourceVersion.IsR2010Plus() ? 0 : 32);
        long ss = rec.StringStreamStartBit;
        if (!TryBlockHeaderHandleStreamStart(rec, src, out long hs)) return false;
        if (start <= 0 || ss <= start || hs <= ss) return false;
        if (!TryReadCentralString(src, ss, out string name) || name.Length == 0) return false;
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
            bool xdicMissing = br.ReadB();
            if (rec.SourceVersion >= DwgVersion.AC1027) br.ReadB();
            if (br.BitPosition >= ss) return false;
            int resolved = br.ReadBS();
            br.ReadB(); br.ReadB(); br.ReadB(); br.ReadB(); br.ReadB();
            long flagsEnd = br.BitPosition;
            long nOwnStart = br.BitPosition;
            int numOwned = br.ReadBL();
            long nOwnEnd = br.BitPosition;
            if (numOwned != owned.Count) return false;
            br.Read3BD();
            long afterBase = br.BitPosition;
            if (afterBase > hs) return false;

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

            var probe = new BitWriter(64);
            if (!TryCarryBlockHeaderTailAc1018(probe, src, nOwnEnd, afterBase, hs, ss, out _))
                return false;
            var probeBytes = probe.ToArray();
            long probeBits = probe.BitPosition;

            CopyBits(body, src, start, (int)(eedStart - start));
            if (!TryWriteEedPreR2007Range(body, src, eedStart, eedLen))
                CopyBits(body, src, eedStart, eedLen);
            body.WriteBL(nr);
            body.WriteTV(name, false);
            body.WriteB(true);
            body.WriteBS((short)resolved);
            body.WriteB(false);
            CopyBits(body, src, flagsEnd - 5, (int)(nOwnStart - flagsEnd + 5));
            if (probeBits > 0x2000_0000) return false;
            CopyBits(body, probeBytes, 0, (int)probeBits);

            var hw = new BitWriter(16);
            long headEnd = ends[entityIdx];
            if (xdicMissing)
            {
                long insAt = ends[nr];
                CopyBits(hw, src, hs, (int)(insAt - hs));
                hw.WriteHandleRef(3, 0);
                CopyBits(hw, src, insAt, (int)(headEnd - insAt));
            }
            else CopyBits(hw, src, hs, (int)(headEnd - hs));
            hw.WriteHandleRef(4, owned.Count > 0 ? owned[0] : 0);
            hw.WriteHandleRef(4, owned.Count > 0 ? owned[^1] : 0);
            long tailFrom = ends[entityIdx + owned.Count];
            CopyBits(hw, src, tailFrom, (int)(hsEnd - tailFrom));
            handles = hw;

            if (DebugTrace)
                System.Console.Error.WriteLine(
                    $"BH1015DN h={rec.Handle:X} t={rec.TypeCode} 49 {rec.SourceVersion}→AC1015 " +
                    $"name=\"{name}\" resolved={resolved} owned={owned.Count} nr={nr} " +
                    $"xdicMiss={xdicMissing} 剥num_owned={nOwnEnd - nOwnStart}位 数据区 {hs - start}位 " +
                    $"body={body.BitPosition} hs={hw.BitPosition}@{hs}");
            return true;
        }
        catch (Exception ex) when (ex is EndOfStreamException or IndexOutOfRangeException
                                      or OverflowException or ArgumentException)
        {
            return false;
        }
    }
}
