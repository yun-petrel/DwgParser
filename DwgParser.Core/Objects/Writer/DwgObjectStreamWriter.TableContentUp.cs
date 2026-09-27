
using System.Collections.Generic;
using DwgParser.Core.IO;

namespace DwgParser.Core.Objects;

public static partial class DwgObjectStreamWriter
{
    private static bool CarriesThinTableContentUp(DwgObjectRecord rec, Target t)
    {
        if (!t.R2007Plus) return false;
        if (rec.SourceVersion < DwgVersion.AC1015 || rec.SourceVersion.IsR2007Plus()) return false;
        return rec.TableContent is { Captured: true }
            || rec.LogicalType == TypeTableContentCode
            || ClassTypeCodes.Normalize(rec.ClassName) == "TABLECONTENT";
    }

    private static bool ConvertThinTableContentUp(BitWriter w, DwgObjectRecord rec, byte[] src,
        long start, int len, Target t, out BitWriter? nativeHandles)
    {
        nativeHandles = null;
        if (len <= 42 || start < 10) return false;
        long entry = w.BitPosition;
        try
        {
            long objStartBit = (long)rec.BodyStart * 8;
            var r = new BitReader(src, 0) { UnicodeStrings = false };
            r.SetPosition((int)(objStartBit >> 3), (int)(objStartBit & 7));
            if (r.ReadBS() != rec.TypeCode) return false;
            uint rl = r.ReadRL();
            long dataEnd = objStartBit + rl;
            long objEndBit = start + len;
            if (rl < 76 || dataEnd >= objEndBit) return false;

            var own = r.ReadHandle();
            long eedStart = r.BitPosition;
            if (!DwgObjectReader.TrySkipEed(r)) return false;
            long eedEnd = r.BitPosition;
            int nreact = (int)r.ReadBL();
            if (nreact is < 0 or > 4096) return false;
            bool xdicMissing = rec.SourceVersion >= DwgVersion.AC1018 && r.ReadB();

            long xdicSlot = -1;
            if (rec.SourceVersion < DwgVersion.AC1018)
            {
                xdicSlot = LocateHandleRef(src, dataEnd, objEndBit, nreact + 2);
                if (xdicSlot > 0 && RefIsNullXdic(src, xdicSlot)) xdicMissing = true;
            }

            w.WriteHandleRef(own.Code, own.Value);
            CopyBits(w, src, eedStart, (int)(eedEnd - eedStart));
            w.WriteBL(nreact);
            w.WriteB(xdicMissing);
            if (t.Version >= DwgVersion.AC1027) w.WriteB(false);

            var ss = new List<string>();
            if (!TableFields(r, w, src, ss)) return false;

            int ng = (int)r.ReadBL();
            if (ng is < 0 or > 64) return false;
            w.WriteBL(ng);
            for (int i = 0; i < ng; i++)
                for (int j = 0; j < 4; j++) CopyBl(r, w, src);

            long tailStart = r.BitPosition;
            int tailBits = (int)(dataEnd - tailStart);
            if (tailBits is < 0 or > 64) return false;

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

            CopyBits(w, src, tailStart, tailBits);

            long hsEnd = HandleRefsEnd(src, dataEnd, objEndBit);
            var hw = new BitWriter(64);
            if (xdicMissing && xdicSlot > dataEnd && xdicSlot + 8 < hsEnd)
            {
                CopyBits(hw, src, dataEnd, (int)(xdicSlot - dataEnd));
                CopyBits(hw, src, xdicSlot + 8, (int)(hsEnd - xdicSlot - 8));
            }
            else
            {
                CopyBits(hw, src, dataEnd, (int)(hsEnd - dataEnd));
            }
            nativeHandles = hw;
            rec.WroteCentralStringStream = true;
            if (DebugTrace)
                System.Console.Error.WriteLine(
                    $"TC531-UP h={rec.Handle:X} ver={t.Version} rl={rl} nreact={nreact} " +
                    $"xdic={xdicMissing} strings={ss.Count} ssBits={ssBits} tail={tailBits} " +
                    $"hs={hw.BitPosition}");
            return true;
        }
        catch (System.Exception)
        {
            w.TrimLastBits(w.BitPosition - entry);
            nativeHandles = null;
            return false;
        }
    }

    private static long HandleRefsEnd(byte[] src, long from, long end)
    {
        var br = new BitReader(src, 0);
        br.SetPosition((int)(from >> 3), (int)(from & 7));
        long last = from;
        while (br.BitPosition + 8 <= end)
        {
            try { br.ReadHandle(); } catch { break; }
            if (br.BitPosition > end) break;
            last = br.BitPosition;
        }
        return last;
    }


