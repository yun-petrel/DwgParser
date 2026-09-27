using DwgParser.Core.IO;

namespace DwgParser.Core.Objects;

public static partial class DwgObjectStreamWriter
{
    internal static int UmcByteLen(uint value)
    {
        int n = 1;
        while (value >= 0x80) { value >>= 7; n++; }
        return n;
    }

    private static bool NeedsCmcUpgrade(DwgObjectRecord rec, Target t)
    {
        if (rec.TypeCode < 500) return false;
        if (t.R2000 || rec.SourceVersion.IsR2004Plus()) return false;
        return rec.LogicalType == TypeVisualStyle;
    }

    private static bool IsVisualStyleR2013bBoundary(DwgObjectRecord rec, Target t)
        => rec.LogicalType == TypeVisualStyle
           && rec.SourceVersion.IsR2010Plus() && t.R2010Plus
           && (rec.SourceVersion >= DwgVersion.AC1027) != (t.Version >= DwgVersion.AC1027)
           && rec.HasHandleStream
           && rec.StringStreamStartBit > rec.CarryStartBit
           && rec.HandleStreamStartBit > rec.StringStreamStartBit;

    private const int VisualStyleR2013bBlockBits = 512;

    internal const int VisualStyleStrokesTuBits = 250;

    private static bool TryCarryVisualStyleR2013b(BitWriter body, DwgObjectRecord rec,
        byte[] src, long srcWin, long bodyEndBit, Target t)
    {
        long ss = rec.StringStreamStartBit;
        long hs = rec.HandleStreamStartBit;
        if (t.Version >= DwgVersion.AC1027)
        {
            long streamBits = hs - 17 - ss;
            if (streamBits < 0 || streamBits + VisualStyleStrokesTuBits > 0x7FFF)
                return false;
            CopyBits(body, src, srcWin, (int)(ss - srcWin));
            WriteVisualStyleR2013bBlock(body, ReadStreamFirstTu(src, ss));
            CopyBits(body, src, ss, (int)(hs - 17 - ss));
            WriteStrokesTu(body);
            body.WriteRS((ushort)(streamBits + VisualStyleStrokesTuBits));
            body.WriteB(ReadSrcBit(src, hs - 1));
        }
        else
        {
            long blkStart = ss - VisualStyleR2013bBlockBits;
            if (rec.VisualStyle is { DownCaptured: true } cap
                && cap.DownFieldsEndBit > srcWin && cap.DownFieldsEndBit < ss)
                blkStart = cap.DownFieldsEndBit;
            if (blkStart <= srcWin || blkStart >= ss)
                return false;
            long tuStart = LastTuStart(src, ss, hs - 17, out long lastTuBits);
            CopyBits(body, src, srcWin, (int)(blkStart - srcWin));
            if (tuStart > ss && tuStart < hs - 17)
            {
                CopyBits(body, src, ss, (int)(tuStart - ss));
                body.WriteRS((ushort)((hs - 17 - ss) - lastTuBits));
                body.WriteB(ReadSrcBit(src, hs - 1));
            }
            else
            {
                CopyBits(body, src, ss, (int)(bodyEndBit - ss));
            }
            if (DebugTrace)
                System.Console.Error.WriteLine(
                    $"VS13bDN h={rec.Handle:X} {rec.SourceVersion}→{t.Version} 块=[{blkStart - srcWin}位@{srcWin}] " +
                    $"剥后串区={hs - 17 - ss}→{(tuStart > ss && tuStart < hs - 17 ? (hs - 17 - ss) - lastTuBits : hs - 17 - ss)}位");
        }
        return true;
    }

