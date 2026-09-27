using DwgParser.Core.IO;

namespace DwgParser.Core.Objects;

public static partial class DwgObjectStreamWriter
{
    private static bool CarriesLayerR2007ToAc1015(DwgObjectRecord rec, Target t)
        => t.Version == DwgVersion.AC1015
           && rec.SourceVersion == DwgVersion.AC1021
           && rec.LogicalType == TypeLayer
           && rec.Layer is { } cap && !string.IsNullOrEmpty(cap.Name)
           && cap.ColorFieldStartBit > 0 && cap.ColorFieldEndBit > cap.ColorFieldStartBit
           && rec.StringStreamStartBit > 0 && rec.HandleStreamStartBit > 0;

    private static bool CarryLayerR2007ToAc1015(BitWriter body, DwgObjectRecord rec, byte[] src,
        out BitWriter? handles)
    {
        handles = null;
        var cap = rec.Layer!;
        long start = rec.CarryStartBit + (rec.SourceVersion.IsR2010Plus() ? 0 : 32);
        long ss = rec.StringStreamStartBit, hs = rec.HandleStreamStartBit;
        long cs = cap.ColorFieldStartBit, ce = cap.ColorFieldEndBit;
        if (start <= 0 || ss <= start || hs <= ss || cs <= start || ce <= cs || ce > ss) return false;
        try
        {
            var br = new BitReader(src, 0);
            DwgObjectRecord.SeekTo(br, start);
            br.ReadHandle();
            long eedStart = br.BitPosition;
            if (!SkipEedWriter(br)) return false;
            int eedLen = (int)(br.BitPosition - eedStart);
            int nr = (int)br.ReadBL();
            if (nr is < 0 or > 10000) return false;
            bool xdic = br.ReadB();
            if (rec.SourceVersion >= DwgVersion.AC1027) br.ReadB();
            int resolved = br.ReadBS();
            long flagsStart = br.BitPosition;
            if (flagsStart <= start || flagsStart > cs) return false;

            CopyBits(body, src, start, (int)(eedStart - start));
            if (!TryWriteEedPreR2007Range(body, src, eedStart, eedLen))
                CopyBits(body, src, eedStart, eedLen);
            body.WriteBL(nr);
            body.WriteTV(cap.Name);
            body.WriteB(true);
            body.WriteBS((short)resolved);
            body.WriteB(cap.XrefDep);
            CopyBits(body, src, flagsStart, (int)(cs - flagsStart));

            var cb = new BitReader(src, 0);
            DwgObjectRecord.SeekTo(cb, cs);
            DwgObjectReader.ReadCmc(cb, out uint trueColor, out _, out _);
            if (cb.BitPosition != ce) return false;
            if ((trueColor >> 24) is < 0xC0 or > 0xC8) return false;
            body.WriteBS(FoldCmcToPreR2004Index(trueColor));
            CopyBits(body, src, ce, (int)(ss - ce));

            long dataEnd = (long)rec.BodyEnd * 8;
            long hsEnd = LastMeaningfulHandleEnd(src, hs, dataEnd);
            if (hsEnd <= hs) return false;
            var refs = new System.Collections.Generic.List<(long start, int bits, int code, ulong val)>();
            var rr = new BitReader(src);
            for (long hp = hs; hp + 8 <= hsEnd; )
            {
                DwgObjectRecord.SeekTo(rr, hp);
                int cc = rr.ReadRC();
                int sz = cc & 0xF;
                int bits = 8 + sz * 8;
                if (hp + bits > hsEnd) break;
                ulong v = 0;
                for (int i = 0; i < sz; i++) v = (v << 8) | rr.ReadRC();
                refs.Add((hp, bits, cc >> 4, v));
                hp += bits;
            }
            if (refs.Count < 2 || refs[0].code != 4) return false;
            var hw = new BitWriter(16);
            CopyBits(hw, src, refs[0].start, refs[0].bits);
            int from = 1;
            if (!xdic)
            {
                while (from < refs.Count && refs[from].code == 3)
                {
                    CopyBits(hw, src, refs[from].start, refs[from].bits);
                    from++;
                }
            }
            else hw.WriteRC(0x30);
            int kept = 0;
            ulong matH = cap.DefaultMaterialHandle;
            if (matH == 0) return false;
            int visIdx = -1;
            if (rec.SourceVersion >= DwgVersion.AC1027)
                for (int i = from; i < refs.Count; i++)
                    if (refs[i].code == 5) visIdx = i;
            for (int i = from; i < refs.Count; i++)
            {
                var (st, bits, code, val) = refs[i];
                if (code != 5) return false;
                if (val == matH || i == visIdx) continue;
                CopyBits(hw, src, st, bits);
                kept++;
            }
            handles = hw;

            if (DebugTrace)
                System.Console.Error.WriteLine(
                    $"LAYR15 h={rec.Handle:X} {rec.SourceVersion}→AC1015 name='{cap.Name}' " +
                    $"xref=({resolved != 0},{resolved},{cap.XrefDep}) nr={nr} xdic={xdic} " +
                    $"cmc=0x{trueColor:X8} start={start} flagsStart={flagsStart} cs={cs} ce={ce} ss={ss} " +
                    $"body={body.BitPosition} hs={hw.BitPosition}位");
            return true;
        }
        catch
        {
            return false;
        }
    }
}
