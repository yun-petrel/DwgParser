using DwgParser.Core.IO;

namespace DwgParser.Core.Objects;

public static partial class DwgObjectStreamWriter
{
    private static bool CarriesDimStyleAc1018ToAc1015(DwgObjectRecord rec, Target t)
        => t.Version == DwgVersion.AC1015
           && rec.SourceVersion == DwgVersion.AC1018
           && rec.LogicalType == TypeDimStyle
           && rec.DimStyle is { NameStartBit: > 0, CmcStartBit: > 0, FxlonEndBit: > 0, Flag0EndBit: > 0 }
           && rec.HasHandleStream && rec.HandleStreamStartBit > 0;

    private static bool CarryDimStyleAc1018ToAc1015(BitWriter body, DwgObjectRecord rec,
        byte[] src, Target t, out BitWriter? handles)
    {
        handles = null;
        var cap = rec.DimStyle;
        if (cap is null) return false;
        long start = rec.CarryStartBit + 32;
        long nameStart = cap.NameStartBit, cmcStart = cap.CmcStartBit, fxlonEnd = cap.FxlonEndBit;
        long hs = rec.HandleStreamStartBit;
        if (start <= 0 || nameStart <= start || cmcStart <= nameStart || fxlonEnd <= cmcStart || hs <= fxlonEnd)
            return false;
        try
        {
            var br = new BitReader(src, 0);
            DwgObjectRecord.SeekTo(br, start);
            br.ReadHandle();
            long eedStart = br.BitPosition;
            if (!SkipEedWriter(br)) return false;
            int eedLen = (int)(br.BitPosition - eedStart);
            int nr = br.ReadBL();
            if (nr is < 0 or > 1024) return false;
            bool xdicMissing = br.ReadB();
            if (br.BitPosition != nameStart) return false;

            if (xdicMissing)
            {
                long hsRefsEnd = LastMeaningfulHandleEnd(src, hs, (long)rec.BodyEnd * 8);
                var hb = new BitWriter(16);
                if (hsRefsEnd <= hs || !CopyPreR2010HandlesWithXdicSlot(hb, src, hs, hsRefsEnd, rec,
                        strictHeadShape: true))
                    return false;
                handles = hb;
            }

            CopyBits(body, src, start, (int)(eedStart - start));
            if (!TryWriteEedPreR2007Range(body, src, eedStart, eedLen))
                CopyBits(body, src, eedStart, eedLen);
            body.WriteBL(nr);
            CopyBits(body, src, nameStart, (int)(cmcStart - nameStart));
            var cb = new BitReader(src, 0);
            DwgObjectRecord.SeekTo(cb, cmcStart);
            for (int i = 0; i < 3; i++)
            {
                var tri = DwgObjectReader.ReadCmcTriple(cb);
                if (cb.BitPosition > fxlonEnd) return false;
                if (tri.Idx is < 0 or > 256 || (tri.Rgb >> 24) is < 0xC0 or > 0xC8) return false;
                body.WriteBS(FoldCmcToPreR2004Index(tri.Rgb));
            }
            long cmcEnd = cb.BitPosition;
            CopyBits(body, src, cmcEnd, (int)(hs - cmcEnd));
            if (DebugTrace)
                System.Console.Error.WriteLine(
                    $"DSDN1015 h={rec.Handle:X} AC1018→AC1015 折 3 枚 CMC {cmcEnd - cmcStart}位 body={body.BitPosition} " +
                    $"xm={xdicMissing} hs={handles?.BitPosition ?? 0}");
            return true;
        }
        catch { return false; }
    }

    private static bool CarriesDimStyleR2010Down(DwgObjectRecord rec, Target t)
        => t.Version == DwgVersion.AC1018
           && rec.SourceVersion >= DwgVersion.AC1021
           && rec.LogicalType == TypeDimStyle
           && rec.DimStyle is { DownCaptured: true, XrefResolved: 0 or 256,
               DownXrefEndBit: > 0, DownNineBdEndBit: > 0, DownHeadEndBit: > 0,
               DownThreeBsEndBit: > 0, DownArcSymEndBit: > 0, DownAtFitEndBit: > 0,
               DownBlockBits: >= 0, Flag0EndBit: > 0 } cap
           && !string.IsNullOrEmpty(cap.Name);