    internal static long LastTuStart(byte[] src, long ss, long end, out long lastTuBits)
    {
        lastTuBits = 0;
        try
        {
            var br = new BitReader(src, 0);
            br.SetPosition((int)(ss >> 3), (int)(ss & 7));
            while (true)
            {
                long tuStart = br.BitPosition;
                int len = br.ReadBS();
                if (len < 0 || len > 512) return -1;
                for (int i = 0; i < len; i++) br.ReadRS();
                long p = br.BitPosition;
                if (p > end) return -1;
                if (p == end)
                {
                    lastTuBits = end - tuStart;
                    return tuStart;
                }
            }
        }
        catch
        {
            return -1;
        }
    }

    internal static string? ReadStreamFirstTu(byte[] src, long ss)
    {
        try
        {
            var br = new BitReader(src, 0) { UnicodeStrings = true };
            br.SetPosition((int)(ss >> 3), (int)(ss & 7));
            int len = br.ReadBS();
            if (len <= 0 || len > 512) return null;
            var sb = new System.Text.StringBuilder(len);
            for (int i = 0; i < len; i++) sb.Append((char)br.ReadRS());
            return sb.ToString();
        }
        catch
        {
            return null;
        }
    }

    internal static void WriteStrokesTu(BitWriter body)
    {
        body.WriteBS((short)VisualStyleStrokes.Length);
        foreach (var c in VisualStyleStrokes)
            body.WriteRS((ushort)c);
    }

    private const string VisualStyleStrokes = "strokes_ogs.tif";

    internal static void WriteVisualStyleR2013bBlock(BitWriter body, string? description)
    {
        var block = description == "2dWireframe" ? VisualStyleR2013bBlock2dWireframe
                                                 : VisualStyleR2013bBlockDefault;
        foreach (var b in block)
            body.WriteBits(b, 8);
    }

    private static readonly byte[] VisualStyleR2013bBlockDefault = Convert.FromHexString(
        "203406809012024048090120299203202A032030000001840080A6480A0680C7" +
        "F8000610020240532405324053240480A6480C00000060004D20280901501501");

    private static readonly byte[] VisualStyleR2013bBlock2dWireframe = Convert.FromHexString(
        "A03404809012024048090120299203202A032030000001840080A6480A0680C7" +
        "F8000610020240532405324053240480A6480C00000060004D20280901501501");

    private static int ReencodeClassBodyFields(BitWriter body, byte[] src,
        DwgObjectRecord rec, Target t, long srcBit, long bodyEndBit)
    {
        int bitLen = (int)(bodyEndBit - srcBit);
        if (bitLen <= 0) return 0;
        var br = new BitReader(src, 0);
        DwgObjectRecord.SeekTo(br, srcBit);
        long start = br.BitPosition;

        switch (rec.LogicalType)
        {
            case TypeVisualStyle:
                return ReencodeVisualStyle(body, br, src, rec, t, start, bodyEndBit);
            default:
                DwgObjectRecord.SeekTo(br, start);
                CopyBits(body, src, start, bitLen);
                return 0;
        }
    }

