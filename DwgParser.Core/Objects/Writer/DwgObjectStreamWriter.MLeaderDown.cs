
using DwgParser.Core.IO;

namespace DwgParser.Core.Objects;

public static partial class DwgObjectStreamWriter
{

    private static bool CarriesMLeaderTextExtUp(DwgObjectRecord rec, Target t)
        => rec.LogicalType == TypeMLeader
        && rec.SourceVersion == DwgVersion.AC1024
        && (t.Version == DwgVersion.AC1027 || t.Version == DwgVersion.AC1032)
        && rec.GeoStartBit > 0;

    private static bool AppendMLeaderTextExtendedUp(BitWriter w, DwgObjectRecord rec, byte[] src,
        Target t, long start, int len)
    {
        long ss = rec.StringStreamStartBit;
        long end = start + len;
        if (ss <= start || ss >= end || end > (long)src.Length * 8) return false;
        long entry = w.BitPosition;
        try
        {
            CopyBits(w, src, start, (int)(ss - start));
            w.WriteB(false);
            CopyBits(w, src, ss, (int)(end - ss));
            if (DebugTrace)
                System.Console.Error.WriteLine(
                    $"MLEADER-TXTX h={rec.Handle:X} {rec.SourceVersion}→{t.Version} " +
                    $"body={ss - start} ins@={ss} out={w.BitPosition - entry}");
            return true;
        }
        catch (System.Exception)
        {
            w.TrimLastBits(w.BitPosition - entry);
            return false;
        }
    }


    private static bool CarriesMLeaderDown(DwgObjectRecord rec, Target t)
        => rec.LogicalType == TypeMLeader
        && rec.SourceVersion.IsR2010Plus()
        && t.Version == DwgVersion.AC1021
        && rec.GeoStartBit > 0;

    private static bool ConvertMLeaderDown(BitWriter w, DwgObjectRecord rec, byte[] src,
        Target t, long start, int len)
    {
        long ss = rec.StringStreamStartBit;
        long end = start + len;
        if (ss <= start || ss >= end || end > (long)src.Length * 8) return false;

        long entry = w.BitPosition;
        rec.MLeaderDownDropTypeAxisRefs = 0;
        BitReader? rr = null;
        try
        {
            var r = new BitReader(src, 0) { UnicodeStrings = true };
            rr = r;
            DwgObjectRecord.SeekTo(r, start);
            MldBs(r, w, src, false);
            int lines = MldDownLeaders(r, w, src);
            MldDownContext(r, w, src);
            MldBs(r, w, src, false);
            MldBs(r, w, src, false);
            MldDownTopCommon(r, w, src);
            w.WriteBL(0);
            MldDownCommonTail(r, w, src);
            MldBs(r, w, src, false);
            MldBs(r, w, src, false);
            MldBs(r, w, src, false);
            if (rec.SourceVersion >= DwgVersion.AC1027)
                MldB(r, w, src, false);

            if (r.BitPosition != ss || lines * 2 > rec.MLeaderTypeAxisRefs.Count)
            {
                if (DebugTrace)
                    System.Console.Error.WriteLine(
                        $"MLEADER-DOWN-MISS h={rec.Handle:X} stop={r.BitPosition} ss={ss} " +
                        $"Δ={ss - r.BitPosition} lines={lines} refs={rec.MLeaderTypeAxisRefs.Count}");
                w.TrimLastBits(w.BitPosition - entry);
                return false;
            }
            CopyBits(w, src, ss, (int)(end - ss));

            rec.MLeaderDownDropTypeAxisRefs = lines * 2;
            if (DebugTrace)
                System.Console.Error.WriteLine(
                    $"MLEADER-DOWN h={rec.Handle:X} {rec.SourceVersion}→{t.Version} " +
                    $"body={ss - start} out={w.BitPosition - entry} lines={lines} " +
                    $"dropRefs={rec.MLeaderDownDropTypeAxisRefs}");
            return true;
        }
        catch (System.Exception e)
        {
            rec.MLeaderDownDropTypeAxisRefs = 0;
            w.TrimLastBits(w.BitPosition - entry);
            if (DebugTrace)
                System.Console.Error.WriteLine(
                    $"MLEADER-DOWN-THROW h={rec.Handle:X} {rec.SourceVersion}→{t.Version} " +
                    $"start={start} len={len} ss={ss} at={rr?.BitPosition.ToString() ?? "-"} " +
                    $"err={e.GetType().Name}:{e.Message}");
            return false;
        }
    }


