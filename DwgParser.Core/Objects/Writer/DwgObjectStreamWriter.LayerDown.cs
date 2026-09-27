using DwgParser.Core.IO;

namespace DwgParser.Core.Objects;

public static partial class DwgObjectStreamWriter
{
    private static bool CarriesLayerR2010Down(DwgObjectRecord rec, Target t)
        => t.Version == DwgVersion.AC1018
           && rec.SourceVersion >= DwgVersion.AC1021
           && rec.LogicalType == TypeLayer
           && rec.Layer is { } cap && !string.IsNullOrEmpty(cap.Name)
           && rec.StringStreamStartBit > 0;

    private static bool CarriesLayerR2010ToAc1015(DwgObjectRecord rec, Target t)
        => t.Version == DwgVersion.AC1015
           && rec.SourceVersion.IsR2010Plus()
           && rec.LogicalType == TypeLayer
           && rec.Layer is { } cap && !string.IsNullOrEmpty(cap.Name)
           && rec.StringStreamStartBit > 0
           && rec.HasHandleStream && rec.HandleStreamStartBit > 0;

    private static bool CarryLayerR2010Down(BitWriter body, DwgObjectRecord rec, byte[] src,
        Target t)
        => CarryLayerCore(body, rec, src, t, ac1015: false);

    internal static bool CarryLayerR2010ToAc1015(BitWriter body, DwgObjectRecord rec,
        byte[] src, Target t, out BitWriter? handles)
    {
        handles = null;
        if (!CarryLayerCore(body, rec, src, t, ac1015: true)) return false;

        long hsStart = rec.HandleStreamStartBit;
        long dataEnd = (long)rec.BodyEnd * 8;
        var srcHs = new BitWriter(16);
        if (!CopyLayerHandlesDownToAc1018(srcHs, src, hsStart, dataEnd, rec)) return false;
        var shaped = srcHs.ToArray();
        if (HandleStreamHasXdicSlot(shaped, srcHs.BitPosition))
        {
            handles = srcHs;
            return true;
        }
        if (FindAfterNumReactors(rec, src, PrefixHasRl(rec.SourceVersion), out _, out uint nr) < 0)
            return false;
        if (nr > 64) return false;
        long want = (long)nr + 1;
        var hs = new BitWriter(16);
        if (!InsertXdicSlotAfterHead(srcHs, hs, want)) return false;
        handles = hs;
        return true;
    }

    private static bool InsertXdicSlotAfterHead(BitWriter srcHs, BitWriter dst, long want)
    {
        var buf = srcHs.ToArray();
        long end = srcHs.BitPosition;
        var br = new BitReader(buf, 0);
        long hp = 0, seen = 0;
        while (hp + 8 <= end)
        {
            DwgObjectRecord.SeekTo(br, hp);
            int cc = br.ReadRC();
            long bits = 8 + (cc & 0xF) * 8L;
            if (hp + bits > end) return false;
            if ((cc >> 4) == 3) return false;
            hp += bits;
            seen++;
            if (seen == want)
            {
                CopyBits(dst, buf, 0, (int)hp);
                dst.WriteRC(0x30);
                CopyBits(dst, buf, hp, (int)(end - hp));
                return true;
            }
        }
        return false;
    }

