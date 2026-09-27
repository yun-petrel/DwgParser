
using DwgParser.Core.IO;
using DwgParser.Core.Objects.Records;

namespace DwgParser.Core.Objects;

public static partial class DwgObjectStreamWriter
{
    private static bool CarriesTableContentR2007Down(DwgObjectRecord rec, Target t)
        => rec.LogicalType == TypeTableContentCode
           && rec.SourceVersion >= DwgVersion.AC1021
           && (t.Version is DwgVersion.AC1015 or DwgVersion.AC1018);

    private static bool ConvertTableContentR2007Down(BitWriter w, DwgObjectRecord rec, byte[] src,
        long start, int len, Target t, out BitWriter? nativeHandles)
    {
        nativeHandles = null;
        long entry = w.BitPosition;
        try
        {
            long objEndBit = start + len;
            var r = new BitReader(src, 0) { UnicodeStrings = true };
            r.SetPosition((int)(start >> 3), (int)(start & 7));
            if (!CentralStrings.TryDataBits(rec, rec.BodyStart, r, out uint rl, out long dataEnd))
                return false;
            if (rl < 76 || dataEnd >= objEndBit) return false;

            if (!CentralStrings.TryMeasure(rec, src, rec.BodyStart, rl,
                    out var str, out long areaStart, out long areaEnd)) return false;
            r.StringStream = str;

            var own = r.ReadHandle();
            long eedStart = r.BitPosition;
            if (!DwgObjectReader.TrySkipEed(r)) return false;
            long eedEnd = r.BitPosition;
            int nreact = (int)r.ReadBL();
            if (nreact is < 0 or > 4096) return false;
            bool xdicMissing = r.ReadB();
            if (rec.SourceVersion >= DwgVersion.AC1027) r.ReadB();

            w.WriteHandleRef(own.Code, own.Value);
            CopyBits(w, src, eedStart, (int)(eedEnd - eedStart));
            w.WriteBL(nreact);
            if (t.Version != DwgVersion.AC1015) w.WriteB(xdicMissing);

            if (!TableFieldsDown(r, w, src)) return false;

            int ng = (int)r.ReadBL();
            if (ng is < 0 or > 64) return false;
            w.WriteBL(ng);
            for (int i = 0; i < ng; i++)
                for (int j = 0; j < 4; j++) CopyBl(r, w, src);

            if (r.BitPosition != areaStart) return false;

            long hsEnd = HandleRefsEnd(src, dataEnd, objEndBit);
            var hw = new BitWriter(64);
            CopyBits(hw, src, dataEnd, (int)(hsEnd - dataEnd));
            if (t.Version == DwgVersion.AC1015)
            {
                if (!xdicMissing) return false;
                byte[] hsBytes = hw.ToArray();
                long total = hw.BitPosition;
                var hbr = new BitReader(hsBytes);
                int cc = hbr.ReadRC();
                long ownerBits = 8 + (cc & 0xF) * 8L;
                if (ownerBits >= total) return false;
                var shifted = new BitWriter(64);
                CopyBits(shifted, hsBytes, 0, (int)ownerBits);
                shifted.WriteHandleRef(3, 0);
                CopyBits(shifted, hsBytes, (int)ownerBits, (int)(total - ownerBits));
                hw = shifted;
            }
            nativeHandles = hw;
            rec.WroteCentralStringStream = true;
            if (DebugTrace)
                System.Console.Error.WriteLine(
                    $"TC531-DOWN h={rec.Handle:X} rl={rl} nreact={nreact} xdic={xdicMissing} " +
                    $"ss=[{areaStart},{areaEnd}) walk={r.BitPosition} hs={hw.BitPosition}");
            return true;
        }
        catch (System.Exception)
        {
            w.TrimLastBits(w.BitPosition - entry);
            nativeHandles = null;
            return false;
        }
    }

    private static bool TableFieldsDown(BitReader r, BitWriter w, byte[] src)
    {
        TvcDown(r, w);
        TvcDown(r, w);
        int nc = (int)r.ReadBL();
        if (nc is < 0 or > 64) return false;
        w.WriteBL(nc);
        for (int c = 0; c < nc; c++)
        {
            TvcDown(r, w);
            CopyBl(r, w, src);
            if (!CdListDown(r, w, src)) return false;
            CellStyleDown(r, w, src);
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
            for (int k = 0; k < ncell; k++) CellDown(r, w, src);
            CopyBl(r, w, src);
            if (!CdListDown(r, w, src)) return false;
            CellStyleDown(r, w, src);
            CopyBl(r, w, src);
            CopyBd(r, w, src);
        }
        int nf = (int)r.ReadBL();
        if (nf is < 0 or > 64) return false;
        w.WriteBL(nf);
        CellStyleDown(r, w, src);
        return true;
    }

    private static bool CdListDown(BitReader r, BitWriter w, byte[] src)
    {
        int n = (int)r.ReadBL();
        if (n is < 0 or > 200) return false;
        w.WriteBL(n);
        for (int i = 0; i < n; i++)
        {
            TvcDown(r, w);
            ValDown(r, w, src);
        }
        return true;
    }

