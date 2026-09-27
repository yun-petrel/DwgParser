using DwgParser.Core.Format;
using DwgParser.Core.IO;

namespace DwgParser.Core.Objects;

public static partial class DwgObjectStreamWriter
{
    private static bool CarriesProxyR2007Down(DwgObjectRecord rec, Target t)
        => t.Version == DwgVersion.AC1018
           && rec.SourceVersion >= DwgVersion.AC1021
           && rec.LogicalType == TypeProxyObj
           && rec.ProxyObject is { Captured: true, NameBitLen: 0, TailMarkerRelBit: >= 0 } cap
           && cap.ClassName.Length is > 0 and <= 31;

    private static bool CarriesProxyR2007ToAc1015Down(DwgObjectRecord rec, Target t)
        => Rv15On()
           && t.Version == DwgVersion.AC1015
           && rec.SourceVersion == DwgVersion.AC1021
           && rec.LogicalType == TypeProxyObj
           && rec.ProxyObject is { Captured: true, NameBitLen: 0, TailMarkerRelBit: >= 0 } cap
           && cap.ClassName.Length is > 0 and <= 31
           && ProxyDataTail(cap.ClassName, DwgVersion.AC1015) != 0;

    private static bool Rv15On()
    {
        var v = System.Environment.GetEnvironmentVariable("RV15");
        return v is null || v.Length == 0 || v[0] != '0';
    }

    private static ushort ProxyDataTail(string className, DwgVersion target)
        => (className, target) switch
        {
            ("cn:AcDbRapidRTRenderSettings", DwgVersion.AC1018) => 0x8C01,
            ("cn:AcDbMentalRayRenderSettings", DwgVersion.AC1018) => 0x4001,
            ("cn:AcDbRapidRTRenderSettings", DwgVersion.AC1015) => 0x8C01,
            _ => 0,
        };

