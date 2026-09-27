using DwgParser.Core.IO;
using static DwgParser.Core.Objects.DwgObjectStreamWriter;

namespace DwgParser.Core.Objects.Records;

internal sealed class XrecordCarryPreR2007Up
{
    public string Name => "AC1015→R2007+";

    public bool IsCaptured(DwgObjectRecord rec) => true;

    internal bool TryCarryFrameStart(BitWriter body, byte[] src, long from, long to,
        DwgObjectRecord rec, Target t)
        => TryCarryCore(body, src, from, to, rec, t, r2010: false);

    internal bool TryCarryFrameStartR2010(BitWriter body, byte[] src, long from, long to,
        DwgObjectRecord rec, Target t)
        => TryCarryCore(body, src, from, to, rec, t, r2010: true);

    private bool TryCarryCore(BitWriter body, byte[] src, long from, long to,
        DwgObjectRecord rec, Target t, bool r2010)
    {
        if (!t.UnicodeStrings || rec.SourceVersion >= DwgVersion.AC1021) return false;
        bool srcR2004 = rec.SourceVersion.IsR2004Plus();
        long start = rec.CarryStartBit;
        long ownStart = start + 32;
        if (r2010)
        {
            if (from != ownStart) return false;
        }
        else if (from != start) return false;

        long hsBit = rec.HasHandleStream && rec.HandleStreamStartBit > 0
            ? rec.HandleStreamStartBit
            : PreR2010HandleStreamStart(src, rec);
        if (hsBit <= 0) return false;

        var br = new BitReader(src);
        DwgObjectRecord.SeekTo(br, ownStart);
        br.ReadHandle();
        long eedStart = br.BitPosition;
        if (!DwgObjectReader.TrySkipEed(br)) return false;
        long eedLen = br.BitPosition - eedStart;
        if (eedLen > 2)
        {
            if (DebugTrace)
                System.Console.Error.WriteLine($"XREEDE h={rec.Handle:X} eedLen={eedLen}（含 EED 未支持）");
            return false;
        }
        br.ReadBL();
        long xsStart = br.BitPosition;
        bool srcXdicMissing = false;
        if (srcR2004)
        {
            srcXdicMissing = br.ReadB();
        }
        int xsize = br.ReadBL();
        if (xsize is < 0 or > 0xFFFF) return false;
        long xdStart = br.BitPosition;
        long xdEnd = xdStart + (long)xsize * 8;
        long cloningStart = xdEnd;
        DwgObjectRecord.SeekTo(br, xdEnd);
        int cloning = br.ReadBS();
        if (DebugTrace)
            System.Console.Error.WriteLine(
                $"XRWALK h={rec.Handle:X} xsize={xsize} xd=[{xdStart},{xdEnd}] cloning@{cloningStart} " +
                $"br@{br.BitPosition} hs={hsBit} frameEnd={(long)rec.BodyEnd * 8}");
        if (br.BitPosition > hsBit) return false;
        if (cloningStart + 32 > hsBit + 64) return false;

        long tolerantEnd = (long)rec.BodyEnd * 8 + 8;
        long convEnd = xdEnd;
        bool okProbe = TryConvertXdata(body: null, src, xdStart, xdEnd, xdEnd, out int newSize, out bool pastDeclared);
        if (!okProbe && TryConvertXdata(body: null, src, xdStart, tolerantEnd, xdEnd, out newSize, out pastDeclared))
        {
            convEnd = tolerantEnd;
            if (DebugTrace)
                System.Console.Error.WriteLine(
                    $"XRCONV h={rec.Handle:X} 严格相失步 ⇒ 按 §619 口径重试收敛 xd=[{xdStart},{convEnd}]");
            okProbe = true;
        }
        if (!okProbe)
        {
            if (DebugTrace)
                System.Console.Error.WriteLine(
                    $"XRCONV h={rec.Handle:X} xd=[{xdStart},{xdEnd}] xsize={xsize} probe 失败（未映射组码/越界）");
            return false;
        }
        if (newSize is < 0 or > 0xFFFF) return false;

        if (!r2010)
            body.WriteRL(0);
        CopyBits(body, src, ownStart, (int)(xsStart - ownStart));
        body.WriteB(srcR2004 ? srcXdicMissing : SourceXdicRefIsNull(src, hsBit, (long)rec.BodyEnd * 8));
        if (t.R2013Plus) body.WriteB(false);
        body.WriteBL(newSize);
        bool ok = TryConvertXdata(body, src, xdStart, convEnd, xdEnd, out int newSize2, out _);
        if (!ok || newSize2 != newSize) return false;
        body.WriteBS((short)(pastDeclared ? 0 : cloning));

        if (r2010) return true;
        int typeBits = (int)(rec.CarryStartBit - (long)rec.BodyStart * 8);
        int dataBits = (int)body.BitPosition - 32;
        body.PatchRL(0, (uint)(typeBits + 32 + dataBits));
        if (DebugTrace)
        {
            static string HexOf(BitWriter w)
            {
                var b = w.ToArray();
                var sb = new System.Text.StringBuilder(b.Length * 3);
                foreach (var x in b) sb.Append($"{x:x2} ");
                return sb.ToString();
            }
            var sw = new BitWriter(16);
            var sbr = new BitReader(src);
            DwgObjectRecord.SeekTo(sbr, xdStart);
            for (int i = 0; i < xsize; i++) sw.WriteRC(sbr.ReadRC());
            var tw = new BitWriter(16);
            TryConvertXdata(tw, src, xdStart, convEnd, xdEnd, out _, out _);
            System.Console.Error.WriteLine(
                $"XRHEX h={rec.Handle:X} xdStart={xdStart} src[{xsize}B]={HexOf(sw)} tgt[{newSize}B]={HexOf(tw)}");
        }
        return true;
    }

