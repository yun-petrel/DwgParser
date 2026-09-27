
using System.Collections.Generic;
using DwgParser.Core.IO;

namespace DwgParser.Core.Objects;

public static partial class DwgObjectStreamWriter
{
    private static bool CarriesMLeaderR2007Down(DwgObjectRecord rec, Target t)
        => rec.LogicalType == TypeMLeader
           && rec.SourceVersion == DwgVersion.AC1021
           && (t.Version == DwgVersion.AC1018 || t.Version == DwgVersion.AC1015)
           && rec.GeoStartBit > 0;

    internal static bool CarryMLeaderR2007Down(BitWriter body, DwgObjectRecord rec, byte[] src,
        long geoStart, int geoLen, Target t)
    {
        if (!CarriesMLeaderR2007Down(rec, t)) return false;
        long ss = rec.StringStreamStartBit;
        long geoEnd = geoStart + geoLen;
        if (ss <= geoStart || ss > geoEnd) return false;
        geoEnd = ss;

        long entry = body.BitPosition;
        var dbg = new List<string>();
        bool fold = t.Version == DwgVersion.AC1015;
        try
        {
            var r = new BitReader(src, 0) { UnicodeStrings = true };
            r.SetPosition((int)(geoStart >> 3), (int)(geoStart & 7));
            var str = new BitReader(src, 0) { UnicodeStrings = true };
            str.SetPosition((int)(ss >> 3), (int)(ss & 7));
            r.StringStream = str;

            Leaders(r, body, src, r2010: false);
            ContextDn(r, body, src, dbg, fold);
            TopCommonDn(r, body, src, dbg, fold);
            TopTailDn(r, body, src, dbg);

            long stop = r.BitPosition;
            if (stop != geoEnd)
            {
                if (DebugTrace)
                    System.Console.Error.WriteLine(
                        $"MLDN-MISS h={rec.Handle:X} stop={stop} ss={geoEnd} Δ={geoEnd - stop}");
                body.TrimLastBits(body.BitPosition - entry);
                return false;
            }
            if (DebugTrace)
                System.Console.Error.WriteLine(
                    $"MLDN h={rec.Handle:X} {rec.SourceVersion}→{t.Version} " +
                    $"body={geoEnd - geoStart} out={body.BitPosition - entry} strs={dbg.Count} " +
                    $"[{string.Join("|", dbg.ConvertAll(s => s.Length.ToString()))}]");
            return true;
        }
        catch (System.Exception)
        {
            body.TrimLastBits(body.BitPosition - entry);
            return false;
        }
    }


    private static void ContextDn(BitReader r, BitWriter w, byte[] src, List<string> dbg, bool fold)
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
            TvDn(r, w, dbg);
            Copy3Bd(r, w, src);
            Copy3Bd(r, w, src);
            Copy3Bd(r, w, src);
            CopyBd(r, w, src);
            CopyBd(r, w, src);
            CopyBd(r, w, src);
            CopyBd(r, w, src);
            CopyBs(r, w, src);
            CmcDn(r, w, src, dbg, fold);
            CopyBs(r, w, src);
            CopyBs(r, w, src);
            CmcDn(r, w, src, dbg, fold);
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
            CmcDn(r, w, src, dbg, fold);
            for (int i = 0; i < 16; i++) CopyBd(r, w, src);
        }

        Copy3Bd(r, w, src);
        Copy3Bd(r, w, src);
        Copy3Bd(r, w, src);
        CopyB(r, w, src);
    }

    private static void TopCommonDn(BitReader r, BitWriter w, byte[] src, List<string> dbg, bool fold)
    {
        CopyBl(r, w, src);
        CopyBs(r, w, src);
        CmcDn(r, w, src, dbg, fold);
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
        CmcDn(r, w, src, dbg, fold);
        CopyB(r, w, src);
        CmcDn(r, w, src, dbg, fold);
        Copy3Bd(r, w, src);
        CopyBd(r, w, src);
        CopyBs(r, w, src);
        CopyB(r, w, src);
    }

    private static void TopTailDn(BitReader r, BitWriter w, byte[] src, List<string> dbg)
    {
        int na = RdBl(r, w, src);
        for (int i = 0; i < na; i++) CopyB(r, w, src);
        int nb = RdBl(r, w, src);
        for (int i = 0; i < nb; i++)
        {
            TvDn(r, w, dbg);
            CopyBs(r, w, src);
            CopyBd(r, w, src);
        }
        CopyB(r, w, src);
        CopyBs(r, w, src);
        CopyBs(r, w, src);
        CopyBd(r, w, src);
    }

    private static void CmcDn(BitReader r, BitWriter w, byte[] src, List<string> dbg, bool fold)
    {
        long p = r.BitPosition;
        r.ReadBS();
        int rgb = r.ReadBL();
        byte flag = r.ReadRC();
        if (fold) w.WriteBS(FoldCmcToPreR2004Index(unchecked((uint)rgb)));
        else CopyBits(w, src, p, (int)(r.BitPosition - p));
        if (flag >= 4) return;
        if (fold)
        {
            if ((flag & 1) != 0) dbg.Add(r.ReadTV());
            if ((flag & 2) != 0) dbg.Add(r.ReadTV());
            return;
        }
        if ((flag & 1) != 0) TvDn(r, w, dbg);
        if ((flag & 2) != 0) TvDn(r, w, dbg);
    }

    private static void TvDn(BitReader r, BitWriter w, List<string> dbg)
    {
        string s = r.ReadTV();
        dbg.Add(s);
        w.WriteTV(s, false);
    }
}
