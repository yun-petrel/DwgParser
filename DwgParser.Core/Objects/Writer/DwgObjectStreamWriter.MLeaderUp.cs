
using System.Collections.Generic;
using DwgParser.Core.IO;

namespace DwgParser.Core.Objects;

public static partial class DwgObjectStreamWriter
{
    private static bool CarriesMLeaderUp(DwgObjectRecord rec, Target t)
        => rec.LogicalType == TypeMLeader
        && (rec.SourceVersion == DwgVersion.AC1015 || rec.SourceVersion == DwgVersion.AC1018)
        && t.Version == DwgVersion.AC1021
        && rec.GeoStartBit > 0;

    private static bool ConvertMLeaderUp(BitWriter w, DwgObjectRecord rec, byte[] src, Target t)
    {
        long start = rec.GeoStartBit;
        long end = rec.HandleStreamStartBit > 0
            ? rec.HandleStreamStartBit
            : (long)rec.BodyEnd * 8;
        if (end <= start || start + 8 >= (long)src.Length * 8) return false;

        long entry = w.BitPosition;
        var ss = new List<string>();
        try
        {
            var r = new BitReader(src, 0) { UnicodeStrings = false };
            r.SetPosition((int)(start >> 3), (int)(start & 7));

            bool widen = rec.SourceVersion < DwgVersion.AC1018;

            _ = Leaders(r, w, src, r2010: false);
            Context(r, w, src, ss, widen, r2010: false);
            TopCommon(r, w, src, ss, widen);
            TopTail(r, w, src, ss, r2010: false);

            long stop = r.BitPosition;
            if (stop != end)
            {
                if (DebugTrace)
                    System.Console.Error.WriteLine(
                        $"MLEADER-MISS h={rec.Handle:X} stop={stop} end={end} Δ={end - stop}");
                w.TrimLastBits(w.BitPosition - entry);
                return false;
            }

            long ssBits = WriteCentralStringTail(w, ss);

            rec.WroteCentralStringStream = true;
            if (DebugTrace)
                System.Console.Error.WriteLine(
                    $"MLEADER-UP h={rec.Handle:X} {rec.SourceVersion}→{t.Version} " +
                    $"body={end - start} strings={ss.Count} ssBits={ssBits} out={w.BitPosition - entry}");
            return true;
        }
        catch (System.Exception)
        {
            w.TrimLastBits(w.BitPosition - entry);
            return false;
        }
    }


    private static long WriteCentralStringTail(BitWriter w, List<string> ss)
    {
        long ssStart = w.BitPosition;
        foreach (var s in ss)
        {
            w.WriteBS(unchecked((short)s.Length));
            foreach (char c in s) w.WriteRS((ushort)c);
        }
        long ssBits = w.BitPosition - ssStart;
        if (ssBits > 0x7FFF)
        {
            w.WriteRS(unchecked((ushort)(ssBits >> 15)));
            w.WriteRS(unchecked((ushort)((ssBits & 0x7FFF) | 0x8000)));
        }
        else
        {
            w.WriteRS(unchecked((ushort)ssBits));
        }
        w.WriteB(true);
        return ssBits;
    }

    private static int Leaders(BitReader r, BitWriter w, byte[] src, bool r2010)
    {
        int lines = 0;
        int n = RdBl(r, w, src);
        for (int i = 0; i < n; i++)
        {
            bool last = RdB(r, w, src);
            bool dog = RdB(r, w, src);
            if (last) Copy3Bd(r, w, src);
            if (dog) Copy3Bd(r, w, src);
            int nb = RdBl(r, w, src);
            for (int k = 0; k < nb; k++) { Copy3Bd(r, w, src); Copy3Bd(r, w, src); }
            CopyBl(r, w, src);
            CopyBd(r, w, src);
            int nl = RdBl(r, w, src);
            lines += nl;
            for (int l = 0; l < nl; l++)
            {
                int np = RdBl(r, w, src);
                for (int p = 0; p < np; p++) Copy3Bd(r, w, src);
                int lb = RdBl(r, w, src);
                for (int k = 0; k < lb; k++) { Copy3Bd(r, w, src); Copy3Bd(r, w, src); }
                CopyBl(r, w, src);
                if (r2010) WriteMLeaderLineR2010(w);
            }
            if (r2010) w.WriteBS(0);
        }
        return lines;
    }

    private static void WriteMLeaderLineR2010(BitWriter w)
    {
        w.WriteBS(1);
        w.WriteBS(0);
        w.WriteBL(unchecked((int)0xC1000000u));
        w.WriteRC(0);
        w.WriteBL(-2);
        w.WriteBD(0.0);
        w.WriteBL(0);
    }