    private static bool CarryLayerCore(BitWriter body, DwgObjectRecord rec, byte[] src,
        Target t, bool ac1015)
    {
        var cap = rec.Layer!;
        long start = rec.CarryStartBit + (rec.SourceVersion.IsR2010Plus() ? 0 : 32);
        long ssStart = rec.StringStreamStartBit;
        if (start <= 0 || ssStart <= start) return false;
        try
        {
            var br = new BitReader(src, 0);
            DwgObjectRecord.SeekTo(br, start);
            br.ReadHandle();
            long eedStart = br.BitPosition;
            if (!SkipEedWriter(br)) return false;
            int eedLen = (int)(br.BitPosition - eedStart);
            long numReactors = br.ReadBL();
            if (numReactors is < 0 or > 10000) return false;
            bool xdic = br.ReadB();
            if (rec.SourceVersion >= DwgVersion.AC1027) br.ReadB();
            long nameIns = br.BitPosition;
            int resolved = br.ReadBS();
            long flagsStart = br.BitPosition;
            if (flagsStart <= start || flagsStart > ssStart) return false;

            CopyBits(body, src, start, (int)(eedStart - start));
            if (!TryWriteEedPreR2007Range(body, src, eedStart, eedLen))
                CopyBits(body, src, eedStart, eedLen);
            body.WriteBL((int)numReactors);
            if (!ac1015) body.WriteB(xdic);

            body.WriteTV(cap.Name);

            body.WriteB(resolved != 0);
            body.WriteBS((short)resolved);
            body.WriteB(cap.XrefDep);

            if (ac1015)
            {
                long cs = cap.ColorFieldStartBit, ce = cap.ColorFieldEndBit;
                if (cs <= flagsStart || ce < cs || ce > ssStart) return false;
                if ((cap.TrueColor >> 24) is < 0xC0 or > 0xC8) return false;
                CopyBits(body, src, flagsStart, (int)(cs - flagsStart));
                body.WriteBS(FoldCmcToPreR2004Index(cap.TrueColor));
                CopyBits(body, src, ce, (int)(ssStart - ce));
            }
            else
            {
                CopyBits(body, src, flagsStart, (int)(ssStart - flagsStart));
            }

            if (DebugTrace)
                System.Console.Error.WriteLine(
                    $"LAYRDOWN h={rec.Handle:X} {rec.SourceVersion}→{t.Version} name='{cap.Name}' " +
                    $"xref=({resolved != 0},{resolved},{cap.XrefDep}) nr={numReactors} xdic={xdic} " +
                    $"start={start} eed={eedStart}+{eedLen} nameIns={nameIns} flagsStart={flagsStart} ss={ssStart} out={body.BitPosition}");
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool CopyLayerHandlesDownToAc1018(BitWriter handles, byte[] src,
        long hsStartBit, long dataEndBit, DwgObjectRecord rec, bool dropMaterial = true)
    {
        long hsEnd = LastMeaningfulHandleEnd(src, hsStartBit, dataEndBit);
        if (hsEnd <= hsStartBit) hsEnd = dataEndBit;
        bool isR2013 = rec.SourceVersion >= DwgVersion.AC1027;
        ulong matH = rec.Layer?.DefaultMaterialHandle ?? 0;

        var refs = new System.Collections.Generic.List<(long start, int bits, int code, ulong val)>();
        var r = new BitReader(src);
        for (long hp = hsStartBit; hp + 8 <= hsEnd; )
        {
            DwgObjectRecord.SeekTo(r, hp);
            int cc = r.ReadRC();
            int sz = cc & 0xF;
            int bits = 8 + sz * 8;
            if (hp + bits > hsEnd) break;
            int code = cc >> 4;
            ulong v = 0;
            for (int i = 0; i < sz; i++) v = (v << 8) | r.ReadRC();
            refs.Add((hp, bits, code, v));
            hp += bits;
        }
        int visIdx = -1;
        if (isR2013)
            for (int i = 0; i < refs.Count; i++)
                if (refs[i].code == 5) visIdx = i;
        var sb = new System.Text.StringBuilder();
        int matBack = isR2013 ? 3 : 2;
        int matIdx = refs.Count - matBack;
        for (int i = 0; i < refs.Count; i++)
        {
            var (st, bits, code, val) = refs[i];
            bool matByPos = dropMaterial && code == 5 && val == 0 && i == matIdx
                && refs.Count >= matBack + 3 && rec.SourceVersion >= DwgVersion.AC1021;
            bool drop = (i == visIdx) || matByPos
                || (dropMaterial && matH > 0 && code == 5 && val == matH);
            if (!drop) CopyBits(handles, src, st, bits);
            sb.Append($"[{code}:{val:X}{(drop ? "×" : "")}]");
        }
        if (DebugTrace)
            System.Console.Error.WriteLine(
                $"LAYRHS2 h={rec.Handle:X} {rec.SourceVersion}→AC1018 mat={matH:X} visIdx={visIdx} refs={sb} out={handles.BitPosition}位");
        return true;
    }
}