    private static void CopyBl(BitReader r, BitWriter w, byte[] src)
    {
        long p = r.BitPosition; r.ReadBL();
        CopyBits(w, src, p, (int)(r.BitPosition - p));
    }

    private static void CopyBs(BitReader r, BitWriter w, byte[] src)
    {
        long p = r.BitPosition; r.ReadBS();
        CopyBits(w, src, p, (int)(r.BitPosition - p));
    }

    private static void CopyBd(BitReader r, BitWriter w, byte[] src)
    {
        long p = r.BitPosition; r.ReadBD();
        CopyBits(w, src, p, (int)(r.BitPosition - p));
    }

    private static void CopyRd(BitReader r, BitWriter w, byte[] src)
    {
        long p = r.BitPosition; r.ReadRD();
        CopyBits(w, src, p, (int)(r.BitPosition - p));
    }

    private static void CopyH(BitReader r, BitWriter w, byte[] src)
    {
        long p = r.BitPosition; r.ReadHandle();
        CopyBits(w, src, p, (int)(r.BitPosition - p));
    }

    private static void Tvc(BitReader r, List<string> ss) => ss.Add(ReadTv(r));

    private static string ReadTv(BitReader r)
    {
        int n = r.ReadBS();
        if (n < 0 || n > 0x100000) throw new System.IO.EndOfStreamException("531 TV len");
        if (n == 0) return string.Empty;
        var bytes = new byte[n];
        for (int i = 0; i < n; i++) bytes[i] = r.ReadRC();
        return DecodeTv(bytes);
    }

    private static string DecodeTv(byte[] bytes)
    {
        int n = bytes.Length;
        while (n > 0 && bytes[n - 1] == 0) n--;
        if (n == 0) return string.Empty;
        return DecodeCodepageText(bytes, 0, n, 39).TrimEnd('\0');
    }

    private static bool TableFields(BitReader r, BitWriter w, byte[] src, List<string> ss)
    {
        Tvc(r, ss);
        Tvc(r, ss);
        int nc = (int)r.ReadBL();
        if (nc is < 0 or > 64) return false;
        w.WriteBL(nc);
        for (int c = 0; c < nc; c++)
        {
            Tvc(r, ss);
            CopyBl(r, w, src);
            if (!CdList(r, w, src, ss)) return false;
            CellStyle(r, w, src, ss);
            CopyBl(r, w, src);
            CopyBd(r, w, src);
        }
        int nr = (int)r.ReadBL();
        if (nr is < 0 or > 64) return false;
        w.WriteBL(nr);
        for (int row = 0; row < nr; row++)
        {
            int ncell = (int)r.ReadBL();
            if (ncell is < 0 or > 64) return false;
            w.WriteBL(ncell);
            for (int k = 0; k < ncell; k++) Cell(r, w, src, ss);
            CopyBl(r, w, src);
            if (!CdList(r, w, src, ss)) return false;
            CellStyle(r, w, src, ss);
            CopyBl(r, w, src);
            CopyBd(r, w, src);
        }
        int nf = (int)r.ReadBL();
        if (nf is < 0 or > 64) return false;
        w.WriteBL(nf);
        for (int i = 0; i < nf; i++) CopyH(r, w, src);
        CellStyle(r, w, src, ss);
        return true;
    }

    private static bool CdList(BitReader r, BitWriter w, byte[] src, List<string> ss)
    {
        int n = (int)r.ReadBL();
        if (n is < 0 or > 200) return false;
        w.WriteBL(n);
        for (int i = 0; i < n; i++)
        {
            Tvc(r, ss);
            Val(r, w, src, ss);
        }
        return true;
    }

    private static void Cell(BitReader r, BitWriter w, byte[] src, List<string> ss)
    {
        CopyBl(r, w, src);
        Tvc(r, ss);
        CopyBl(r, w, src);
        if (!CdList(r, w, src, ss)) throw new System.IO.EndOfStreamException("531 cell cd");
        long lp = r.BitPosition;
        int lk = (int)r.ReadBL();
        CopyBits(w, src, lp, (int)(r.BitPosition - lp));
        if (lk != 0)
        {
            CopyBl(r, w, src);
            CopyBl(r, w, src);
            CopyBl(r, w, src);
        }
        int n = (int)r.ReadBL();
        if (n is < 0 or > 16) throw new System.IO.EndOfStreamException("531 nContents");
        w.WriteBL(n);
        for (int i = 0; i < n; i++)
        {
            int ct = (int)r.ReadBL();
            w.WriteBL(ct);
            if (ct == 1) Val(r, w, src, ss);
            else if (ct != 0) CopyH(r, w, src);
            int na = (int)r.ReadBL();
            if (na is < 0 or > 16) throw new System.IO.EndOfStreamException("531 natts");
            w.WriteBL(na);
            for (int j = 0; j < na; j++)
            {
                CopyH(r, w, src);
                Tvc(r, ss);
                CopyBl(r, w, src);
            }
            long hp = r.BitPosition;
            short hf = r.ReadBS();
            CopyBits(w, src, hp, (int)(r.BitPosition - hp));
            if (hf != 0) ContentFormat(r, w, src, ss);
        }
        CellStyle(r, w, src, ss);
        CopyBl(r, w, src);
        long up = r.BitPosition;
        int uf = (int)r.ReadBL();
        CopyBits(w, src, up, (int)(r.BitPosition - up));
        if (uf != 0)
        {
            CopyBl(r, w, src);
            CopyBd(r, w, src);
            CopyBd(r, w, src);
            long gp = r.BitPosition;
            int g = (int)r.ReadBL();
            CopyBits(w, src, gp, (int)(r.BitPosition - gp));
            CopyH(r, w, src);
            if (g != 0)
            {
                for (int i = 0; i < 6; i++) CopyBd(r, w, src);
                for (int i = 0; i < 4; i++) CopyBd(r, w, src);
                CopyBl(r, w, src);
            }
        }
    }