    private static void Context(BitReader r, BitWriter w, byte[] src, List<string> ss, bool widen,
        bool r2010)
    {
        CopyBd(r, w, src);
        Copy3Bd(r, w, src);
        CopyBd(r, w, src);
        CopyBd(r, w, src);
        CopyBd(r, w, src);
        CopyBs(r, w, src);
        CopyBs(r, w, src);
        CopyBs(r, w, src);
        CopyBs(r, w, src);

        if (RdB(r, w, src))
        {
            ss.Add(ReadTv(r));
            Copy3Bd(r, w, src);
            Copy3Bd(r, w, src);
            Copy3Bd(r, w, src);
            CopyBd(r, w, src);
            CopyBd(r, w, src);
            CopyBd(r, w, src);
            CopyBd(r, w, src);
            CopyBs(r, w, src);
            Cmc(r, w, src, ss, widen);
            CopyBs(r, w, src);
            CopyBs(r, w, src);
            Cmc(r, w, src, ss, widen);
            CopyBd(r, w, src);
            CopyBl(r, w, src);
            CopyB(r, w, src);
            CopyB(r, w, src);
            CopyBs(r, w, src);
            CopyB(r, w, src);
            CopyBd(r, w, src);
            CopyBd(r, w, src);
            CopyB(r, w, src);
            int ncs = RdBl(r, w, src);
            for (int i = 0; i < ncs; i++) CopyBd(r, w, src);
            CopyB(r, w, src);
            CopyB(r, w, src);
        }
        else if (RdB(r, w, src))
        {
            Copy3Bd(r, w, src);
            Copy3Bd(r, w, src);
            Copy3Bd(r, w, src);
            CopyBd(r, w, src);
            Cmc(r, w, src, ss, widen);
            for (int i = 0; i < 16; i++) CopyBd(r, w, src);
        }

        Copy3Bd(r, w, src);
        Copy3Bd(r, w, src);
        Copy3Bd(r, w, src);
        CopyB(r, w, src);
        if (r2010)
        {
            w.WriteBS(9);
            w.WriteBS(9);
        }
    }

    private static void TopCommon(BitReader r, BitWriter w, byte[] src, List<string> ss, bool widen)
    {
        CopyBl(r, w, src);
        CopyBs(r, w, src);
        Cmc(r, w, src, ss, widen);
        CopyBl(r, w, src);
        CopyB(r, w, src);
        CopyB(r, w, src);
        CopyBd(r, w, src);
        CopyBd(r, w, src);
        CopyBs(r, w, src);
        CopyBs(r, w, src);
        CopyBs(r, w, src);
        CopyBs(r, w, src);
        CopyBs(r, w, src);
        Cmc(r, w, src, ss, widen);
        CopyB(r, w, src);
        Cmc(r, w, src, ss, widen);
        Copy3Bd(r, w, src);
        CopyBd(r, w, src);
        CopyBs(r, w, src);
        CopyB(r, w, src);
    }

    private static void TopTail(BitReader r, BitWriter w, byte[] src, List<string> ss, bool r2010)
    {
        int na;
        if (r2010)
        {
            na = r.ReadBL();
            if (na != 0)
                throw new System.IO.InvalidDataException("升 R2010+ 带 arrowheads ⇒ 出射程");
        }
        else
        {
            na = RdBl(r, w, src);
            for (int i = 0; i < na; i++) CopyB(r, w, src);
        }
        int nb = RdBl(r, w, src);
        if (r2010 && nb != 0)
            throw new System.IO.InvalidDataException("升 R2010+ 带 blocklabels ⇒ 出射程");
        for (int i = 0; i < nb; i++)
        {
            ss.Add(ReadTv(r));
            CopyBs(r, w, src);
            CopyBd(r, w, src);
        }
        CopyB(r, w, src);
        CopyBs(r, w, src);
        CopyBs(r, w, src);
        CopyBd(r, w, src);
        if (r2010)
        {
            w.WriteBS(0);
            w.WriteBS(9);
            w.WriteBS(9);
        }
    }

    private static void Cmc(BitReader r, BitWriter w, byte[] src, List<string> ss, bool widen)
    {
        if (widen)
        {
            short idx = r.ReadBS();
            w.WriteBS(0);
            w.WriteBL(unchecked((int)Records.VisualStyleCarryAcR2000ToAc1021.UpgradedRgb(idx)));
            for (int i = 0; i < 8; i++) w.WriteB(false);
            return;
        }
        long p = r.BitPosition;
        r.ReadBS();
        r.ReadBL();
        byte flag = r.ReadRC();
        CopyBits(w, src, p, (int)(r.BitPosition - p));
        if (flag >= 4) return;
        if ((flag & 1) != 0) ss.Add(ReadTv(r));
        if ((flag & 2) != 0) ss.Add(ReadTv(r));
    }


    private static int RdBl(BitReader r, BitWriter w, byte[] src)
    {
        long p = r.BitPosition;
        long v = r.ReadBL();
        if (v is < 0 or > 100000) throw new System.IO.EndOfStreamException("MLeader BL");
        CopyBits(w, src, p, (int)(r.BitPosition - p));
        return (int)v;
    }

    private static void Copy3Bd(BitReader r, BitWriter w, byte[] src)
    {
        CopyBd(r, w, src); CopyBd(r, w, src); CopyBd(r, w, src);
    }

    private static void CopyB(BitReader r, BitWriter w, byte[] src)
    {
        long p = r.BitPosition; r.ReadB();
        CopyBits(w, src, p, 1);
    }

    private static bool RdB(BitReader r, BitWriter w, byte[] src)
    {
        long p = r.BitPosition;
        bool v = r.ReadB();
        CopyBits(w, src, p, 1);
        return v;
    }

}