    private static int ReencodeVisualStyle(BitWriter body, BitReader brSrc,
        byte[] src, DwgObjectRecord rec, Target t, long start, long bodyEndBit)
    {
        bool trc = System.Environment.GetEnvironmentVariable("DWG509") == "1";
        if (trc)
            System.Console.Error.WriteLine(
                $"V509 h={rec.Handle:X} start(rel)={start - (long)rec.BodyStart * 8} bodyEndBit(rel)={bodyEndBit - (long)rec.BodyStart * 8} " +
                $"startBit(body abs)={body.BitPosition}");
        var cmcSrcW = new System.Collections.Generic.List<int>();
        var cmcDstW = new System.Collections.Generic.List<int>();
        var cmcIdx = new System.Collections.Generic.List<short>();
        void UpgradeCmc()
        {
            long s0 = brSrc.BitPosition, d0 = body.BitPosition;
            short index = brSrc.ReadBS();
            uint method;
            if (index == 0) method = 0xC1;
            else if (index == 256) method = 0xC0;
            else method = 0xC3;
            uint rgb = (method << 24) | AciPaletteRgb(index);
            body.WriteBS(0);
            body.WriteBL(unchecked((int)rgb));
            body.WriteRC(0);
            cmcSrcW.Add((int)(brSrc.BitPosition - s0));
            cmcDstW.Add((int)(body.BitPosition - d0));
            if (trc) cmcIdx.Add(index);
        }
        void CopyFieldBL() { body.WriteBL(brSrc.ReadBL()); }
        void CopyFieldBD() { body.WriteBD(brSrc.ReadBD()); }
        void CopyFieldBS() { body.WriteBS(brSrc.ReadBS()); }
        void CopyFieldRC() { body.WriteRC(brSrc.ReadRC()); }
        void CopyFieldB() { body.WriteB(brSrc.ReadB()); }
        void CopyFieldTV()
        {
            long s0 = brSrc.BitPosition, d0 = body.BitPosition;
            string v = brSrc.ReadTV();
            body.WriteTV(v);
            if (trc) System.Console.Error.WriteLine(
                $"  TV n={v.Length} \"{v}\" srcBits={brSrc.BitPosition - s0} dstBits={body.BitPosition - d0}");
        }

        long startBit = body.BitPosition;
        if (trc) System.Console.Error.WriteLine("  @" + (brSrc.BitPosition - start) + " " + "description"); CopyFieldTV();
        if (trc) System.Console.Error.WriteLine("  @" + (brSrc.BitPosition - start) + " " + "style_type"); CopyFieldBL();
        if (trc) System.Console.Error.WriteLine("  @" + (brSrc.BitPosition - start) + " " + "face_lighting_model"); CopyFieldBL();
        if (trc) System.Console.Error.WriteLine("  @" + (brSrc.BitPosition - start) + " " + "face_lighting_quality"); CopyFieldBL();
        if (trc) System.Console.Error.WriteLine("  @" + (brSrc.BitPosition - start) + " " + "face_color_mode"); CopyFieldBL();
        if (trc) System.Console.Error.WriteLine("  @" + (brSrc.BitPosition - start) + " " + "face_opacity"); CopyFieldBD();
        if (trc) System.Console.Error.WriteLine("  @" + (brSrc.BitPosition - start) + " " + "face_specular"); CopyFieldBD();
        if (trc) System.Console.Error.WriteLine("  @" + (brSrc.BitPosition - start) + " " + "face_mono_color"); UpgradeCmc();
        if (trc) System.Console.Error.WriteLine("  @" + (brSrc.BitPosition - start) + " " + "face_modifier"); CopyFieldBL();
        if (trc) System.Console.Error.WriteLine("  @" + (brSrc.BitPosition - start) + " " + "edge_model"); CopyFieldBL();
        if (trc) System.Console.Error.WriteLine("  @" + (brSrc.BitPosition - start) + " " + "edge_style"); CopyFieldBL();
        if (trc) System.Console.Error.WriteLine("  @" + (brSrc.BitPosition - start) + " " + "edge_intersection_color"); UpgradeCmc();
        if (trc) System.Console.Error.WriteLine("  @" + (brSrc.BitPosition - start) + " " + "edge_obscured_color"); UpgradeCmc();
        if (trc) System.Console.Error.WriteLine("  @" + (brSrc.BitPosition - start) + " " + "edge_obscured_ltype"); CopyFieldBL();
        if (trc) System.Console.Error.WriteLine("  @" + (brSrc.BitPosition - start) + " " + "edge_crease_angle"); CopyFieldBD();
        if (trc) System.Console.Error.WriteLine("  @" + (brSrc.BitPosition - start) + " " + "edge_modifier"); CopyFieldBL();
        if (trc) System.Console.Error.WriteLine("  @" + (brSrc.BitPosition - start) + " " + "edge_color"); UpgradeCmc();
        if (trc) System.Console.Error.WriteLine("  @" + (brSrc.BitPosition - start) + " " + "edge_opacity"); CopyFieldBD();
        if (trc) System.Console.Error.WriteLine("  @" + (brSrc.BitPosition - start) + " " + "edge_width"); CopyFieldBS();
        if (trc) System.Console.Error.WriteLine("  @" + (brSrc.BitPosition - start) + " " + "edge_overhang"); CopyFieldBS();
        if (trc) System.Console.Error.WriteLine("  @" + (brSrc.BitPosition - start) + " " + "edge_jitter"); CopyFieldBL();
        if (trc) System.Console.Error.WriteLine("  @" + (brSrc.BitPosition - start) + " " + "edge_silhouette_color"); UpgradeCmc();
        if (trc) System.Console.Error.WriteLine("  @" + (brSrc.BitPosition - start) + " " + "edge_silhouette_width"); CopyFieldBS();
        if (trc) System.Console.Error.WriteLine("  @" + (brSrc.BitPosition - start) + " " + "edge_halo_gap"); CopyFieldRC();
        if (trc) System.Console.Error.WriteLine("  @" + (brSrc.BitPosition - start) + " " + "edge_isolines"); CopyFieldBS();
        if (trc) System.Console.Error.WriteLine("  @" + (brSrc.BitPosition - start) + " " + "edge_do_hide_precision"); CopyFieldB();
        if (trc) System.Console.Error.WriteLine("  @" + (brSrc.BitPosition - start) + " " + "edge_style_apply"); CopyFieldBS();
        if (trc) System.Console.Error.WriteLine("  @" + (brSrc.BitPosition - start) + " " + "edge_intersection_ltype"); CopyFieldBS();
        if (trc) System.Console.Error.WriteLine("  @" + (brSrc.BitPosition - start) + " " + "display_settings"); CopyFieldBL();
        if (trc) System.Console.Error.WriteLine("  @" + (brSrc.BitPosition - start) + " " + "display_brightness"); CopyFieldBL();
        if (trc) System.Console.Error.WriteLine("  @" + (brSrc.BitPosition - start) + " " + "display_shadow_type"); CopyFieldBL();
        if (trc) System.Console.Error.WriteLine("  @" + (brSrc.BitPosition - start) + " " + "internal_only"); CopyFieldB();
        if (trc)
        {
            int ss = 0, sd = 0;
            foreach (var v in cmcSrcW) ss += v;
            foreach (var v in cmcDstW) sd += v;
            System.Console.Error.WriteLine(
                $"F509 h={rec.Handle:X} srcStart={start} srcEnd={brSrc.BitPosition} srcConsumed={brSrc.BitPosition - start} " +
                $"bodyEndBit={bodyEndBit} srcFieldW={bodyEndBit - start} dstW={body.BitPosition - startBit} " +
                $"cmcSrc=[{string.Join(",", cmcSrcW)}]={ss} cmcDst=[{string.Join(",", cmcDstW)}]={sd} idx=[{string.Join(",", cmcIdx)}]");
        }
        return (int)(body.BitPosition - startBit) - (int)(bodyEndBit - start);
    }

