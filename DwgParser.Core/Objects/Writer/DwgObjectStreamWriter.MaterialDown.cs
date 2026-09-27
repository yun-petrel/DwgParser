using DwgParser.Core.IO;

namespace DwgParser.Core.Objects;

public static partial class DwgObjectStreamWriter
{
    private static bool CarriesMaterialR2010Down(DwgObjectRecord rec, Target t)
        => (t.Version == DwgVersion.AC1018 || t.Version == DwgVersion.AC1015)
           && rec.SourceVersion.IsR2010Plus()
           && rec.LogicalType == TypeMaterial
           && rec.StringStreamStartBit > 0;

    private static bool CarriesMaterialAc1021Down(DwgObjectRecord rec, Target t)
        => t.Version == DwgVersion.AC1018
           && rec.SourceVersion == DwgVersion.AC1021
           && rec.LogicalType == TypeMaterial
           && !rec.HasHandleStream;

    private static bool CarriesMaterialR2010ToAc1015(DwgObjectRecord rec, Target t)
        => t.Version == DwgVersion.AC1015
           && rec.SourceVersion.IsR2010Plus()
           && rec.LogicalType == TypeMaterial
           && rec.StringStreamStartBit > 0
           && rec.HandleStreamStartBit > 0;

    private static bool CarryMaterialR2010ToAc1015(BitWriter body, DwgObjectRecord rec, byte[] src,
        Target t, out BitWriter? handles)
    {
        handles = null;
        long hs = rec.HandleStreamStartBit;
        if (hs <= 0 || hs >= (long)rec.BodyEnd * 8) return false;
        try
        {
            var refs = new List<(long Start, int Bits, byte Code, ulong Val)>();
            var br = new BitReader(src, 0);
            DwgObjectRecord.SeekTo(br, hs);
            long end = (long)rec.BodyEnd * 8;
            while (br.BitPosition < end && refs.Count < 3)
            {
                long hp = br.BitPosition;
                byte c = br.ReadRC();
                int n = c & 0x0F;
                if (n > 7) return false;
                ulong v = 0;
                for (int i = 0; i < n; i++) v = (v << 8) | (ulong)br.ReadRC();
                refs.Add((hp, (int)(br.BitPosition - hp), (byte)(c >> 4), v));
                if (end - br.BitPosition < 8) break;
            }
            if (refs.Count != 2 || refs[0].Code != 4 || refs[1].Code != 4) return false;
            if (refs[0].Val != refs[1].Val) return false;

            var h = new BitWriter(16);
            CopyBits(h, src, refs[0].Start, refs[0].Bits);
            CopyBits(h, src, refs[1].Start, refs[1].Bits);
            h.WriteRC(0x32);
            h.WriteRC(0x00);
            h.WriteRC(0x00);
            handles = h;

            if (!CarryMaterialCore(body, rec, src, t, rec.CarryStartBit,
                    rec.StringStreamStartBit, hs)) { handles = null; return false; }
            if (DebugTrace)
                System.Console.Error.WriteLine(
                    $"MAT1015 h={rec.Handle:X} {rec.SourceVersion}→AC1015 refs=2→2 hs={h.BitPosition}位");
            return true;
        }
        catch
        {
            handles = null;
            return false;
        }
    }


    private static bool CarriesMaterialAc1021ToAc1015(DwgObjectRecord rec, Target t)
        => t.Version == DwgVersion.AC1015
           && rec.SourceVersion == DwgVersion.AC1021
           && rec.LogicalType == TypeMaterial
           && rec.StringStreamStartBit == 0
           && rec.HandleStreamStartBit == 0;

    private static bool MatXdicPassthroughOn()
    {
        var v = System.Environment.GetEnvironmentVariable("MATXD");
        return v is null || v.Length == 0 || v[0] != '0';
    }

    private static bool CarryMaterialAc1021ToAc1015(BitWriter body, DwgObjectRecord rec, byte[] src,
        Target t, out BitWriter? handles)
    {
        handles = null;
        if (!DictionaryCarry.TryAc1021Spans(src, rec, out long dataEnd, out long ss, out bool hasStrings)
            || !hasStrings) return false;
        long start = rec.CarryStartBit + 32;
        if (start <= 0 || ss <= start || dataEnd <= ss) return false;

        try
        {
            var refs = new List<(long Start, int Bits, byte Code, ulong Val)>();
            var br = new BitReader(src, 0);
            DwgObjectRecord.SeekTo(br, dataEnd);
            long end = (long)rec.BodyEnd * 8;
            while (br.BitPosition < end && refs.Count < 3)
            {
                long hp = br.BitPosition;
                byte c = br.ReadRC();
                int n = c & 0x0F;
                if (n > 7) return false;
                ulong v = 0;
                for (int i = 0; i < n; i++) v = (v << 8) | (ulong)br.ReadRC();
                refs.Add((hp, (int)(br.BitPosition - hp), (byte)(c >> 4), v));
                if (end - br.BitPosition < 8) break;
            }
            if (refs[0].Code != 4 || refs[1].Code != 4) return false;
            if (refs[0].Val != refs[1].Val) return false;

            var h = new BitWriter(16);
            if (refs.Count == 3 && refs[2].Code == 3 && MatXdicPassthroughOn())
            {
                long refsEnd = refs[2].Start + refs[2].Bits;
                CopyBits(h, src, refs[0].Start, (int)(refsEnd - refs[0].Start));
                if (DebugTrace)
                    System.Console.Error.WriteLine(
                        $"MATXD h={rec.Handle:X} 源已带 xdic (3,0x{refs[2].Val:X}) ⇒ 三枚逐字搬 不补空槽");
            }
            else
            {
                if (refs.Count != 2) return false;
                CopyBits(h, src, refs[0].Start, refs[0].Bits);
                CopyBits(h, src, refs[1].Start, refs[1].Bits);
                h.WriteRC(0x32);
                h.WriteRC(0x00);
                h.WriteRC(0x00);
            }
            handles = h;

            if (!CarryMaterialCore(body, rec, src, t, start, ss, dataEnd)) { handles = null; return false; }
            if (DebugTrace)
                System.Console.Error.WriteLine(
                    $"MAT1015A21 h={rec.Handle:X} AC1021→AC1015 start={start} ss={ss} dataEnd={dataEnd} " +
                    $"hs={h.BitPosition}位 out={body.BitPosition}");
            return true;
        }
        catch
        {
            handles = null;
            return false;
        }
    }

