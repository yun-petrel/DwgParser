using DwgParser.Core.IO;

namespace DwgParser.Core.Objects;

public static partial class DwgObjectStreamWriter
{
    private static bool CarriesDimStyleR2010ToAc1015(DwgObjectRecord rec, Target t)
        => t.Version == DwgVersion.AC1015
           && rec.SourceVersion >= DwgVersion.AC1021
           && rec.LogicalType == TypeDimStyle
           && rec.DimStyle is { DownCaptured: true, DownBlockBits: >= 0,
               DownXrefEndBit: > 0, DownNineBdEndBit: > 0, DownHeadEndBit: > 0,
               DownThreeBsEndBit: > 0, DownArcSymEndBit: > 0, DownAtFitEndBit: > 0,
               CmcStartBit: > 0, CmcEndBit: > 0, FxlonEndBit: > 0, Flag0EndBit: > 0 } cap
           && !string.IsNullOrEmpty(cap.Name)
           && rec.StringStreamStartBit > 0
           && rec.HasHandleStream && rec.HandleStreamStartBit > 0;

    private static bool CarryDimStyleR2010ToAc1015(BitWriter body, DwgObjectRecord rec,
        byte[] src, Target t, out BitWriter? handles)
    {
        handles = null;
        var cap = rec.DimStyle;
        if (cap is null) return false;
        long start = rec.CarryStartBit + (rec.SourceVersion.IsR2010Plus() ? 0 : 32);
        long xrefEnd = cap.DownXrefEndBit, nineBdEnd = cap.DownNineBdEndBit;
        long headEnd = cap.DownHeadEndBit, threeBsEnd = cap.DownThreeBsEndBit;
        long arcSymEnd = cap.DownArcSymEndBit, atFitEnd = cap.DownAtFitEndBit;
        long cmcStart = cap.CmcStartBit, cmcEnd = cap.CmcEndBit;
        long fxlonEnd = cap.FxlonEndBit, flag0End = cap.Flag0EndBit;
        long hs = rec.HandleStreamStartBit;
        if (start <= 0) return false;
        long blockEnd = fxlonEnd + cap.DownBlockBits;
        if (!(start < xrefEnd && xrefEnd <= nineBdEnd && nineBdEnd <= headEnd
              && headEnd <= threeBsEnd && threeBsEnd <= arcSymEnd
              && arcSymEnd < cmcStart && cmcStart < cmcEnd && cmcEnd <= atFitEnd
              && atFitEnd <= fxlonEnd && fxlonEnd <= blockEnd && blockEnd <= flag0End
              && flag0End <= hs))
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
            bool xdicMissing = br.ReadB();
            if (rec.SourceVersion >= DwgVersion.AC1027) br.ReadB();
            int resolved = br.ReadBS();
            if (resolved is not (0 or 256) || br.BitPosition != xrefEnd) return false;

            long hsRefsEnd = LastMeaningfulHandleEnd(src, hs, (long)rec.BodyEnd * 8);
            if (hsRefsEnd <= hs) return false;
            var hb = new BitWriter(16);
            if (!RebuildDimStyleHandlesR2010ToAc1015(hb, src, hs, hsRefsEnd, rec, numReactors))
                return false;
            handles = hb;

            CopyBits(body, src, start, (int)(eedStart - start));
            if (!TryWriteEedPreR2007Range(body, src, eedStart, eedLen))
                CopyBits(body, src, eedStart, eedLen);
            body.WriteBL((int)numReactors);

            body.WriteTV(cap.Name);
            body.WriteB(true);
            body.WriteBS((short)resolved);
            body.WriteB(resolved == 256);
            body.WriteTV(cap.Dimpost ?? "");
            body.WriteTV(cap.Dimapost ?? "");

            CopyBits(body, src, xrefEnd, (int)(nineBdEnd - xrefEnd));
            CopyBits(body, src, headEnd, (int)(threeBsEnd - headEnd));
            CopyBits(body, src, arcSymEnd, (int)(cmcStart - arcSymEnd));
            var cb = new BitReader(src, 0);
            DwgObjectRecord.SeekTo(cb, cmcStart);
            for (int i = 0; i < 3; i++)
            {
                var tri = DwgObjectReader.ReadCmcTriple(cb);
                if (cb.BitPosition > cmcEnd) return false;
                if (tri.Idx is < 0 or > 256 || (tri.Rgb >> 24) is < 0xC0 or > 0xC8) return false;
                body.WriteBS(FoldCmcToPreR2004Index(tri.Rgb));
            }
            if (cb.BitPosition != cmcEnd) return false;
            CopyBits(body, src, cmcEnd, (int)(atFitEnd - cmcEnd));
            CopyBits(body, src, blockEnd, (int)(flag0End - blockEnd));

            if (DebugTrace)
                System.Console.Error.WriteLine(
                    $"DS1015 h={rec.Handle:X} {rec.SourceVersion}→AC1015 name='{cap.Name}' " +
                    $"xref=(1,{resolved},{resolved == 256}) nr={numReactors} xm={xdicMissing} " +
                    $"cmc=[{cmcStart},{cmcEnd}) body={body.BitPosition} hs={handles.BitPosition}位");
            return true;
        }
        catch { return false; }
    }

    private static bool RebuildDimStyleHandlesR2010ToAc1015(BitWriter handles, byte[] src,
        long hsStart, long hsEnd, DwgObjectRecord rec, long numReactors)
    {
        var refs = new System.Collections.Generic.List<(long start, int bits, int code, ulong val)>();
        var r = new BitReader(src);
        for (long hp = hsStart; hp + 8 <= hsEnd; )
        {
            DwgObjectRecord.SeekTo(r, hp);
            int cc = r.ReadRC();
            if (cc == 0) break;
            int bits = 8 + (cc & 0xF) * 8;
            if (hp + bits > hsEnd) break;
            int code = cc >> 4;
            ulong v = 0;
            for (int i = 0; i < (cc & 0xF); i++) v = (v << 8) | r.ReadRS();
            refs.Add((hp, bits, code, v));
            hp += bits;
        }
        if (numReactors > 0)
        {
            int nr = (int)numReactors;
            if (refs.Count < nr + 7) return false;
            for (int i = 0; i <= nr; i++)
                if (refs[i].code != 4) return false;
            for (int i = nr + 1; i < nr + 7; i++)
                if (refs[i].code != 5) return false;
            for (int i = nr + 7; i < nr + 10 && i < refs.Count; i++)
                if (refs[i].code != 5 || refs[i].val != 0) return false;
            long ownReactEnd = refs[nr].start + refs[nr].bits;
            CopyBits(handles, src, refs[0].start, (int)(ownReactEnd - refs[0].start));
            handles.WriteRC(0x30);
            long styleEnd = refs[nr + 6].start + refs[nr + 6].bits;
            CopyBits(handles, src, refs[nr + 1].start, (int)(styleEnd - refs[nr + 1].start));
            if (DebugTrace)
                System.Console.Error.WriteLine(
                    $"DS1015HS-R h={rec.Handle:X} nr={nr} 源{refs.Count}枚→{nr + 8}枚（保 own/reactors/(3,0)/6样式，丢尾 null+(6,)）");
            return true;
        }
        if (refs.Count != 10 || refs[0].code != 4) return false;
        for (int i = 1; i < refs.Count; i++)
            if (refs[i].code != 5) return false;
        for (int i = 7; i < 10; i++)
            if (refs[i].val != 0) return false;
        if (numReactors != 0) return false;
        CopyBits(handles, src, refs[0].start, refs[0].bits);
        handles.WriteRC(0x30);
        for (int i = 1; i < 7; i++)
            CopyBits(handles, src, refs[i].start, refs[i].bits);
        if (DebugTrace)
            System.Console.Error.WriteLine(
                $"DS1015HS h={rec.Handle:X} {rec.SourceVersion}→AC1015 refs=10→8 " +
                $"hs={handles.BitPosition}位");
        return true;
    }
}