    private static void CellDown(BitReader r, BitWriter w, byte[] src)
    {
        CopyBl(r, w, src);
        TvcDown(r, w);
        CopyBl(r, w, src);
        if (!CdListDown(r, w, src)) throw new System.IO.EndOfStreamException("531 cell cd");
        int lk = (int)r.ReadBL();
        w.WriteBL(lk);
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
            if (ct == 1) ValDown(r, w, src);
            int na = (int)r.ReadBL();
            if (na is < 0 or > 16) throw new System.IO.EndOfStreamException("531 natts");
            w.WriteBL(na);
            for (int j = 0; j < na; j++)
            {
                TvcDown(r, w);
                CopyBl(r, w, src);
            }
            short hf = r.ReadBS();
            w.WriteBS(hf);
            if (hf != 0) ContentFormatDown(r, w, src);
        }
        CellStyleDown(r, w, src);
        CopyBl(r, w, src);
        int uf = (int)r.ReadBL();
        w.WriteBL(uf);
        if (uf != 0)
        {
            CopyBl(r, w, src);
            CopyBd(r, w, src);
            CopyBd(r, w, src);
            int g = (int)r.ReadBL();
            w.WriteBL(g);
            if (g != 0)
                for (int i = 0; i < g; i++)
                {
                    CopyBd(r, w, src); CopyBd(r, w, src); CopyBd(r, w, src);
                    CopyBd(r, w, src); CopyBd(r, w, src); CopyBd(r, w, src);
                    CopyBd(r, w, src); CopyBd(r, w, src); CopyBd(r, w, src);
                    CopyBd(r, w, src);
                    CopyBl(r, w, src);
                }
        }
    }

    private static void CellStyleDown(BitReader r, BitWriter w, byte[] src)
    {
        CopyBl(r, w, src);
        short hasData = r.ReadBS();
        w.WriteBS(hasData);
        if (hasData == 0) return;
        CopyBl(r, w, src);
        CopyBl(r, w, src);
        CmcDown(r, w, src);
        CopyBl(r, w, src);
        ContentFormatDown(r, w, src);
        short mf = r.ReadBS();
        w.WriteBS(mf);
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
                CmcDown(r, w, src);
                CopyBl(r, w, src);
                CopyBl(r, w, src);
                CopyBd(r, w, src);
            }
        }
    }

    private static void ContentFormatDown(BitReader r, BitWriter w, byte[] src)
    {
        CopyBl(r, w, src);
        CopyBl(r, w, src);
        CopyBl(r, w, src);
        CopyBl(r, w, src);
        TvcDown(r, w);
        CopyBd(r, w, src);
        CopyBd(r, w, src);
        CopyBl(r, w, src);
        CmcDown(r, w, src);
        CopyBd(r, w, src);
    }

    private static void CmcDown(BitReader r, BitWriter w, byte[] src)
    {
        CopyBs(r, w, src);
        CopyBl(r, w, src);
        long p = r.BitPosition;
        byte fl = r.ReadRC();
        CopyBits(w, src, p, (int)(r.BitPosition - p));
        if (fl != 0) TvcDown(r, w);
    }

    private static void ValDown(BitReader r, BitWriter w, byte[] src)
    {
        r.ReadBL();
        int vt = (int)r.ReadBL();
        w.WriteBL(vt);
        switch (vt)
        {
            case 0: w.WriteBL(0); break;
            case 1: CopyBl(r, w, src); break;
            case 2: CopyBd(r, w, src); break;
            case 4: case 0x200: StrDown(r, w, src); break;
            case 8:
            {
                long p = r.BitPosition;
                int sz = (int)r.ReadBL();
                if (sz is < 0 or > 0x100000) throw new System.IO.EndOfStreamException("531 date len");
                for (int i = 0; i < sz; i++) r.ReadRC();
                CopyBits(w, src, p, (int)(r.BitPosition - p));
                break;
            }
            case 0x10:
            {
                long p = r.BitPosition;
                int f = (int)r.ReadBL();
                if (f > 0) { r.ReadRD(); r.ReadRD(); }
                CopyBits(w, src, p, (int)(r.BitPosition - p));
                break;
            }
            case 0x20:
            {
                long p = r.BitPosition;
                int f = (int)r.ReadBL();
                if (f > 0) { r.ReadRD(); r.ReadRD(); r.ReadRD(); }
                CopyBits(w, src, p, (int)(r.BitPosition - p));
                break;
            }
            case 0x40: break;
            default: throw new System.IO.EndOfStreamException($"531 value type {vt}");
        }
        int unit = (int)r.ReadBL();
        r.ReadTV();
        if (unit != 12) r.ReadTV();
    }

    private static void StrDown(BitReader r, BitWriter w, byte[] src)
    {
        int n = (int)r.ReadBL();
        if (n is < 0 or > 0x200000) throw new System.IO.EndOfStreamException("531 str len");
        var bytes = new byte[n];
        for (int i = 0; i < n; i++) bytes[i] = r.ReadRC();
        string s = n >= 2
            ? System.Text.Encoding.Unicode.GetString(bytes, 0, n).TrimEnd('\0')
            : string.Empty;
        byte[] enc = EncodeCodepageText(s, 39);
        w.WriteBL(enc.Length + 1);
        foreach (byte b in enc) w.WriteRC(b);
        w.WriteRC(0);
    }

    private static void TvcDown(BitReader r, BitWriter w)
    {
        string s = r.ReadTV();
        if (string.IsNullOrEmpty(s)) { w.WriteTV(string.Empty, false); return; }
        byte[] enc = BitWriter.EncodeText(s);
        w.WriteBS(unchecked((short)(enc.Length + 1)));
        foreach (var b in enc) w.WriteRC(b);
        w.WriteRC(0);
    }
}