    private static bool CarryProxyR2007Down(BitWriter body, DwgObjectRecord rec, byte[] src, Target t)
    {
        var cap = rec.ProxyObject;
        if (cap is not { Captured: true } || cap.TailMarkerRelBit < 0) return false;
        if (cap.NameBitLen != 0) return false;
        int n = cap.ClassName.Length;
        if (n is <= 0 or > 31) return false;
        long dataStart = cap.DataRegionStartBit;
        long hs = cap.DataRegionEndBit;
        long marker = dataStart + cap.TailMarkerRelBit;
        long blockStart = marker - (10 + 16 * n);
        if (blockStart < dataStart || hs <= marker) return false;
        long start = rec.CarryStartBit + (PrefixHasRl(rec.SourceVersion) ? 32 : 0);
        long cidEnd = cap.NameStartBit;
        if (start <= 0 || cidEnd <= start) return false;
        long ds = cap.DsBitPos;
        if (ds >= 0 && (ds < start || ds + 1 > cidEnd)) return false;
        ushort tail = ProxyDataTail(cap.ClassName, t.Version);
        if (tail == 0) return false;
        if (hs - marker < 16) return false;
        try
        {
            CopyBits(body, src, start, (int)((ds >= 0 ? ds : cidEnd) - start));
            if (ds >= 0) CopyBits(body, src, ds + 1, (int)(cidEnd - ds - 1));
            body.WriteTV(cap.ClassName);
            body.WriteBL((int)cap.Version);
            body.WriteB(cap.FromDxf);
            CopyBits(body, src, dataStart, (int)(blockStart - dataStart));
            CopyBits(body, src, marker, (int)(hs - marker - 16));
            body.WriteBits(tail, 16);
            if (DebugTrace)
                System.Console.Error.WriteLine(
                    $"PXDOWN h={rec.Handle:X} {rec.SourceVersion}→{t.Version} name=\"{cap.ClassName}\" " +
                    $"n={n} cid={cap.ClassId} ver=0x{cap.Version:X} ds={(ds >= 0 ? 1 : 0)} " +
                    $"data {hs - dataStart}→{hs - dataStart - (10 + 16 * n)} 位 T=0x{tail:X4} " +
                    $"[carry={rec.CarryStartBit} hsStart={rec.HandleStreamStartBit} " +
                    $"strStart={rec.StringStreamStartBit} dataStart={dataStart} dataEnd={hs} " +
                    $"marker={marker} blockStart={blockStart} start={start} cidEnd={cidEnd} dsPos={ds}]");
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool CarryProxyR2007ToAc1015Down(BitWriter body, DwgObjectRecord rec, byte[] src, Target t)
    {
        var cap = rec.ProxyObject;
        if (cap is not { Captured: true } || cap.TailMarkerRelBit < 0) return false;
        if (cap.NameBitLen != 0) return false;
        int n = cap.ClassName.Length;
        if (n is <= 0 or > 31) return false;
        long dataStart = cap.DataRegionStartBit;
        long hs = cap.DataRegionEndBit;
        long marker = dataStart + cap.TailMarkerRelBit;
        long blockStart = marker - (10 + 16 * n);
        if (blockStart < dataStart || hs <= marker) return false;
        long start = rec.CarryStartBit + (PrefixHasRl(rec.SourceVersion) ? 32 : 0);
        long cidEnd = cap.NameStartBit;
        if (start <= 0 || cidEnd <= start) return false;
        long ds = cap.DsBitPos;
        if (ds >= 0 && (ds < start || ds + 1 > cidEnd)) return false;
        ushort tail = ProxyDataTail(cap.ClassName, t.Version);
        if (tail == 0) return false;
        if (hs - marker < 16) return false;
        long xmBit = -1;
        {
            var brx = new BitReader(src, 0);
            DwgObjectRecord.SeekTo(brx, start);
            if (brx.ReadHandle().Value == 0) return false;
            if (!DwgObjectReader.TrySkipEed(brx)) return false;
            long nrx = brx.ReadBL();
            if (nrx is < 0 or > 8) return false;
            xmBit = brx.BitPosition;
        }
        try
        {
            CopyHeaderSkipBits(body, src, start, cidEnd, xmBit, ds);
            body.WriteBL((int)cap.Version);
            body.WriteB(cap.FromDxf);
            CopyBits(body, src, dataStart, (int)(blockStart - dataStart));
            CopyBits(body, src, marker, (int)(hs - marker - 16));
            body.WriteBits(tail, 16);
            if (DebugTrace)
                System.Console.Error.WriteLine(
                    $"PX15DOWN h={rec.Handle:X} {rec.SourceVersion}→{t.Version} name=\"{cap.ClassName}\" " +
                    $"n={n} cid={cap.ClassId} ver=0x{cap.Version:X} ds={(ds >= 0 ? 1 : 0)} " +
                    $"data {hs - dataStart}→{hs - dataStart - (10 + 16 * n)} 位 T=0x{tail:X4} " +
                    $"[carry={rec.CarryStartBit} hsStart={rec.HandleStreamStartBit} " +
                    $"strStart={rec.StringStreamStartBit} dataStart={dataStart} dataEnd={hs} " +
                    $"marker={marker} blockStart={blockStart} start={start} cidEnd={cidEnd} dsPos={ds}]");
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryDropProxyClassNameToR2000(BitWriter body, byte[] src,
        long from, long to, DwgObjectRecord rec, bool rlInBody)
    {
        if (!Px15On()) return false;
        if (rec.ProxyObject is not { Captured: true, NameBitLen: > 0 } cap) return false;
        long at = cap.NameStartBit, nameEnd = at + cap.NameBitLen;
        if (at <= from || nameEnd > to) return false;

        long startBit = body.BitPosition;
        CopyBits(body, src, from, (int)(at - from));
        CopyBits(body, src, nameEnd, (int)(to - nameEnd));
        int removed = (int)(to - from) - (int)(body.BitPosition - startBit);
        if (removed != (int)cap.NameBitLen) return false;
        if (rlInBody && removed != 0)
        {
            uint rlNow = new BitReader(body.ToArray(), 0).ReadRL();
            body.PatchRL(0, unchecked((uint)(rlNow - removed)));
            if (DebugTrace)
                System.Console.Error.WriteLine(
                    $"PXDROP-RL h={rec.Handle:X} RL {rlNow}→{rlNow - removed}（剥类名 −{removed} 位）");
        }
        if (DebugTrace)
            System.Console.Error.WriteLine(
                $"PXDROP h={rec.Handle:X} {rec.SourceVersion}→AC1015 name=\"{cap.ClassName}\" " +
                $"cid={cap.ClassId} at=+{at - from} 位 窗=[{from},{to}) −{removed}");
        return true;
    }

    private static bool CarriesProxyHandlesAc1015(DwgObjectRecord rec, Target t)
        => Px15On()
           && t.Version == DwgVersion.AC1015
           && (rec.SourceVersion == DwgVersion.AC1018 && rec.LogicalType == TypeProxyObj
               || SynthesizesRenderSettingsProxy(rec, t)
               || CarriesProxyR2007ToAc1015Down(rec, t));

    private static bool Px15On()
    {
        var v = System.Environment.GetEnvironmentVariable("PX15");
        return v == null || v.Length == 0 || v[0] != '0';
    }

    private static bool CopyProxyHandlesAc1015(BitWriter handles, byte[] src,
        long hsStart, long hsEnd, DwgObjectRecord rec)
    {
        if (FindAfterNumReactors(rec, src, PrefixHasRl(rec.SourceVersion), out _, out uint nr) < 0)
            return false;
        if (nr > 64) return false;
        long want = (long)nr + 1;
        long hp = hsStart, refsEnd = hsStart, insertAt = -1, seen = 0;
        bool hasSlot = false;
        var br = new BitReader(src, 0);
        while (hp + 8 <= hsEnd)
        {
            DwgObjectRecord.SeekTo(br, hp);
            int cc = br.ReadRC();
            if (cc == 0) break;
            int bits = 8 + (cc & 0xF) * 8;
            if (hp + bits > hsEnd) break;
            if ((cc >> 4) == 3)
            {
                if (hasSlot || seen != want) return false;
                hasSlot = true;
                insertAt = hp;
            }
            else if (seen < want && (cc >> 4) != 4) return false;
            seen++;
            hp += bits;
            refsEnd = hp;
            if (!hasSlot && seen == want) insertAt = hp;
        }
        if (insertAt < 0 || refsEnd <= hsStart) return false;
        CopyBits(handles, src, hsStart, (int)(insertAt - hsStart));
        if (!hasSlot) handles.WriteRC(0x30);
        CopyBits(handles, src, insertAt, (int)(refsEnd - insertAt));
        if (DebugTrace)
            System.Console.Error.WriteLine(
                $"PXHS h={rec.Handle:X} {rec.SourceVersion}→AC1015 nr={nr} slot={(hasSlot ? "源已带" : "补(3,0)")} " +
                $"源窗 {hsEnd - hsStart}位 → {handles.BitPosition}位（裁尾垫 {(hasSlot ? refsEnd - hsStart : 0)}位）");
        return true;
    }
}
