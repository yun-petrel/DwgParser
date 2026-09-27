
using DwgParser.Core.IO;
using DwgParser.Core.Objects.Records;

namespace DwgParser.Core.Objects;

public static partial class DwgObjectStreamWriter
{
    private static bool CarriesMediumFieldDown(DwgObjectRecord rec, Target t)
    {
        if (t.Version != DwgVersion.AC1018) return false;
        if (!rec.SourceVersion.IsR2007Plus()) return false;
        return ClassTypeCodes.Normalize(rec.ClassName) == "FIELD";
    }

    private static bool ConvertMediumFieldDown(BitWriter w, DwgObjectRecord rec, byte[] src,
        long start, int len, Target t, out BitWriter? nativeHandles)
    {
        nativeHandles = null;
        if (len <= 42 || start < 10) return false;
        long entry = w.BitPosition;
        bool dbg = System.Environment.GetEnvironmentVariable("DWGFLD") == "1";
        var tr = new System.Text.StringBuilder();
        try
        {
            long objEndBit = start + len;
            var r = new BitReader(src, 0) { UnicodeStrings = true };
            r.SetPosition((int)(start >> 3), (int)(start & 7));
            if (!CentralStrings.TryDataBits(rec, rec.BodyStart, r, out uint rl, out long dataEnd))
                return false;
            long objStartBit = (long)rec.BodyStart * 8;
            if (rl < 76 || dataEnd >= objEndBit) return false;

            if (!CentralStrings.TryMeasure(rec, src, rec.BodyStart, rl,
                    out var str, out long areaStart, out long areaEnd)) return false;
            r.StringStream = str;
            tr.Append($"FLDD h={rec.Handle:X} ver={t.Version} srcV={rec.SourceVersion} rl={rl} " +
                      $"data={dataEnd - objStartBit} ss=[{areaStart - objStartBit}," +
                      $"{areaEnd - objStartBit}) end={objEndBit - objStartBit}");

            int ssCount = -1;
            if (dbg)
            {
                var probe = new BitReader(src, 0) { UnicodeStrings = true };
                probe.SetPosition((int)(areaStart >> 3), (int)(areaStart & 7));
                if (CentralStrings.ReadAll(probe, areaEnd, 64, out var got)) ssCount = got.Count;
            }

            long headStart = r.BitPosition;
            var own = r.ReadHandle();
            long eedStart = r.BitPosition;
            if (!DwgObjectReader.TrySkipEed(r)) return false;
            long eedEnd = r.BitPosition;
            int nreact = (int)r.ReadBL();
            if (nreact is < 0 or > 4096) return false;
            bool xdicMissing = r.ReadB();
            long headEnd = r.BitPosition;
            if (rec.SourceVersion >= DwgVersion.AC1027) r.ReadB();
            if (eedEnd - eedStart != 2) return false;
            tr.Append($" head={headEnd - headStart} nreact={nreact} xdic={xdicMissing}");

            string id = r.ReadTV();
            string code = r.ReadTV();
            tr.Append($" id='{id}' code='{code}' @{r.BitPosition - objStartBit}");
            long childsStart = r.BitPosition;
            int nchilds = (int)r.ReadBL();
            int nobjects = (int)r.ReadBL();
            long childsEnd = r.BitPosition;
            if (nchilds is < 0 or > 4096 || nobjects is < 0 or > 4096) return false;
            tr.Append($" childs={nchilds} objects={nobjects} bits={childsEnd - childsStart}");

            long scalarsStart = r.BitPosition;
            for (int i = 0; i < 5; i++) r.ReadBL();
            long scalarsEnd = r.BitPosition;
            tr.Append($" 5BL={scalarsEnd - scalarsStart}");

            string errMsg = r.ReadTV();
            tr.Append($" emsg='{errMsg}' @{r.BitPosition - objStartBit}");

            int flags = (int)r.ReadBL();
            Seg val = ReadUnionSeg(r, src, "FIELD value", flags);
            int units = (int)r.ReadBL();
            string fmt300 = r.ReadTV();
            string val302 = units != 12 ? r.ReadTV() : string.Empty;
            tr.Append($" flags={flags} val={val} units={units} f300='{fmt300}' v302='{val302}'");

            string val301 = r.ReadTV();
            long len301Start = r.BitPosition;
            int vlen = (int)r.ReadBL();
            if (vlen is < 0 or > 0x10000) return false;
            long len301End = r.BitPosition;
            tr.Append($" 301='{val301}' len={vlen} bits={len301End - len301Start}");

            long ncvStart = r.BitPosition;
            int ncv = (int)r.ReadBL();
            if (ncv is < 0 or > 64) return false;
            long ncvEnd = r.BitPosition;
            var kids = new List<(string key, Seg val, int kf, int ku, string k300, string k302)>();
            for (int i = 0; i < ncv; i++)
            {
                string key = r.ReadTV();
                int kf = (int)r.ReadBL();
                Seg kv = ReadUnionSeg(r, src, "FIELD child", kf);
                int ku = (int)r.ReadBL();
                string k300 = r.ReadTV();
                string k302 = ku != 12 ? r.ReadTV() : string.Empty;
                kids.Add((key, kv, kf, ku, k300, k302));
                tr.Append($" kid{i}='{key}' kf={kf} kv={kv} ku={ku} k300='{k300}' k302='{k302}'");
            }
            tr.Append($" ncv={ncv} ncvBits={ncvEnd - ncvStart}");

            if (r.BitPosition != areaStart)
            {
                tr.Append($" NOT-CLOSED walk={r.BitPosition - objStartBit} " +
                          $"ss={areaStart - objStartBit} Δ={r.BitPosition - areaStart}");
                if (dbg) System.Console.Error.WriteLine(tr.ToString());
                return false;
            }

            CopyBits(w, src, headStart, (int)(headEnd - headStart));
            WriteThinTv(w, id);
            WriteThinTv(w, code);
            CopyBits(w, src, childsStart, (int)(childsEnd - childsStart));
            WriteThinTv(w, fmt300);
            CopyBits(w, src, scalarsStart, (int)(scalarsEnd - scalarsStart));
            WriteThinTv(w, errMsg);
            val.Emit(w, src);
            WriteThinTv(w, val301);
            CopyBits(w, src, len301Start, (int)(len301End - len301Start));
            CopyBits(w, src, ncvStart, (int)(ncvEnd - ncvStart));
            foreach (var k in kids)
            {
                WriteThinTv(w, k.key);
                k.val.Emit(w, src);
            }

            int expect = 6 + 3 * ncv - (units == 12 ? 1 : 0);
            long hsEnd = HandleRefsEnd(src, dataEnd, objEndBit);
            var hw = new BitWriter(64);
            CopyBits(hw, src, dataEnd, (int)(hsEnd - dataEnd));
            nativeHandles = hw;
            rec.WroteCentralStringStream = true;

            if (dbg)
                System.Console.Error.WriteLine(tr.Append(
                    $" OK thin={w.BitPosition - entry} ssCount={ssCount} expect={expect}" +
                    $" hs={hw.BitPosition} hsRaw={objEndBit - dataEnd}").ToString());
            return true;
        }
        catch (System.Exception ex)
        {
            if (dbg) System.Console.Error.WriteLine(tr.Append($" EX {ex.Message}").ToString());
            w.TrimLastBits(w.BitPosition - entry);
            nativeHandles = null;
            return false;
        }
    }

