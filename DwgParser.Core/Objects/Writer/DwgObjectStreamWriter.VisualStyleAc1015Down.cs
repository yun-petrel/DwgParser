using DwgParser.Core.IO;
using DwgParser.Core.Objects.Records;

namespace DwgParser.Core.Objects;

public static partial class DwgObjectStreamWriter
{
    private static bool CarriesVisualStyleR2010ToAc1015(DwgObjectRecord rec, Target t)
        => t.Version == DwgVersion.AC1015
           && rec.SourceVersion.IsR2010Plus()
           && rec.LogicalType == TypeVisualStyle
           && rec.VisualStyle is { DownCaptured: true }
           && rec.HasHandleStream;

    private static bool CarryVisualStyleR2010ToAc1015(BitWriter body, DwgObjectRecord rec,
        byte[] src, out BitWriter? handles)
    {
        handles = null;
        var cap = rec.VisualStyle;
        if (cap == null) return false;
        if (cap.DownFieldSpans.Length != 18 || cap.DownScalars.Length != 20) return false;
        long start = rec.CarryStartBit;
        long body0 = cap.DownBodyStartBit;
        long ss = cap.DownStreamStartBit;
        long hsAnchor = rec.HandleStreamStartBit;
        if (start <= 0 || body0 <= start || ss <= body0 || hsAnchor <= ss) return false;
        if (!CmcMethodsInSpec(cap)) return false;
        string desc = ReadStreamFirstTu(src, ss) ?? "";
        if (desc.Length == 0 || desc.Length > 255) return false;
        try
        {
            var br = new BitReader(src, 0);
            DwgObjectRecord.SeekTo(br, start);
            br.ReadHandle();
            long eedStart = br.BitPosition;
            if (!SkipEedWriter(br)) return false;
            int eedLen = (int)(br.BitPosition - eedStart);
            int numReactors = br.ReadBL();
            if (numReactors is < 0 or > 8) return false;
            bool xdic = br.ReadB();
            if (rec.SourceVersion >= DwgVersion.AC1027) br.ReadB();
            if (br.BitPosition != body0) return false;

            CopyBits(body, src, start, (int)(eedStart - start));
            if (!TryWriteEedPreR2007Range(body, src, eedStart, eedLen))
                CopyBits(body, src, eedStart, eedLen);
            body.WriteBL(numReactors);
            body.WriteTV(desc);
            WriteVisualStylePreFields(body, src, cap, withBd2007: false, cmcShortForm: true);

            long hsEnd = LastMeaningfulHandleEnd(src, hsAnchor, (long)rec.BodyEnd * 8);
            if (hsEnd <= hsAnchor) return false;
            var hs = new BitWriter(16);
            CopyBits(hs, src, hsAnchor, (int)(hsEnd - hsAnchor));
            long insertAt = DictionaryCarry.ScanOwnerReactorPrefix(hs.ToArray());
            if (insertAt < 0) insertAt = AllCode4RunEnd(hs);
            if (insertAt <= 0 || insertAt > hs.BitPosition) return false;
            var probe = new BitReader(hs.ToArray(), 0);
            DwgObjectRecord.SeekTo(probe, insertAt);
            if (insertAt == hs.BitPosition || (probe.ReadRC() >> 4) != 3)
            {
                long total = hs.BitPosition;
                var shifted = new BitWriter(16);
                CopyBits(shifted, hs.ToArray(), 0, (int)insertAt);
                shifted.WriteHandleRef(3, 0);
                CopyBits(shifted, hs.ToArray(), (int)insertAt, (int)(total - insertAt));
                hs = shifted;
            }
            handles = hs;
            if (DebugTrace)
                System.Console.Error.WriteLine(
                    $"VSDN1015 h={rec.Handle:X} {rec.SourceVersion}→AC1015 desc='{desc}' nr={numReactors} " +
                    $"xdicsrc={xdic} 剥串区{hsAnchor - ss}位 body={body.BitPosition} hs={hs.BitPosition}@{insertAt}");
            return true;
        }
        catch { return false; }
    }

    private static bool CarriesVisualStyleAc1018ToAc1015(DwgObjectRecord rec, Target t)
        => t.Version == DwgVersion.AC1015
           && rec.SourceVersion == DwgVersion.AC1018
           && rec.LogicalType == TypeVisualStyle
           && rec.VisualStyle is { Captured: true }
           && rec.HasHandleStream;