    private static void CellStyle(BitReader r, BitWriter w, byte[] src, List<string> ss)
    {
        CopyBl(r, w, src);
        long hp = r.BitPosition;
        short hasData = r.ReadBS();
        CopyBits(w, src, hp, (int)(r.BitPosition - hp));
        if (hasData == 0) return;
        CopyBl(r, w, src);
        CopyBl(r, w, src);
        Cmc(r, w, src, ss);
        CopyBl(r, w, src);
        ContentFormat(r, w, src, ss);
        long mp = r.BitPosition;
        short mf = r.ReadBS();
        CopyBits(w, src, mp, (int)(r.BitPosition - mp));
        if (mf != 0)
            for (int k = 0; k < 6; k++) CopyBd(r, w, src);
        int nb = (int)r.ReadBL();
        if (nb is < 0 or > 8) throw new System.IO.EndOfStreamException("531 nborders");
        w.WriteBL(nb);
        for (int i = 0; i < nb; i++)
        {
            int e = (int)r.ReadBL();
            w.WriteBL(e);
            if (e is 1 or 2 or 4 or 8 or 0x10 or 0x20)
            {
                CopyBl(r, w, src);
                CopyBl(r, w, src);
                Cmc(r, w, src, ss);
                CopyBl(r, w, src);
                CopyBl(r, w, src);
                CopyBd(r, w, src);
            }
        }
    }

    private static void ContentFormat(BitReader r, BitWriter w, byte[] src, List<string> ss)
    {
        CopyBl(r, w, src);
        CopyBl(r, w, src);
        CopyBl(r, w, src);
        CopyBl(r, w, src);
        Tvc(r, ss);
        CopyBd(r, w, src);
        CopyBd(r, w, src);
        CopyBl(r, w, src);
        Cmc(r, w, src, ss);
        CopyBd(r, w, src);
    }

    private static void Cmc(BitReader r, BitWriter w, byte[] src, List<string> ss)
    {
        CopyBs(r, w, src);
        CopyBl(r, w, src);
        long p = r.BitPosition;
        byte fl = r.ReadRC();
        CopyBits(w, src, p, (int)(r.BitPosition - p));
        if (fl != 0) Tvc(r, ss);
    }

    private static void Val(BitReader r, BitWriter w, byte[] src, List<string> ss)
    {
        w.WriteBL(6);
        int vt = (int)r.ReadBL();
        w.WriteBL(vt);
        string text = string.Empty;
        switch (vt)
        {
            case 0: case 1: CopyBl(r, w, src); break;
            case 2: CopyBd(r, w, src); break;
            case 3: case 4: case 5: text = RawStrToUtf16(r, w, src); break;
            case 6: CopyRd(r, w, src); CopyRd(r, w, src); break;
            case 7: CopyBd(r, w, src); CopyBd(r, w, src); CopyBd(r, w, src); break;
            case 8: CopyH(r, w, src); break;
            default: throw new System.IO.EndOfStreamException($"531 value type {vt}");
        }
        w.WriteBL(0);
        ss.Add(string.Empty);
        ss.Add(text);
    }

    private static string RawStrToUtf16(BitReader r, BitWriter w, byte[] src)
    {
        int n = (int)r.ReadBL();
        if (n < 0 || n > 0x100000) throw new System.IO.EndOfStreamException("531 str len");
        var bytes = new byte[n];
        for (int i = 0; i < n; i++) bytes[i] = r.ReadRC();
        string s = DecodeTv(bytes);
        w.WriteBS(unchecked((short)((s.Length + 1) * 2)));
        foreach (char c in s) w.WriteRS((ushort)c);
        w.WriteRS(0);
        return s;
    }
}