    private static Seg ReadUnionSeg(BitReader r, byte[] src, string tag, int flags)
    {
        long pType = r.BitPosition;
        int vt = (int)r.ReadBL();
        if ((flags & 1) != 0)
        {
            if (vt is not (0 or 1))
                throw new System.IO.EndOfStreamException($"{tag} 公式型 type={vt} 补形未定谱");
            var bw = new BitWriter(8);
            bw.WriteBL(vt);
            bw.WriteBL(0);
            return Seg.FromSyn(bw);
        }
        switch (vt)
        {
            case 0: case 1: case 2:
            case 6:
            case 7:
            case 8:
            case 9: case 10: case 11: case 12: case 13:
                ConsumePayload(r, vt, tag);
                return Seg.FromSrc(pType, r.BitPosition);
            case 3: case 4: case 5:
                return Seg.FromSyn(StrMidToThin(r, tag, vt));
            default: throw new System.IO.EndOfStreamException($"{tag} value type {vt}");
        }
    }

    private static void ConsumePayload(BitReader r, int vt, string tag)
    {
        switch (vt)
        {
            case 0: case 1: r.ReadBL(); break;
            case 2: r.ReadBD(); break;
            case 6: r.ReadRD(); r.ReadRD(); break;
            case 7: r.ReadBD(); r.ReadBD(); r.ReadBD(); break;
            case 8: r.ReadHandle(); break;
            case 9: case 10: case 11: case 12: case 13:
                throw new System.IO.EndOfStreamException($"{tag} value type {vt} 未定谱");
            default: throw new System.IO.EndOfStreamException($"{tag} value type {vt}");
        }
    }