    private static bool CarryDimStyleR2010Down(BitWriter body, DwgObjectRecord rec, byte[] src,
        Target t)
    {
        var cap = rec.DimStyle!;
        long start = rec.CarryStartBit + (rec.SourceVersion.IsR2010Plus() ? 0 : 32);
        long flag0End = cap.Flag0EndBit;
        long blockEnd = cap.FxlonEndBit + cap.DownBlockBits;
        if (rec.SourceVersion >= DwgVersion.AC1024 ? cap.DownBlockBits <= 0 : cap.DownBlockBits != 0)
            return false;
        if (start <= 0 || flag0End <= start
            || !(start < cap.DownXrefEndBit && cap.DownXrefEndBit <= cap.DownNineBdEndBit
                && cap.DownNineBdEndBit <= cap.DownHeadEndBit
                && cap.DownHeadEndBit <= cap.DownThreeBsEndBit
                && cap.DownThreeBsEndBit <= cap.DownArcSymEndBit
                && cap.DownArcSymEndBit <= cap.DownAtFitEndBit
                && cap.DownAtFitEndBit <= cap.FxlonEndBit
                && cap.FxlonEndBit <= blockEnd && blockEnd <= flag0End))
            return false;
        try
        {
            var br = new BitReader(src, 0);
            DwgObjectRecord.SeekTo(br, start);
            br.ReadHandle();
            long eedStart = br.BitPosition;
            if (!SkipEedWriter(br)) return false;
            int eedLen = (int)(br.BitPosition - eedStart);
            long numReactors = br.ReadBL();
            if (numReactors is < 0 or > 1024) return false;
            bool xdic = br.ReadB();
            if (rec.SourceVersion >= DwgVersion.AC1027) br.ReadB();
            int resolved = br.ReadBS();
            if (resolved is not (0 or 256) || br.BitPosition != cap.DownXrefEndBit) return false;

            CopyBits(body, src, start, (int)(eedStart - start));
            if (!TryWriteEedPreR2007Range(body, src, eedStart, eedLen))
                CopyBits(body, src, eedStart, eedLen);
            body.WriteBL((int)numReactors);
            body.WriteB(xdic);

            body.WriteTV(cap.Name);
            body.WriteB(true);
            body.WriteBS((short)resolved);
            body.WriteB(resolved == 256);
            body.WriteTV(cap.Dimpost ?? "");
            body.WriteTV(cap.Dimapost ?? "");

            CopyBits(body, src, cap.DownXrefEndBit, (int)(cap.DownNineBdEndBit - cap.DownXrefEndBit));
            CopyBits(body, src, cap.DownHeadEndBit, (int)(cap.DownThreeBsEndBit - cap.DownHeadEndBit));
            CopyBits(body, src, cap.DownArcSymEndBit, (int)(cap.DownAtFitEndBit - cap.DownArcSymEndBit));
            CopyBits(body, src, blockEnd, (int)(flag0End - blockEnd));

            if (DebugTrace)
                System.Console.Error.WriteLine(
                    $"DSDN27 h={rec.Handle:X} {rec.SourceVersion}→{t.Version} name='{cap.Name}' " +
                    $"xref=(1,{resolved},{resolved == 256}) nr={numReactors} xdic={xdic} " +
                    $"segs=[{cap.DownXrefEndBit},{cap.DownNineBdEndBit})[{cap.DownHeadEndBit},{cap.DownThreeBsEndBit})" +
                    $"[{cap.DownArcSymEndBit},{cap.DownAtFitEndBit})[{blockEnd},{flag0End}) out={body.BitPosition}");
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool CopyDimStyleHandlesDownToAc1018(BitWriter handles, byte[] src,
        long hsStartBit, long dataEndBit, DwgObjectRecord rec)
    {
        var refs = new System.Collections.Generic.List<(long start, int bits, int code, ulong val)>();
        var r = new BitReader(src);
        for (long hp = hsStartBit; hp + 8 <= dataEndBit; )
        {
            DwgObjectRecord.SeekTo(r, hp);
            int cc = r.ReadRC();
            int sz = cc & 0xF;
            int bits = 8 + sz * 8;
            if (hp + bits > dataEndBit) break;
            int code = cc >> 4;
            ulong v = 0;
            for (int i = 0; i < sz; i++) v = (v << 8) | r.ReadRS();
            refs.Add((hp, bits, code, v));
            hp += bits;
        }
        if (refs.Count < 10 || refs[0].code != 4) return false;
        int keep = refs.Count - 3;
        for (int i = keep; i < refs.Count; i++)
            if (refs[i].code != 5) return false;
        for (int i = 0; i < keep; i++)
            CopyBits(handles, src, refs[i].start, refs[i].bits);
        if (DebugTrace)
            System.Console.Error.WriteLine(
                $"DSDNHS h={rec.Handle:X} {rec.SourceVersion}→AC1018 剥末三枚 ltype/tex refs={refs.Count}→{keep} " +
                $"hs={handles.BitPosition}位");
        return true;
    }
}
