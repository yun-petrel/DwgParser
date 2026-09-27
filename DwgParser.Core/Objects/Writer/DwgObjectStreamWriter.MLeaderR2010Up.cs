
using System.IO;
using DwgParser.Core.IO;

namespace DwgParser.Core.Objects;

public static partial class DwgObjectStreamWriter
{
    private static bool CarriesMLeaderR2010Up(DwgObjectRecord rec, Target t)
    {
        if (rec.LogicalType != TypeMLeader || rec.GeoStartBit <= 0) return false;
        if (t.Version != DwgVersion.AC1024 && t.Version != DwgVersion.AC1027 && t.Version != DwgVersion.AC1032)
            return false;
        return rec.SourceVersion switch
        {
            DwgVersion.AC1021 => rec.StringStreamStartBit > 0,
            DwgVersion.AC1015 or DwgVersion.AC1018 => rec.HandleStreamStartBit > 0,
            _ => false,
        };
    }

    private static bool ConvertMLeaderR2010Up(BitWriter w, DwgObjectRecord rec, byte[] src,
        Target t, long start, int len)
    {
        long ss = rec.StringStreamStartBit;
        long end = start + len;
        bool inPath1 = rec.SourceVersion == DwgVersion.AC1021;
        if (inPath1)
        {
            if (ss <= start || ss >= end || end > (long)src.Length * 8) return false;
        }
        else
        {
            if (end <= start || end > (long)src.Length * 8) return false;
        }

        long entry = w.BitPosition;
        rec.MLeaderUpAddTypeAxisRefs = 0;
        BitReader? rr = null;
        try
        {
            var r = new BitReader(src, 0) { UnicodeStrings = true };
            rr = r;
            DwgObjectRecord.SeekTo(r, start);
            int lines;

            if (rec.SourceVersion == DwgVersion.AC1021)
            {
                w.WriteBS(2);
                lines = MldUpLeaders(r, w, src);
                MldUpContext(r, w, src);
                w.WriteBS(9);
                w.WriteBS(9);
                MldUpTopCommon(r, w, src);
                MldUpSkipArrowheads(r, w, src);
                MldUpCommonTail(r, w, src);
                w.WriteBS(0);
                w.WriteBS(9);
                w.WriteBS(9);
                if (t.Version >= DwgVersion.AC1027)
                    w.WriteB(false);

                if (r.BitPosition != ss)
                {
                    if (DebugTrace)
                        System.Console.Error.WriteLine(
                            $"MLEADER-UP10-MISS h={rec.Handle:X} stop={r.BitPosition} ss={ss} " +
                            $"Δ={ss - r.BitPosition} lines={lines}");
                    w.TrimLastBits(w.BitPosition - entry);
                    return false;
                }
                CopyBits(w, src, ss, (int)(end - ss));
            }
            else
            {
                var ssx = new System.Collections.Generic.List<string>();
                bool widen = rec.SourceVersion < DwgVersion.AC1018;
                w.WriteBS(2);
                lines = Leaders(r, w, src, r2010: true);
                Context(r, w, src, ssx, widen, r2010: true);
                TopCommon(r, w, src, ssx, widen);
                TopTail(r, w, src, ssx, r2010: true);
                if (t.Version >= DwgVersion.AC1027)
                    w.WriteB(false);

                long stop = r.BitPosition;
                if (stop != end)
                {
                    if (DebugTrace)
                        System.Console.Error.WriteLine(
                            $"MLEADER-UP10X-MISS h={rec.Handle:X} stop={stop} end={end} " +
                            $"Δ={end - stop} lines={lines}");
                    w.TrimLastBits(w.BitPosition - entry);
                    return false;
                }
                WriteCentralStringTail(w, ssx);
                rec.WroteCentralStringStream = true;
            }

            rec.MLeaderUpAddTypeAxisRefs = lines * 2;
            if (DebugTrace)
                System.Console.Error.WriteLine(
                    $"MLEADER-UP10 h={rec.Handle:X} {rec.SourceVersion}→{t.Version} " +
                    $"body={(inPath1 ? ss : end) - start} out={w.BitPosition - entry} lines={lines} " +
                    $"addRefs={rec.MLeaderUpAddTypeAxisRefs}");
            return true;
        }
        catch (System.Exception e)
        {
            rec.MLeaderUpAddTypeAxisRefs = 0;
            w.TrimLastBits(w.BitPosition - entry);
            if (DebugTrace)
                System.Console.Error.WriteLine(
                    $"MLEADER-UP10-THROW h={rec.Handle:X} {rec.SourceVersion}→{t.Version} " +
                    $"start={start} len={len} ss={ss} at={rr?.BitPosition.ToString() ?? "-"} " +
                    $"err={e.GetType().Name}:{e.Message}");
            return false;
        }
    }


