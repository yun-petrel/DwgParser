
using DwgParser.Core.IO;

namespace DwgParser.Core.Objects;

public static partial class DwgObjectStreamWriter
{
    private static bool CarriesThinFieldUp(DwgObjectRecord rec, Target t)
    {
        if (!t.R2007Plus) return false;
        if (rec.SourceVersion < DwgVersion.AC1015 || rec.SourceVersion.IsR2007Plus()) return false;
        return ClassTypeCodes.Normalize(rec.ClassName) == "FIELD";
    }

    private static bool ConvertThinFieldUp(BitWriter w, DwgObjectRecord rec, byte[] src,
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

            var ss = new List<string>();
            w.WriteHandleRef(own.Code, own.Value);
            CopyBits(w, src, eedStart, (int)(eedEnd - eedStart));
            w.WriteBL(nreact);
            w.WriteB(xdicMissing);
            if (t.Version >= DwgVersion.AC1027) w.WriteB(false);

            ss.Add(ReadTv(r));
            ss.Add(ReadTv(r));
            CopyBl(r, w, src);
            CopyBl(r, w, src);
            string fmt300 = ReadTv(r);
            for (int i = 0; i < 5; i++) CopyBl(r, w, src);
            ss.Add(ReadTv(r));
            FieldUnionUp(r, w, src, ss, fmt300);
            ss.Add(ReadTv(r));
            long lp = r.BitPosition;
            int vlen = (int)r.ReadBL();
            if (vlen is < 0 or > 0x10000) return false;
            CopyBits(w, src, lp, (int)(r.BitPosition - lp));
            int ncv = (int)r.ReadBL();
            if (ncv is < 0 or > 64) return false;
            w.WriteBL(ncv);
            for (int i = 0; i < ncv; i++)
            {
                ss.Add(ReadTv(r));
                FieldUnionUp(r, w, src, ss, string.Empty);
            }

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
                    $"FIELDUP h={rec.Handle:X} ver={t.Version} rl={rl} nreact={nreact} " +
                    $"xdic={xdicMissing} strings={ss.Count} ssBits={ssBits} hs={hw.BitPosition}");
            return true;
        }
        catch (System.Exception)
        {
            w.TrimLastBits(w.BitPosition - entry);
            nativeHandles = null;
            return false;
        }
    }

    private static void FieldUnionUp(BitReader r, BitWriter w, byte[] src, List<string> ss,
        string fmt300)
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
            default: throw new System.IO.EndOfStreamException($"FIELD value type {vt}");
        }
        w.WriteBL(0);
        ss.Add(fmt300);
        ss.Add(string.IsNullOrEmpty(text) ? text : string.Empty);
    }
}