    private static bool CarryVisualStyleAc1018ToAc1015(BitWriter body, DwgObjectRecord rec,
        byte[] src, out BitWriter? handles)
    {
        handles = null;
        var cap = rec.VisualStyle;
        if (cap is null) return false;
        var sp = cap.FieldSpans;
        if (sp.Length != 60) return false;
        long start = rec.CarryStartBit + 32;
        long descStart = cap.BodyStartBit, fieldsStart = cap.StreamStartBit, hs = rec.HandleStreamStartBit;
        if (start <= 0 || descStart <= start || fieldsStart <= descStart) return false;
        if (fieldsStart > sp[0] || sp[59] > hs) return false;
        try
        {
            var br = new BitReader(src, 0);
            DwgObjectRecord.SeekTo(br, start);
            br.ReadHandle();
            long eedStart = br.BitPosition;
            if (!SkipEedWriter(br)) return false;
            int eedLen = (int)(br.BitPosition - eedStart);
            int nr = br.ReadBL();
            if (nr is < 0 or > 8) return false;
            br.ReadB();
            if (br.BitPosition != descStart) return false;

            CopyBits(body, src, start, (int)(eedStart - start));
            if (!TryWriteEedPreR2007Range(body, src, eedStart, eedLen))
                CopyBits(body, src, eedStart, eedLen);
            body.WriteBL(nr);
            CopyBits(body, src, descStart, (int)(fieldsStart - descStart));
            for (int f = 0; f < 30; f++)
            {
                long s0 = sp[f * 2], s1 = sp[f * 2 + 1];
                if (f is 6 or 10 or 11 or 15 or 20)
                {
                    var cb = new BitReader(src, 0);
                    DwgObjectRecord.SeekTo(cb, s0);
                    var tri = DwgObjectReader.ReadCmcTriple(cb);
                    if (cb.BitPosition != s1) return false;
                    if (tri.Idx is < 0 or > 256 || (tri.Rgb >> 24) is < 0xC0 or > 0xC8) return false;
                    body.WriteBS(FoldCmcToPreR2004Index(tri.Rgb));
                }
                else CopyBits(body, src, s0, (int)(s1 - s0));
            }
            CopyBits(body, src, sp[59], (int)(hs - sp[59]));
            long hsEnd = LastMeaningfulHandleEnd(src, hs, (long)rec.BodyEnd * 8);
            if (hsEnd <= hs) return false;
            var hw = new BitWriter(16);
            CopyBits(hw, src, hs, (int)(hsEnd - hs));
            if (!HandleStreamHasXdicSlot(hw.ToArray(), hw.BitPosition)) hw.WriteRC(0x30);
            handles = hw;
            if (DebugTrace)
                System.Console.Error.WriteLine(
                    $"VSDN1015A h={rec.Handle:X} AC1018→AC1015 body={body.BitPosition} hs={hw.BitPosition}位");
            return true;
        }
        catch { return false; }
    }

    private static long AllCode4RunEnd(BitWriter hs)
    {
        try
        {
            var data = hs.ToArray();
            var br = new BitReader(data, 0);
            long p = 0, end = (long)data.Length * 8;
            int seen = 0;
            while (p < end)
            {
                DwgObjectRecord.SeekTo(br, p);
                int cc = br.ReadRC();
                if (cc == 0) return seen > 0 ? p : -1;
                if ((cc >> 4) != 4) return -1;
                long nb = 8 + (long)(cc & 0xF) * 8;
                if (p + nb > end) return -1;
                p += nb; seen++;
            }
            return seen > 0 ? p : -1;
        }
        catch { return -1; }
    }

    private static bool CmcMethodsInSpec(VisualStyleCapture c)
    {
        foreach (var rgb in new[] { c.FaceMonoColor.Rgb, c.EdgeIntersectionColor.Rgb,
                c.EdgeObscuredColor.Rgb, c.EdgeColor.Rgb, c.EdgeSilhouetteColor.Rgb })
            switch (rgb >> 24)
            {
                case 0xC0: case 0xC1: case 0xC2: case 0xC3: case 0xC8: break;
                default: return false;
            }
        return true;
    }

    internal static short FoldCmcToPreR2004Index(uint rgb)
    {
        switch (rgb >> 24)
        {
            case 0xC0: return 256;
            case 0xC1: return 0;
            case 0xC8: return 257;
            case 0xC3: return (short)((rgb & 0xFF) <= 256 ? rgb & 0xFF : 256);
            default: return PreR2004NearestAci(rgb & 0xFFFFFF);
        }
    }