    private static bool TryConvertXdata(BitWriter? body, byte[] src, long start, long end, long declaredEnd,
        out int newSize, out bool pastDeclared)
    {
        newSize = 0;
        pastDeclared = false;
        try
        {
            var br = new BitReader(src);
            DwgObjectRecord.SeekTo(br, start);
            var probe = body ?? new BitWriter(16);
            long probeStart = probe.BitPosition;
            void Fail(int c, string why)
            {
                if (body == null && DebugTrace)
                    System.Console.Error.WriteLine(
                        $"XRITEM pos={br.BitPosition} code={c} {why}");
            }
            bool skipPre2007Group = false;
            bool terminated = false;
            bool Terminate(long itemStart)
            {
                if (itemStart < declaredEnd) return false;
                DwgObjectRecord.SeekTo(br, itemStart);
                terminated = true;
                return true;
            }
            while (br.BitPosition < end)
            {
                long itemStart = br.BitPosition;
                int code = br.ReadRS();
                int valBytes = 0; bool isString = false;
                if (code >= 300)
                {
                    if (code >= 440)
                    {
                        if (code >= 1000)
                        {
                            if (code == 1004) valBytes = -1;
                            else if (code <= 1009) isString = true;
                            else if (code <= 1039) valBytes = 24;
                            else if (code <= 1042) valBytes = 8;
                            else if (code <= 1069) valBytes = 24;
                            else if (code == 1070) valBytes = 2;
                            else if (code == 1071) valBytes = 4;
                            else { if (!Terminate(itemStart)) { Fail(code, "未映射组码"); return false; } break; }
                        }
                        else if (code <= 459) valBytes = 4;
                        else if (code <= 469) valBytes = 8;
                        else if (code <= 479) isString = true;
                        else if (code == 999) isString = true;
                        else { if (!Terminate(itemStart)) { Fail(code, "未映射组码"); return false; } break; }
                    }
                    else if (code <= 309) isString = true;
                    else if (code <= 319) valBytes = -1;
                    else if (code <= 329) valBytes = 8;
                    else if (code <= 369) valBytes = 8;
                    else if (code <= 389) valBytes = 2;
                    else if (code <= 399) valBytes = 8;
                    else if (code <= 409) valBytes = 2;
                    else if (code <= 419) isString = true;
                    else if (code <= 429) valBytes = 4;
                    else isString = true;
                }
                else if (code >= 105)
                {
                    if (code >= 210)
                    {
                        if (code <= 269) valBytes = 24;
                        else if (code <= 279) valBytes = 2;
                        else if (code <= 289) valBytes = 1;
                        else valBytes = 1;
                    }
                    else if (code == 105) valBytes = 8;
                    else if (code <= 109) { if (!Terminate(itemStart)) { Fail(code, "未映射组码"); return false; } break; }
                    else if (code <= 139) valBytes = 24;
                    else if (code <= 149) valBytes = 8;
                    else if (code <= 169) valBytes = 8;
                    else if (code <= 179) valBytes = 2;
                    else { if (!Terminate(itemStart)) { Fail(code, "未映射组码"); return false; } break; }
                }
                else if (code >= 38)
                {
                    if (code <= 59) valBytes = 8;
                    else if (code <= 79) valBytes = 2;
                    else if (code <= 99) valBytes = 4;
                    else isString = true;
                }
                else if (code <= 4) isString = true;
                else if (code == 5) valBytes = 8;
                else if (code <= 9) isString = true;
                else valBytes = 24;
                if (isString)
                {
                    int len = br.ReadRS();
                    if (len < 0 || br.BitPosition + 8 + (long)len * 8 > end)
                    { if (!Terminate(itemStart)) { Fail(code, $"串长越界 len={len}"); return false; } break; }
                    int cp = br.ReadRC();
                    _ = cp;
                    var units = new ushort[len];
                    for (int i = 0; i < len; i++) units[i] = (ushort)br.ReadRC();
                    int ulen = len;
                    while (ulen > 0 && units[ulen - 1] == 0) ulen--;
                    if (code == 102)
                    {
                        var name = new string(units.Take(ulen).Select(u => (char)u).ToArray())
                                   .TrimStart('\'');
                        if (name.StartsWith("ACAD_ROUNDTRIP_PRE2007_", StringComparison.Ordinal))
                            skipPre2007Group = true;
                        else
                            skipPre2007Group = false;
                    }
                    if (skipPre2007Group) continue;
                    probe.WriteRS((ushort)code);
                    probe.WriteRS((ushort)ulen);
                    for (int i = 0; i < ulen; i++) probe.WriteRS(units[i]);
                }
                else if (valBytes == -1)
                {
                    int lenc = br.ReadRC();
                    if (lenc < 0 || br.BitPosition + (long)lenc * 8 > end)
                    { if (!Terminate(itemStart)) { Fail(code, $"二进制越界 lenc={lenc}"); return false; } break; }
                    if (!skipPre2007Group)
                        CopyBits(probe, src, itemStart, (int)(br.BitPosition - itemStart) + lenc * 8);
                    DwgObjectRecord.SeekTo(br, itemStart + 16 + 8 + (long)lenc * 8);
                }
                else
                {
                    if (br.BitPosition + (long)valBytes * 8 > end)
                    { if (!Terminate(itemStart)) { Fail(code, $"值越界 valBytes={valBytes}"); return false; } break; }
                    if (!skipPre2007Group)
                        CopyBits(probe, src, itemStart, (int)(br.BitPosition - itemStart) + valBytes * 8);
                    DwgObjectRecord.SeekTo(br, itemStart + 16 + (long)valBytes * 8);
                }
            }
            if (!terminated && br.BitPosition != end)
            { Fail(-1, $"walk 止于 {br.BitPosition} ≠ end {end}"); return false; }
            if (terminated && br.BitPosition < declaredEnd)
            { Fail(-1, $"终结点 {br.BitPosition} 未抵声明末 {declaredEnd}"); return false; }
            pastDeclared = terminated;
            int size = (int)(probe.BitPosition - probeStart) / 8;
            if (body == null) { newSize = size; return true; }
            newSize = size;
            return true;
        }
        catch
        {
            return false;
        }
    }
}