    private static BitWriter StrMidToThin(BitReader r, string tag, int vt)
    {
        int n = r.ReadBS();
        if (n is < 0 or > 0x200000) throw new System.IO.EndOfStreamException($"{tag} str len {n}");
        var bytes = new byte[n];
        for (int i = 0; i < n; i++) bytes[i] = r.ReadRC();
        string s = n >= 2
            ? System.Text.Encoding.Unicode.GetString(bytes, 0, n & ~1).TrimEnd('\0')
            : string.Empty;
        byte[] enc = EncodeThinText(s);
        if (enc.Length >= 256)
            throw new System.IO.EndOfStreamException($"{tag} str 码页字节 {enc.Length} ≥256 未定谱");
        var bw = new BitWriter(enc.Length + 16);
        bw.WriteBL(vt);
        bw.WriteBL(enc.Length + 1);
        foreach (byte b in enc) bw.WriteRC(b);
        bw.WriteRC(0);
        return bw;
    }

    private static void WriteThinTv(BitWriter w, string? value)
    {
        if (string.IsNullOrEmpty(value)) { w.WriteBS(0); return; }
        byte[] bytes = EncodeThinText(value);
        if (bytes.Length >= 256)
            throw new System.IO.EndOfStreamException($"FIELD 薄形串 {bytes.Length} 字节 ≥256 未定谱");
        w.WriteBS(unchecked((short)(bytes.Length + 1)));
        foreach (var b in bytes) w.WriteRC(b);
        w.WriteRC(0);
    }

    private static byte[] EncodeThinText(string value)
    {
        bool ascii = true;
        foreach (var c in value)
            if (c > 0x7f) { ascii = false; break; }
        if (ascii) return System.Text.Encoding.UTF8.GetBytes(value);
        var gbk = System.Text.Encoding.GetEncoding("GBK");
        byte[] bytes = gbk.GetBytes(value);
        if (gbk.GetString(bytes) != value)
            throw new System.NotSupportedException($"FIELD 薄形码页往返失败：'{value}'");
        return bytes;
    }

    private readonly struct Seg
    {
        private Seg(long pos, int bits, BitWriter? syn)
        {
            Pos = pos; Bits = bits; Syn = syn;
        }
        public static Seg FromSrc(long from, long to) => new(from, (int)(to - from), null);
        public static Seg FromSyn(BitWriter syn) => new(0, (int)syn.BitPosition, syn);
        private long Pos { get; }
        private int Bits { get; }
        private BitWriter? Syn { get; }
        public void Emit(BitWriter w, byte[] src)
        {
            if (Syn is not null) CopyBits(w, Syn.ToArray(), 0, Bits);
            else CopyBits(w, src, Pos, Bits);
        }
        public override string ToString() =>
            Syn is not null ? $"syn[{Bits}]" : $"src[{Pos}+{Bits}]";
    }
}
