
using System.Collections.Generic;
using DwgParser.Core.IO;

namespace DwgParser.Core.Objects;

public static partial class DwgObjectStreamWriter
{
    private static bool CarriesMLeaderR2010ToAc1018(DwgObjectRecord rec, Target t)
        => rec.LogicalType == TypeMLeader
           && rec.SourceVersion.IsR2010Plus()
           && t.Version == DwgVersion.AC1018
           && rec.GeoStartBit > 0;

    internal static bool ConvertMLeaderR2010ToAc1018(BitWriter w, DwgObjectRecord rec, byte[] src,
        Target t, long start, int len)
    {
        if (!CarriesMLeaderR2010ToAc1018(rec, t)) return false;
        if (Ml1018Var() == 0) return false;
        long ss = rec.StringStreamStartBit, hs = rec.HandleStreamStartBit, end = start + len;
        if (ss <= start || ss >= end || hs <= ss || end > (long)src.Length * 8) return false;

        long entry = w.BitPosition;
        rec.MLeaderDownDropTypeAxisRefs = 0;
        var dbg = new List<string>();
        BitReader? rr = null;
        try
        {
            var r = new BitReader(src, 0) { UnicodeStrings = true };
            rr = r;
            DwgObjectRecord.SeekTo(r, start);
            var str = new BitReader(src, 0) { UnicodeStrings = true };
            DwgObjectRecord.SeekTo(str, ss);
            r.StringStream = str;

            MldBs(r, w, src, false);
            int lines = MldDownLeaders(r, w, src);
            Ml1018Context(r, w, src, dbg);
            MldBs(r, w, src, false);
            MldBs(r, w, src, false);
            Ml1018TopCommon(r, w, src, dbg);
            w.WriteBL(0);
            Ml1018CommonTail(r, w, src, dbg);
            MldBs(r, w, src, false);
            MldBs(r, w, src, false);
            MldBs(r, w, src, false);
            if (rec.SourceVersion >= DwgVersion.AC1027)
                MldB(r, w, src, false);

            long dataSize = Ml1018StringAreaBits(src, ss, hs);
            if (r.BitPosition != ss || lines * 2 > rec.MLeaderTypeAxisRefs.Count
                || str.BitPosition != ss + dataSize)
            {
                if (DebugTrace)
                    System.Console.Error.WriteLine(
                        $"MLDN1018-MISS h={rec.Handle:X} stop={r.BitPosition} ss={ss} Δ={ss - r.BitPosition} " +
                        $"lines={lines} refs={rec.MLeaderTypeAxisRefs.Count} " +
                        $"strEnd={str.BitPosition} strWant={ss + dataSize} Δstr={str.BitPosition - ss - dataSize}");
                w.TrimLastBits(w.BitPosition - entry);
                return false;
            }
            rec.MLeaderDownDropTypeAxisRefs = lines * 2;
            if (DebugTrace)
                System.Console.Error.WriteLine(
                    $"MLDN1018 h={rec.Handle:X} {rec.SourceVersion}→{t.Version} body={ss - start} " +
                    $"out={w.BitPosition - entry} lines={lines} dropRefs={rec.MLeaderDownDropTypeAxisRefs} " +
                    $"strs={dbg.Count} strArea={dataSize}");
            return true;
        }
        catch (System.Exception e)
        {
            rec.MLeaderDownDropTypeAxisRefs = 0;
            w.TrimLastBits(w.BitPosition - entry);
            if (DebugTrace)
                System.Console.Error.WriteLine(
                    $"MLDN1018-THROW h={rec.Handle:X} {rec.SourceVersion}→{t.Version} start={start} len={len} " +
                    $"ss={ss} at={rr?.BitPosition.ToString() ?? "-"} err={e.GetType().Name}:{e.Message}");
            return false;
        }
    }

    private static long Ml1018StringAreaBits(byte[] src, long ss, long hs)
    {
        var foot = new BitReader(src, 0) { UnicodeStrings = true };
        foot.SetPosition((int)((hs - 17) >> 3), (int)((hs - 17) & 7));
        uint dataSize = foot.ReadRS();
        if ((dataSize & 0x8000) != 0)
        {
            foot.SetPosition((int)((hs - 33) >> 3), (int)((hs - 33) & 7));
            dataSize = (dataSize & 0x7FFF) | ((uint)foot.ReadRS() << 15);
        }
        return dataSize;
    }

    private static int Ml1018Var()
    {
        var s = System.Environment.GetEnvironmentVariable("ML1018");
        return int.TryParse(s, out var v) ? v : 1;
    }


    private static void Ml1018Context(BitReader r, BitWriter w, byte[] src, List<string> dbg)
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
            TvDn(r, w, dbg);
            Mld3Bd(r, w, src, true);
            Mld3Bd(r, w, src, true);
            Mld3Bd(r, w, src, true);
            MldBd(r, w, src, true);
            MldBd(r, w, src, true);
            MldBd(r, w, src, true);
            MldBd(r, w, src, true);
            MldBs(r, w, src, true);
            CmcDn(r, w, src, dbg, fold: false);
            MldBs(r, w, src, true);
            MldBs(r, w, src, true);
            CmcDn(r, w, src, dbg, fold: false);
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
            CmcDn(r, w, src, dbg, fold: false);
            for (int i = 0; i < 16; i++) MldBd(r, w, src, true);
        }
        Mld3Bd(r, w, src, true);
        Mld3Bd(r, w, src, true);
        Mld3Bd(r, w, src, true);
        MldB(r, w, src, true);
    }

    private static void Ml1018TopCommon(BitReader r, BitWriter w, byte[] src, List<string> dbg)
    {
        MldBl(r, w, src, true);
        MldBs(r, w, src, true);
        CmcDn(r, w, src, dbg, fold: false);
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
        CmcDn(r, w, src, dbg, fold: false);
        MldB(r, w, src, true);
        CmcDn(r, w, src, dbg, fold: false);
        Mld3Bd(r, w, src, true);
        MldBd(r, w, src, true);
        MldBs(r, w, src, true);
        MldB(r, w, src, true);
    }

    private static void Ml1018CommonTail(BitReader r, BitWriter w, byte[] src, List<string> dbg)
    {
        int nb = MldBlCount(r, w, src, true);
        if (nb != 0) throw new InvalidDataException("MLDN1018 带 blocklabels ⇒ 出射程");
        MldB(r, w, src, true);
        MldBs(r, w, src, true);
        MldBs(r, w, src, true);
        MldBd(r, w, src, true);
    }
}