    internal static short PreR2004NearestAci(uint rgb)
    {
        int r = (int)((rgb >> 16) & 0xFF), g = (int)((rgb >> 8) & 0xFF), b = (int)(rgb & 0xFF);
        int best = -1, bestD = int.MaxValue;
        for (int i = 0; i < Palette.Length; i++)
        {
            uint p = i switch { 8 => 0x808080, 9 => 0xC0C0C0, _ => Palette[i] };
            int dr = (int)((p >> 16) & 0xFF) - r, dg = (int)((p >> 8) & 0xFF) - g, db = (int)(p & 0xFF) - b;
            int d = dr * dr + dg * dg + db * db;
            if (d < bestD) { bestD = d; best = i; }
        }
        return (short)(best < 0 ? 256 : best);
    }

    private static bool CarriesVisualStyleAc1021ToAc1015(DwgObjectRecord rec, Target t)
        => t.Version == DwgVersion.AC1015
           && rec.SourceVersion == DwgVersion.AC1021
           && rec.LogicalType == TypeVisualStyle
           && rec.VisualStyle is { Captured: true }
           && rec.HasHandleStream;

    private static bool CarryVisualStyleAc1021ToAc1015(BitWriter body, DwgObjectRecord rec,
        byte[] src, out BitWriter? handles)
    {
        handles = null;
        var cap = rec.VisualStyle;
        if (cap == null) return false;
        var sp = cap.FieldSpans;
        long start = rec.CarryStartBit + 32;
        long body0 = cap.BodyStartBit;
        long ss = cap.StreamStartBit;
        long hsAnchor = rec.HandleStreamStartBit;
        if (start <= 0 || body0 <= start || ss <= body0 || hsAnchor <= ss) return false;
        if (sp.Length != 60 || sp[0] != body0) return false;
        for (int i = 0; i + 3 < sp.Length; i += 2)
            if (sp[i + 1] != sp[i + 2]) return false;
        long fieldsEnd = sp[sp.Length - 1];
        if (fieldsEnd + 3 != ss) return false;
        if (!CmcMethodsInSpec(cap)) return false;
        try
        {
            string desc;
            {
                var sr = new BitReader(src, 0) { UnicodeStrings = true };
                DwgObjectRecord.SeekTo(sr, ss);
                desc = sr.ReadTV();
                if (sr.BitPosition + 17 != hsAnchor) return false;
            }
            if (desc.Length == 0 || desc.Length > 255) return false;

            var br = new BitReader(src, 0);
            DwgObjectRecord.SeekTo(br, start);
            br.ReadHandle();
            long eedStart = br.BitPosition;
            if (!SkipEedWriter(br)) return false;
            int eedLen = (int)(br.BitPosition - eedStart);
            int nr = br.ReadBL();
            if (nr is < 0 or > 8) return false;
            br.ReadB();
            if (br.BitPosition != body0) return false;

            CopyBits(body, src, start, (int)(eedStart - start));
            if (!TryWriteEedPreR2007Range(body, src, eedStart, eedLen))
                CopyBits(body, src, eedStart, eedLen);
            body.WriteBL(nr);
            body.WriteTV(desc);
            for (int f = 0; f < 30; f++)
            {
                long s0 = sp[f * 2], s1 = sp[f * 2 + 1];
                if (f is 6 or 10 or 11 or 15 or 20)
                {
                    var cb = new BitReader(src, 0);
                    DwgObjectRecord.SeekTo(cb, s0);
                    var tri = DwgObjectReader.ReadCmcTriple(cb);
                    if (cb.BitPosition != s1) return false;
                    if (tri.Idx is < 0 or > 256 || (tri.Rgb >> 24) is < 0xC0 or > 0xC8) return false;
                    body.WriteBS(FoldCmcToPreR2004Index(tri.Rgb));
                }
                else CopyBits(body, src, s0, (int)(s1 - s0));
            }
            body.WriteB(ReadSrcBit(src, fieldsEnd + 2));

            long hsEnd = LastMeaningfulHandleEnd(src, hsAnchor, (long)rec.BodyEnd * 8);
            if (hsEnd <= hsAnchor) return false;
            var hw = new BitWriter(16);
            CopyBits(hw, src, hsAnchor, (int)(hsEnd - hsAnchor));
            if (!HandleStreamHasXdicSlot(hw.ToArray(), hw.BitPosition)) hw.WriteRC(0x30);
            handles = hw;
            if (DebugTrace)
                System.Console.Error.WriteLine(
                    $"VSDN2115 h={rec.Handle:X} AC1021→AC1015 desc='{desc}' nr={nr} body={body.BitPosition} hs={hw.BitPosition}位");
            return true;
        }
        catch { return false; }
    }
}