    private static bool CarryMaterialR2010Down(BitWriter body, DwgObjectRecord rec, byte[] src,
        Target t)
        => CarryMaterialCore(body, rec, src, t, rec.CarryStartBit, rec.StringStreamStartBit,
                             rec.HandleStreamStartBit);

    private static bool CarryMaterialAc1021Down(BitWriter body, DwgObjectRecord rec, byte[] src,
        Target t, out BitWriter? handles)
    {
        handles = null;
        if (!DictionaryCarry.TryAc1021Spans(src, rec, out long dataEnd, out long ss,
                out bool hasStrings) || !hasStrings) return false;
        if (!DictionaryCarry.TryAc1021HandlesTail(rec, src, dataEnd, 2, out handles)) return false;
        return CarryMaterialCore(body, rec, src, t, rec.CarryStartBit + 32, ss, dataEnd);
    }

    private static bool CarryMaterialCore(BitWriter body, DwgObjectRecord rec, byte[] src, Target t,
        long start, long ssStart, long footerEnd)
    {
        if (start <= 0 || ssStart <= start || footerEnd <= ssStart) return false;
        if (MatVar() == 0) return false;
        try
        {
            var br = new BitReader(src, 0);
            DwgObjectRecord.SeekTo(br, start);
            br.ReadHandle();
            long eedStart = br.BitPosition;
            if (!SkipEedWriter(br)) return false;
            int eedLen = (int)(br.BitPosition - eedStart);
            long numReactors = br.ReadBL();
            if (numReactors is < 0 or > 8) return false;
            bool xdic = br.ReadB();
            if (rec.SourceVersion >= DwgVersion.AC1027) br.ReadB();
            long mapsStart = br.BitPosition;

            var insertAt = new List<long>();
            MatColor(br); MatColor(br); MatMap(br, insertAt);
            MatColor(br); MatMap(br, insertAt); br.ReadBD();
            MatMap(br, insertAt); br.ReadBD(); MatMap(br, insertAt);
            MatMap(br, insertAt); br.ReadBD(); MatMap(br, insertAt);
            long mapsEnd = br.BitPosition;
            br.ReadBits(20);
            if (br.BitPosition != ssStart) return false;

            string name, desc;
            var extra = new List<string>();
            {
                var sr = new BitReader(src) { UnicodeStrings = true };
                DwgObjectRecord.SeekTo(sr, ssStart);
                long ssEnd = footerEnd - 17;
                name = sr.ReadTV();
                desc = sr.ReadTV();
                while (sr.BitPosition < ssEnd)
                {
                    extra.Add(sr.ReadTV());
                    if (extra.Count > 24) return false;
                }
                if (sr.BitPosition != ssEnd) return false;
            }
            if (name.Length == 0 || name.Length > 255 || desc.Length > 255) return false;
            foreach (var x in extra)
                if (x.Length != 0) return false;
            if (extra.Count != insertAt.Count) return false;

            CopyBits(body, src, start, (int)(eedStart - start));
            if (!TryWriteEedPreR2007Range(body, src, eedStart, eedLen))
                CopyBits(body, src, eedStart, eedLen);
            body.WriteBL((int)numReactors);
            if (t.Version != DwgVersion.AC1015) body.WriteB(xdic);

            body.WriteTV(name);
            body.WriteTV(desc);
            long p = mapsStart;
            foreach (var at in insertAt)
            {
                CopyBits(body, src, p, (int)(at - p));
                body.WriteTV("");
                p = at;
            }
            CopyBits(body, src, p, (int)(mapsEnd - p));

            if (DebugTrace)
                System.Console.Error.WriteLine(
                    $"MATDOWN h={rec.Handle:X} {rec.SourceVersion}→{t.Version} name='{name}' desc='{desc}' " +
                    $"nr={numReactors} xdic={xdic} start={start} eed={eedStart}+{eedLen} " +
                    $"maps=[{mapsStart},{mapsEnd}) src1={insertAt.Count} 余串={extra.Count} ss={ssStart} " +
                    $"hs={footerEnd} out={body.BitPosition} var={MatVar()}");
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static int MatVar()
    {
        var s = System.Environment.GetEnvironmentVariable("MATVAR");
        return int.TryParse(s, out var v) ? v : 1;
    }

    private static void MatColor(BitReader r)
    {
        byte flag = r.ReadRC();
        r.ReadBD();
        if (flag == 1) r.ReadBL();
    }

    private static void MatMap(BitReader r, List<long> insertAt)
    {
        r.ReadBD();
        r.ReadRC(); r.ReadRC(); r.ReadRC();
        for (int i = 0; i < 16; i++) r.ReadBD();
        if (r.ReadRC() == 1) insertAt.Add(r.BitPosition);
    }
}
