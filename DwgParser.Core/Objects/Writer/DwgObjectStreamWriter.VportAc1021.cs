using DwgParser.Core.Format;
using DwgParser.Core.IO;

namespace DwgParser.Core.Objects;

public static partial class DwgObjectStreamWriter
{
    private const short TypeVport = 0x41;

    private static void StampVportDefaultVisualStyle(
        DwgObjectReader.WalkResult walk, byte[] src, DwgVersion target)
    {
        if (target < DwgVersion.AC1021 || src.Length == 0) return;
        bool need = false;
        foreach (var r in walk.Records)
        {
            if (r.SourceVersion is not (DwgVersion.AC1015 or DwgVersion.AC1018)) continue;
            if (r.LogicalType == TypeVport && r.Vport is { Captured: true }) { need = true; break; }
            if (r.LogicalType == TypeViewport && r.VportR2007aTailSlot) { need = true; break; }
        }
        if (!need) return;
        ulong h = FindDefaultVisualStyleHandle(walk, src);
        if (h == 0) return;
        foreach (var r in walk.Records)
        {
            if (r.SourceVersion is not (DwgVersion.AC1015 or DwgVersion.AC1018)) continue;
            if (r.LogicalType == TypeVport && r.Vport is { Captured: true })
                r.Vport.VisualStyleHandle = h;
            else if (r.LogicalType == TypeViewport && r.VportR2007aTailSlot)
                r.VportVisualStyleRef = new DwgHandleReference(5, h);
        }
        if (DebugTrace) System.Console.Error.WriteLine(
            $"VPSDEF 默认视觉样式 h={h:X} → 盖入已捕获 VPORT/VIEWPORT 的 visualstyle 槽");
    }

    private static ulong FindDefaultVisualStyleHandle(
        DwgObjectReader.WalkResult walk, byte[] src)
    {
        foreach (var r in walk.Records)
        {
            if (r.LogicalType != TypeVisualStyle || r.Handle == 0
                || r.SourceVersion is not (DwgVersion.AC1015 or DwgVersion.AC1018)) continue;
            long own, hs;
            try
            {
                var hdr = new BitReader(src, r.BodyStart);
                hdr.ReadBS();
                uint rl = hdr.ReadRL();
                own = hdr.BitPosition;
                hs = (long)r.BodyStart * 8 + rl;
            }
            catch { continue; }
            if (hs <= own || hs > (long)r.BodyEnd * 8) continue;
            for (long b = own; b + 16 <= hs; b++)
            {
                string v;
                try
                {
                    var p = new BitReader(src) { UnicodeStrings = false };
                    p.SetPosition((int)(b >> 3), (int)(b & 7));
                    v = p.ReadTV();
                }
                catch { break; }
                if (string.Equals(v, "2dWireframe", StringComparison.OrdinalIgnoreCase))
                    return r.Handle;
            }
        }
        return 0;
    }

    private static void CopyCentralizedTail(BitWriter body, byte[] src, long pos, long to,
        DwgObjectRecord rec, Target t)
    {
        var vp = rec.Vport;
        if (vp != null && VportR2007aApplies(rec, t) && pos == vp.XrefStartBit && pos + 4 <= to)
        {
            body.WriteBS((short)vp.XrefResolved);
            pos += 4;
        }
        long cut = vp?.LightsAnchorBit ?? 0;
        if (vp == null || !VportR2007aApplies(rec, t) || cut <= pos || cut >= to)
        {
            CopyBits(body, src, pos, (int)(to - pos));
            return;
        }
        CopyBits(body, src, pos, (int)(cut - pos));
        WriteVportR2007aLights(body);
        CopyBits(body, src, cut, (int)(to - cut));
    }

    private static bool VportR2007aApplies(DwgObjectRecord rec, Target t)
        => rec.LogicalType == TypeVport
            && rec.SourceVersion is DwgVersion.AC1015 or DwgVersion.AC1018
            && t.Version >= DwgVersion.AC1021 && rec.StringFields.Count == 1
            && rec.Vport is { Captured: true };

    private static void WriteVportR2007aLights(BitWriter body)
    {
        body.WriteB(true);
        body.WriteRC(1);
        body.WriteBD(0.0);
        body.WriteBD(0.0);
        body.WriteBS(0);
        body.WriteBL(unchecked((int)0xC30000FAu));
        body.WriteRC(0);
    }

    private static void WriteVportR2007aGrid(BitWriter body, DwgObjectRecord rec, Target t)
    {
        if (!VportR2007aApplies(rec, t)) return;
        body.WriteBS(3);
        body.WriteBS(5);
    }



    private static bool RewritesViewportR2007Up(DwgObjectRecord rec, Target t)
        => rec.LogicalType == TypeViewport
            && rec.SourceVersion is DwgVersion.AC1015 or DwgVersion.AC1018
            && t.Version >= DwgVersion.AC1021
            && rec.VportR2007aTailSlot;