    private static int MldUpLeaders(BitReader r, BitWriter w, byte[] src)
    {
        int n = MldBlCount(r, w, src, true);
        int lines = 0;
        for (int i = 0; i < n; i++)
        {
            bool hl = MldB(r, w, src, true);
            bool dg = MldB(r, w, src, true);
            if (hl) Mld3Bd(r, w, src, true);
            if (dg) Mld3Bd(r, w, src, true);
            int nb = MldBlCount(r, w, src, true);
            for (int k = 0; k < nb; k++) { Mld3Bd(r, w, src, true); Mld3Bd(r, w, src, true); }
            MldBl(r, w, src, true);
            MldBd(r, w, src, true);
            int nl = MldBlCount(r, w, src, true);
            lines += nl;
            for (int l = 0; l < nl; l++)
            {
                int np = MldBlCount(r, w, src, true);
                for (int p = 0; p < np; p++) Mld3Bd(r, w, src, true);
                int lb = MldBlCount(r, w, src, true);
                for (int k = 0; k < lb; k++) { Mld3Bd(r, w, src, true); Mld3Bd(r, w, src, true); }
                MldBl(r, w, src, true);
                w.WriteBS(1);
                w.WriteBS(0);
                w.WriteBL(unchecked((int)0xC1000000u));
                w.WriteRC(0);
                w.WriteBL(-2);
                w.WriteBD(0.0);
                w.WriteBL(0);
            }
            w.WriteBS(0);
        }
        return lines;
    }

    private static void MldUpContext(BitReader r, BitWriter w, byte[] src)
    {
        MldBd(r, w, src, true);
        Mld3Bd(r, w, src, true);
        MldBd(r, w, src, true);
        MldBd(r, w, src, true);
        MldBd(r, w, src, true);
        MldBs(r, w, src, true);
        MldBs(r, w, src, true);
        MldBs(r, w, src, true);
        MldBs(r, w, src, true);
        if (MldB(r, w, src, true))
        {
            Mld3Bd(r, w, src, true);
            Mld3Bd(r, w, src, true);
            Mld3Bd(r, w, src, true);
            MldBd(r, w, src, true);
            MldBd(r, w, src, true);
            MldBd(r, w, src, true);
            MldBd(r, w, src, true);
            MldBs(r, w, src, true);
            MldCmc(r, w, src, true);
            MldBs(r, w, src, true);
            MldBs(r, w, src, true);
            MldCmc(r, w, src, true);
            MldBd(r, w, src, true);
            MldBl(r, w, src, true);
            MldB(r, w, src, true);
            MldB(r, w, src, true);
            MldBs(r, w, src, true);
            MldB(r, w, src, true);
            MldBd(r, w, src, true);
            MldBd(r, w, src, true);
            MldB(r, w, src, true);
            int ncs = MldBlCount(r, w, src, true);
            for (int i = 0; i < ncs; i++) MldBd(r, w, src, true);
            MldB(r, w, src, true);
            MldB(r, w, src, true);
        }
        else if (MldB(r, w, src, true))
        {
            Mld3Bd(r, w, src, true);
            Mld3Bd(r, w, src, true);
            Mld3Bd(r, w, src, true);
            MldBd(r, w, src, true);
            MldCmc(r, w, src, true);
            for (int i = 0; i < 16; i++) MldBd(r, w, src, true);
        }
        Mld3Bd(r, w, src, true);
        Mld3Bd(r, w, src, true);
        Mld3Bd(r, w, src, true);
        MldB(r, w, src, true);
    }

    private static void MldUpTopCommon(BitReader r, BitWriter w, byte[] src)
    {
        MldBl(r, w, src, true);
        MldBs(r, w, src, true);
        MldCmc(r, w, src, true);
        MldBl(r, w, src, true);
        MldB(r, w, src, true);
        MldB(r, w, src, true);
        MldBd(r, w, src, true);
        MldBd(r, w, src, true);
        MldBs(r, w, src, true);
        MldBs(r, w, src, true);
        MldBs(r, w, src, true);
        MldBs(r, w, src, true);
        MldBs(r, w, src, true);
        MldCmc(r, w, src, true);
        MldB(r, w, src, true);
        MldCmc(r, w, src, true);
        Mld3Bd(r, w, src, true);
        MldBd(r, w, src, true);
        MldBs(r, w, src, true);
        MldB(r, w, src, true);
    }

    private static void MldUpSkipArrowheads(BitReader r, BitWriter w, byte[] src)
    {
        int na = MldBlCount(r, w, src, false);
        if (na != 0) throw new InvalidDataException("MLEADER-UP10 带 arrowheads ⇒ 出射程");
    }

    private static void MldUpCommonTail(BitReader r, BitWriter w, byte[] src)
    {
        if (MldBlCount(r, w, src, true) != 0)
            throw new InvalidDataException("MLEADER-UP10 带 blocklabels ⇒ 出射程");
        MldB(r, w, src, true);
        MldBs(r, w, src, true);
        MldBs(r, w, src, true);
        MldBd(r, w, src, true);
    }
}