    private static uint AciPaletteRgb(short index)
    {
        if (index < 0 || index >= 256) return 0;
        return Palette[index];
    }
    private static readonly uint[] Palette = {
        0x000000,0xFF0000,0xFFFF00,0x00FF00,0x00FFFF,0x0000FF,0xFF00FF,0xFFFFFF,0x414141,0x808080,
        0xFF0000,0xFFAAAA,0xBD0000,0xBD7E7E,0x810000,0x815555,0x680000,0x684545,0x4F0000,0x4F3333,
        0xFF3F00,0xFFBFAA,0xBD2E00,0xBD8E7E,0x811F00,0x816155,0x681900,0x684E45,0x4F1300,0x4F3B33,
        0xFF7F00,0xFFD4AA,0xBD5E00,0xBD9D7E,0x814000,0x816B55,0x683300,0x685645,0x4F2600,0x4F4233,
        0xFFBF00,0xFFEAAA,0xBD8E00,0xBDAE7E,0x816100,0x817655,0x684E00,0x685E45,0x4F3B00,0x4F4833,
        0xFFFF00,0xFFFFAA,0xBDBD00,0xBDBD7E,0x818100,0x818155,0x686800,0x686845,0x4F4F00,0x4F4F33,
        0xBFFF00,0xEAFFAA,0x8EBD00,0xAEBE7E,0x618100,0x768155,0x4E6800,0x5E6845,0x3B4F00,0x484F33,
        0x7FFF00,0xD4FFAA,0x5EBD00,0x9DBE7E,0x408100,0x6B8155,0x336800,0x566845,0x264F00,0x424F33,
        0x3FFF00,0xBFFFAA,0x2EBD00,0x8EBE7E,0x1F8100,0x618155,0x196800,0x4E6845,0x134F00,0x3B4F33,
        0x00FF00,0xAAFFAA,0x00BD00,0x7EBD7E,0x008100,0x558155,0x006800,0x456845,0x004F00,0x334F33,
        0x00FF3F,0xAAFFBF,0x00BD2E,0x7EBD8E,0x00811F,0x558161,0x006819,0x45684E,0x004F13,0x334F3B,
        0x00FF7F,0xAAFFD4,0x00BD5E,0x7EBD9D,0x008140,0x55816B,0x006833,0x456856,0x004F26,0x334F42,
        0x00FFBF,0xAAFFEA,0x00BD8E,0x7EBDAE,0x008161,0x558176,0x00684E,0x45685E,0x004F3B,0x334F48,
        0x00FFFF,0xAAFFFF,0x00BDBD,0x7EBDBD,0x008181,0x558181,0x006868,0x456868,0x004F4F,0x334F4F,
        0x00BFFF,0xAAEAFF,0x008EBD,0x7EAEBE,0x006181,0x557681,0x004E68,0x455E68,0x003B4F,0x33484F,
        0x007FFF,0xAAD4FF,0x005EBD,0x7E9DBE,0x004081,0x556B81,0x003368,0x455668,0x00264F,0x33424F,
        0x003FFF,0xAABFFF,0x002EBD,0x7E8EBE,0x001F81,0x556181,0x001968,0x454E68,0x00134F,0x333B4F,
        0x0000FF,0xAAAAFF,0x0000BD,0x7E7EBD,0x000081,0x555581,0x000068,0x454568,0x00004F,0x33334F,
        0x3F00FF,0xBFAAFF,0x2E00BD,0x8E7EBD,0x1F0081,0x615581,0x190068,0x4E4568,0x13004F,0x3B334F,
        0x7F00FF,0xD4AAFF,0x5E00BD,0x9D7EBD,0x400081,0x6B5581,0x330068,0x564568,0x26004F,0x42334F,
        0xBF00FF,0xEAAAFE,0x8E00BD,0xAE7EBD,0x610081,0x765581,0x4E0068,0x5E4568,0x3B004F,0x48334F,
        0xFF00FF,0xFFAAFF,0xBD00BD,0xBD7EBD,0x810081,0x815581,0x680068,0x684568,0x4F004F,0x4F334F,
        0xFF00BF,0xFFAAEA,0xBD008E,0xBD7EAE,0x810061,0x815576,0x68004E,0x68455E,0x4F003B,0x4F3348,
        0xFF007F,0xFFAAD4,0xBD005E,0xBD7E9D,0x810040,0x81556B,0x680033,0x684556,0x4F0026,0x4F3342,
        0xFF003F,0xFFAABF,0xBD002E,0xBD7E8E,0x81001F,0x815561,0x680019,0x68454E,0x4F0013,0x4F333B,
        0x333333,0x505050,0x696969,0x828282,0xBEBEBE,0xFFFFFF,0x000000
    };
}