    internal static bool RewriteViewportR2007Up(BitWriter w, DwgObjectRecord rec, byte[] src,
        long geoStart, long geoLen, Target t)
    {
        long geoEnd = geoStart + geoLen;
        if (geoLen <= 0 || geoEnd > (long)src.Length * 8) return false;
        var tmp = new BitWriter(64);
        try
        {
            long anchor = ViewportCircleZoomEndBit(src, geoStart, geoEnd);
            bool lacksShadePlot = rec.SourceVersion < DwgVersion.AC1018;
            long statusEnd = ViewportFrozenAndStatusEndBit(src, anchor, geoEnd);
            if (statusEnd <= anchor || geoEnd - (statusEnd + 2) != (lacksShadePlot ? 32 : 34)) return false;

            CopyBits(tmp, src, geoStart, (int)(anchor - geoStart));
            tmp.WriteBS(5);
            CopyBits(tmp, src, anchor, (int)(statusEnd - anchor));
            CopyBits(tmp, src, statusEnd + 2, (int)(geoEnd - statusEnd - 2));
            if (lacksShadePlot) tmp.WriteBS(0);
            WriteVportR2007aLights(tmp);
            tmp.WriteBits(0x40401u, 19);

            Append(w, tmp);
        }
        catch
        {
            return false;
        }
        if (DebugTrace)
            System.Console.Error.WriteLine(
                $"VP2007U h={rec.Handle:X} {rec.SourceVersion}→{t.Version} " +
                $"geoBits={geoLen}→{tmp.BitPosition}");
        return true;
    }

    private static bool AppendsViewportShadePlotR2004(DwgObjectRecord rec, Target t)
        => rec.LogicalType == TypeViewport && rec.SourceVersion == DwgVersion.AC1015
            && t.Version == DwgVersion.AC1018;

    private static bool AppendViewportShadePlotR2004(BitWriter w, DwgObjectRecord rec, byte[] src,
        long geoStart, long geoLen)
    {
        long geoEnd = geoStart + geoLen;
        if (geoLen <= 0 || geoEnd > (long)src.Length * 8) return false;
        var tmp = new BitWriter(64);
        try
        {
            long anchor = ViewportCircleZoomEndBit(src, geoStart, geoEnd);
            long statusEnd = ViewportFrozenAndStatusEndBit(src, anchor, geoEnd);
            if (anchor <= geoStart || statusEnd <= anchor
                || geoEnd - (statusEnd + 2) != 32) return false;
            CopyBits(tmp, src, geoStart, (int)(geoEnd - geoStart));
            tmp.WriteBS(0);
        }
        catch { return false; }
        Append(w, tmp);
        if (DebugTrace)
            System.Console.Error.WriteLine(
                $"VP2004U h={rec.Handle:X} geoBits={geoLen}→{tmp.BitPosition}");
        return true;
    }

    private static bool RewritesViewportR2007Down(DwgObjectRecord rec, Target t)
        => rec.LogicalType == TypeViewport
            && rec.SourceVersion >= DwgVersion.AC1021
            && t.Version is DwgVersion.AC1018 or DwgVersion.AC1015
            && VpDownVar() != 0;

    private static int VpDownVar()
    {
        var s = System.Environment.GetEnvironmentVariable("VPDVAR");
        return int.TryParse(s, out var v) ? v : 1;
    }

    internal static bool RewriteViewportR2007Down(BitWriter w, DwgObjectRecord rec, byte[] src,
        long geoStart, long geoLen, Target t)
    {
        long geoEnd = geoStart + geoLen;
        if (geoLen <= 0 || geoEnd > (long)src.Length * 8) return false;
        var tmp = new BitWriter(64);
        long anchorRel = 0;
        try
        {
            long anchor = ViewportCircleZoomEndBit(src, geoStart, geoEnd);
            if (anchor <= geoStart) return false;
            long segStart = anchor + 10;
            long statusEnd = ViewportFrozenAndStatusEndBit(src, segStart, geoEnd);
            long ucsBits = t.Version == DwgVersion.AC1015 ? 32 : 34;
            if (statusEnd <= segStart || geoEnd - (statusEnd + 34) != 76) return false;
            anchorRel = anchor - geoStart;
            CopyBits(tmp, src, geoStart, (int)(anchor - geoStart));
            CopyBits(tmp, src, segStart, (int)(statusEnd - segStart));
            tmp.WriteBS(0);
            CopyBits(tmp, src, statusEnd, (int)ucsBits);
        }
        catch
        {
            return false;
        }
        Append(w, tmp);
        if (DebugTrace)
            System.Console.Error.WriteLine(
                $"VP2007D h={rec.Handle:X} {rec.SourceVersion}→{t.Version} " +
                $"geoBits={geoLen}→{tmp.BitPosition} anchorRel={anchorRel}");
        return true;
    }

    private static long ViewportFrozenAndStatusEndBit(byte[] src, long anchor, long geoEnd)
    {
        if (anchor <= 0 || anchor + 4 > geoEnd) return 0;
        try
        {
            var br = new BitReader(src, 0);
            DwgObjectRecord.SeekTo(br, anchor);
            br.ReadBL();
            br.ReadBL();
            long end = br.BitPosition;
            return end > anchor && end + 2 <= geoEnd ? end : 0;
        }
        catch
        {
            return 0;
        }
    }

    private static long ViewportCircleZoomEndBit(byte[] src, long geoStart, long geoEnd)
    {
        try
        {
            var br = new BitReader(src, 0);
            DwgObjectRecord.SeekTo(br, geoStart);
            br.Read3BD();
            br.ReadBD();
            br.ReadBD();
            br.Read3BD();
            br.Read3BD();
            br.ReadBD();
            br.ReadBD();
            br.ReadBD();
            br.ReadBD();
            br.ReadBD();
            br.ReadBD();
            br.Read2RD();
            br.Read2RD();
            br.Read2RD();
            br.Read2RD();
            br.ReadBS();
            long anchor = br.BitPosition;
            return anchor > geoStart && anchor <= geoEnd ? anchor : 0;
        }
        catch
        {
            return 0;
        }
    }
}