    private static int MldDownLeaders(BitReader r, BitWriter w, byte[] src)
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
            for (int k = 0; k < nb; k++)
            {
                Mld3Bd(r, w, src, true);
                Mld3Bd(r, w, src, true);
            }
            MldBl(r, w, src, true);
            MldBd(r, w, src, true);
            int nl = MldBlCount(r, w, src, true);
            lines += nl;
            for (int l = 0; l < nl; l++)
            {
                int np = MldBlCount(r, w, src, true);
                for (int p = 0; p < np; p++) Mld3Bd(r, w, src, true);
                int lb = MldBlCount(r, w, src, true);
                for (int k = 0; k < lb; k++)
                {
                    Mld3Bd(r, w, src, true);
                    Mld3Bd(r, w, src, true);
                }
                MldBl(r, w, src, true);
                MldBs(r, w, src, false);
                MldCmc(r, w, src, false);
                MldBl(r, w, src, false);
                MldBd(r, w, src, false);
                MldBl(r, w, src, false);
            }
            MldBs(r, w, src, false);
        }
        return lines;
    }

    private static void MldDownContext(BitReader r, BitWriter w, byte[] src)
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

    private static void MldDownTopCommon(BitReader r, BitWriter w, byte[] src)
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

    private static void MldDownCommonTail(BitReader r, BitWriter w, byte[] src)
    {
        if (MldBlCount(r, w, src, true) != 0)
            throw new InvalidDataException("MLEADER-DOWN 带 blocklabels ⇒ 出射程");
        MldB(r, w, src, true);
        MldBs(r, w, src, true);
        MldBs(r, w, src, true);
        MldBd(r, w, src, true);
    }


    private static bool MldB(BitReader r, BitWriter w, byte[] src, bool copy)
    {
        long p = r.BitPosition;
        bool v = r.ReadB();
        if (copy) CopyBits(w, src, p, 1);
        return v;
    }

    private static int MldBl(BitReader r, BitWriter w, byte[] src, bool copy)
    {
        long p = r.BitPosition;
        int v = r.ReadBL();
        if (copy) CopyBits(w, src, p, (int)(r.BitPosition - p));
        return v;
    }

    private static int MldBlCount(BitReader r, BitWriter w, byte[] src, bool copy)
    {
        int v = MldBl(r, w, src, copy);
        if (v is < 0 or > 1024) throw new InvalidDataException($"MLEADER-DOWN 计数越界 v={v}");
        return v;
    }

    private static void MldBs(BitReader r, BitWriter w, byte[] src, bool copy)
    {
        long p = r.BitPosition;
        r.ReadBS();
        if (copy) CopyBits(w, src, p, (int)(r.BitPosition - p));
    }

    private static void MldBd(BitReader r, BitWriter w, byte[] src, bool copy)
    {
        long p = r.BitPosition;
        r.ReadBD();
        if (copy) CopyBits(w, src, p, (int)(r.BitPosition - p));
    }

    private static void Mld3Bd(BitReader r, BitWriter w, byte[] src, bool copy)
    {
        long p = r.BitPosition;
        r.ReadBD(); r.ReadBD(); r.ReadBD();
        if (copy) CopyBits(w, src, p, (int)(r.BitPosition - p));
    }

    private static void MldCmc(BitReader r, BitWriter w, byte[] src, bool copy)
    {
        long p = r.BitPosition;
        r.ReadBS(); r.ReadBL(); r.ReadRC();
        if (copy) CopyBits(w, src, p, (int)(r.BitPosition - p));
    }
}
