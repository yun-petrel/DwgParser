using DwgParser.Core.IO;

namespace DwgParser.Core.Objects;

public static partial class DwgObjectStreamWriter
{
    private static bool CarriesMLeaderStyleR2010ToAc1018(DwgObjectRecord rec, Target t)
        => t.Version == DwgVersion.AC1018
           && rec.SourceVersion.IsR2010Plus()
           && rec.LogicalType == TypeMLeaderStyle
           && rec.StringStreamStartBit > 0
           && rec.MLeader is { DownCaptured: true };

    private static bool CarriesMLeaderStyleAc1021Down(DwgObjectRecord rec, Target t)
        => t.Version == DwgVersion.AC1018
           && rec.SourceVersion == DwgVersion.AC1021
           && rec.LogicalType == TypeMLeaderStyle
           && rec.MLeader is { DownCaptured: true };

    private static bool CarryMLeaderStyleR2010ToAc1018(BitWriter body, DwgObjectRecord rec,
        byte[] src, Target t, out BitWriter? handles, DwgObjectReader.WalkResult? walk = null)
    {
        handles = null;
        if (!CarryMLeaderStyleCore(body, rec, src, t, rec.CarryStartBit, walk: walk)) return false;
        if (MLSynth() == 0) return true;
        if (FindAppIdHandle(walk, "ACAD_MLEADERVER") == 0) return true;
        long hs = rec.HandleStreamStartBit;
        long bodyEnd = (long)rec.BodyEnd * 8;
        if (hs <= 0 || hs >= bodyEnd)
        {
            if (DebugTrace) System.Console.Error.WriteLine($"MLSYNTH h={rec.Handle:X} 句柄流锚不合 hs={hs} bodyEnd={bodyEnd}");
            return true;
        }
        long hsEnd = LastMeaningfulHandleEnd(src, hs, bodyEnd);
        if (hsEnd <= hs)
        {
            if (DebugTrace) System.Console.Error.WriteLine($"MLSYNTH h={rec.Handle:X} 句柄流空 hsEnd={hsEnd}");
            return true;
        }
        int nr;
        {
            var hbr = new BitReader(src, 0);
            DwgObjectRecord.SeekTo(hbr, rec.CarryStartBit);
            hbr.ReadHandle();
            if (!DwgObjectReader.TrySkipEed(hbr)) return true;
            nr = hbr.ReadBL();
            if (nr is < 0 or > 8) return true;
        }
        var hw = new BitWriter(16);
        long hp = hs;
        int copied = 0, want = 1 + nr;
        while (copied < want && hp + 8 <= hsEnd)
        {
            var rbr = new BitReader(src, 0);
            DwgObjectRecord.SeekTo(rbr, hp);
            int cc = rbr.ReadRC();
            long bits = 8 + (cc & 0xF) * 8L;
            if (hp + bits > hsEnd) break;
            CopyBits(hw, src, hp, (int)bits);
            hp += bits; copied++;
        }
        bool hasSlot = false;
        if (copied == want && hp + 8 <= hsEnd)
        {
            var pbr = new BitReader(src, 0);
            DwgObjectRecord.SeekTo(pbr, hp);
            hasSlot = (pbr.ReadRC() >> 4) == 3;
        }
        if (copied == want && !hasSlot) hw.WriteRC(0x30);
        CopyBits(hw, src, hp, (int)(hsEnd - hp));
        handles = hw;
        if (DebugTrace) System.Console.Error.WriteLine($"MLSYNTH h={rec.Handle:X} 自出句柄流 nr={nr} 插槽@{(copied == want ? "owner+reactor 后" : "未成")} hs={hw.BitPosition}位");
        return true;
    }

    private static int MLSynth()
    {
        var s = System.Environment.GetEnvironmentVariable("MLSYNTH");
        return int.TryParse(s, out var v) ? v : 1;
    }

    private const string MLeaderEedPrefix = "010000001101010010";
    private const string MLeaderEedSuffix58 = "010001100000001000000000";

    private static void WriteMLeaderServerEed(BitWriter body, ulong appidHandle)
    {
        foreach (var c in MLeaderEedPrefix) body.WriteB(c == '1');
        for (int i = 15; i >= 0; i--) body.WriteB(((appidHandle >> i) & 1) != 0);
        foreach (var c in MLeaderEedSuffix58) body.WriteB(c == '1');
    }

    private static ulong FindAppIdHandle(DwgObjectReader.WalkResult? walk, string name)
    {
        if (walk is null) return 0;
        foreach (var r in walk.Records)
            if (r.LogicalType == TypeAppId && r.Appid is { Captured: true } a && a.Name == name)
                return r.Handle;
        return 0;
    }

    private static bool CarriesMLeaderStyleR2010ToAc1015(DwgObjectRecord rec, Target t)
        => t.Version == DwgVersion.AC1015
           && rec.SourceVersion.IsR2010Plus()
           && rec.LogicalType == TypeMLeaderStyle
           && rec.StringStreamStartBit > 0
           && rec.MLeader is { DownCaptured: true, DownCmcSpans.Count: 3 }
           && rec.HasHandleStream && rec.HandleStreamStartBit > 0
           && MLeaderCmcSpansUsable(rec.MLeader.DownCmcSpans, rec.MLeader.DownFieldsStartBit,
               rec.MLeader.DownAttachStartBit);

    private static bool CarriesMLeaderStyleAc1018ToAc1015(DwgObjectRecord rec, Target t)
        => t.Version == DwgVersion.AC1015
           && rec.SourceVersion == DwgVersion.AC1018
           && rec.LogicalType == TypeMLeaderStyle
           && rec.MLeader is { UpCaptured: true, DownCmcSpans.Count: 3, UpHeadEndBit: > 0 }
           && rec.HasHandleStream && rec.HandleStreamStartBit > 0;

    private static bool CarryMLeaderStyleAc1018ToAc1015(BitWriter body, DwgObjectRecord rec,
        byte[] src, out BitWriter? handles)
    {
        handles = null;
        var cap = rec.MLeader;
        if (cap is null || cap.DownCmcSpans.Count != 3) return false;
        long start = rec.CarryStartBit + 32;
        long fieldsStart = cap.UpHeadEndBit, hs = rec.HandleStreamStartBit;
        var c0 = cap.DownCmcSpans[0]; var c1 = cap.DownCmcSpans[1]; var c2 = cap.DownCmcSpans[2];
        if (start <= 0 || fieldsStart <= start || hs <= c2.End) return false;
        if (!(fieldsStart <= c0.Start && c0.End <= c1.Start && c1.End <= c2.Start)) return false;
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
            if (br.BitPosition != fieldsStart) return false;

            CopyBits(body, src, start, (int)(eedStart - start));
            if (!TryWriteEedPreR2007Range(body, src, eedStart, eedLen))
                CopyBits(body, src, eedStart, eedLen);
            body.WriteBL(nr);
            CopyBits(body, src, fieldsStart, (int)(c0.Start - fieldsStart));
            if (!WriteFoldedCmc(body, src, c0.Start, c0.End)) return false;
            CopyBits(body, src, c0.End, (int)(c1.Start - c0.End));
            if (!WriteFoldedCmc(body, src, c1.Start, c1.End)) return false;
            CopyBits(body, src, c1.End, (int)(c2.Start - c1.End));
            if (!WriteFoldedCmc(body, src, c2.Start, c2.End)) return false;
            CopyBits(body, src, c2.End, (int)(hs - c2.End));
            long hsEnd = LastMeaningfulHandleEnd(src, hs, (long)rec.BodyEnd * 8);
            if (hsEnd <= hs) return false;
            var hw = new BitWriter(16);
            CopyBits(hw, src, hs, (int)(hsEnd - hs));
            if (!HandleStreamHasXdicSlot(hw.ToArray(), hw.BitPosition)) hw.WriteRC(0x30);
            handles = hw;
            if (DebugTrace)
                System.Console.Error.WriteLine(
                    $"MLSDN1015 h={rec.Handle:X} AC1018→AC1015 折 3 枚 CMC body={body.BitPosition} hs={hw.BitPosition}");
            return true;
        }
        catch { return false; }
    }

    private static bool WriteFoldedCmc(BitWriter body, byte[] src, long s, long e)
    {
        var cb = new BitReader(src, 0);
        DwgObjectRecord.SeekTo(cb, s);
        var tri = DwgObjectReader.ReadCmcTriple(cb);
        if (cb.BitPosition != e) return false;
        if (tri.Idx is < 0 or > 256 || (tri.Rgb >> 24) is < 0xC0 or > 0xC8) return false;
        body.WriteBS(FoldCmcToPreR2004Index(tri.Rgb));
        return true;
    }

    private static bool CarriesMLeaderStyleAc1021ToAc1015(DwgObjectRecord rec, Target t)
        => t.Version == DwgVersion.AC1015
           && rec.SourceVersion == DwgVersion.AC1021
           && rec.LogicalType == TypeMLeaderStyle
           && rec.MLeader is { DownCaptured: true, DownCmcSpans.Count: 3 }
           && rec.HasHandleStream && rec.HandleStreamStartBit > 0
           && MLeaderCmcSpansUsable(rec.MLeader.DownCmcSpans, rec.MLeader.DownFieldsStartBit,
               rec.MLeader.DownAttachStartBit);

    private static bool CarryMLeaderStyleAc1021ToAc1015(BitWriter body, DwgObjectRecord rec,
        byte[] src, Target t, out BitWriter? handles)
    {
        handles = null;
        var ml = rec.MLeader!;
        if (!CarryMLeaderStyleCore(body, rec, src, t, rec.CarryStartBit + 32, ac1015: true))
            return false;
        long hsStart = rec.HandleStreamStartBit;
        long hsEnd = LastMeaningfulHandleEnd(src, hsStart, (long)rec.BodyEnd * 8);
        if (hsEnd <= hsStart) return false;
        var hs = new BitWriter(16);
        CopyBits(hs, src, hsStart, (int)(hsEnd - hsStart));
        long insertAt = DictionaryCarry.ScanOwnerReactorPrefix(hs.ToArray());
        if (insertAt <= 0 || insertAt > hs.BitPosition) return false;
        var probe = new BitReader(hs.ToArray(), 0);
        DwgObjectRecord.SeekTo(probe, insertAt);
        bool srcHasXdic = probe.ReadRC() == 3;
        handles = hs;
        if (DebugTrace)
            System.Console.Error.WriteLine(
                $"MLSDN15A21 h={rec.Handle:X} {rec.SourceVersion}→AC1015 body={body.BitPosition} " +
                $"hs={hs.BitPosition}位@ins{insertAt} srcXdic={srcHasXdic}(不补槽) " +
                $"cmcs=[{string.Join(",", ml.DownCmcSpans.Select(x => $"{x.Start}..{x.End}"))}]");
        return true;
    }

    internal static bool CarryMLeaderStyleR2010ToAc1015(BitWriter body, DwgObjectRecord rec,
        byte[] src, Target t, out BitWriter? handles)
    {
        handles = null;
        var ml = rec.MLeader!;
        if (!CarryMLeaderStyleCore(body, rec, src, t, rec.CarryStartBit, ac1015: true))
            return false;
        long hsStart = rec.HandleStreamStartBit;
        long hsEnd = LastMeaningfulHandleEnd(src, hsStart, (long)rec.BodyEnd * 8);
        if (hsEnd <= hsStart) return false;
        var hs = new BitWriter(16);
        CopyBits(hs, src, hsStart, (int)(hsEnd - hsStart));
        long insertAt = DictionaryCarry.ScanOwnerReactorPrefix(hs.ToArray());
        if (insertAt <= 0 || insertAt > hs.BitPosition) return false;
        var probe = new BitReader(hs.ToArray(), 0);
        DwgObjectRecord.SeekTo(probe, insertAt);
        bool srcHasXdic = probe.ReadRC() == 3;
        if (!srcHasXdic)
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
                $"MLSDN15 h={rec.Handle:X} {rec.SourceVersion}→AC1015 body={body.BitPosition} " +
                $"hs={hs.BitPosition}位@insert{insertAt} srcXdic={srcHasXdic} " +
                $"cmcs=[{string.Join(",", ml.DownCmcSpans.Select(x => $"{x.Start}..{x.End}"))}]");
        return true;
    }

    private static bool MLeaderCmcSpansUsable(List<(long Start, long End)> cmcs,
        long fieldsStart, long attachStart)
    {
        long prev = fieldsStart;
        foreach (var (s, e) in cmcs)
        {
            if (s < prev || e <= s || e > attachStart) return false;
            prev = e;
        }
        return prev <= attachStart;
    }

    private static void CopySpanCollapsingCmc(BitWriter body, byte[] src, long from, long to,
        List<(long Start, long End)> cmcs)
    {
        long cursor = from;
        foreach (var (s, e) in cmcs)
        {
            if (e <= from || s >= to) continue;
            CopyBits(body, src, cursor, (int)(s - cursor));
            var r = new BitReader(src, 0);
            DwgObjectRecord.SeekTo(r, s);
            body.WriteBS(r.ReadBS());
            cursor = e;
        }
        CopyBits(body, src, cursor, (int)(to - cursor));
    }

    private static bool CarryMLeaderStyleAc1021Down(BitWriter body, DwgObjectRecord rec,
        byte[] src, Target t)
        => CarryMLeaderStyleCore(body, rec, src, t, rec.CarryStartBit + 32);

    private static bool CarryMLeaderStyleCore(BitWriter body, DwgObjectRecord rec, byte[] src,
        Target t, long start, bool ac1015 = false,
        DwgObjectReader.WalkResult? walk = null)
    {
        var ml = rec.MLeader;
        if (ml is not { DownCaptured: true }) return false;
        long headEnd = ml.DownHeadEndBit;
        long fieldsStart = ml.DownFieldsStartBit;
        long attachStart = ml.DownAttachStartBit;
        long ssStart = ml.DownStreamStartBit;
        long descIns = ml.DownDescInsertBit;
        long tdIns = ml.DownTextDefInsertBit;
        if (start <= 0 || headEnd <= start) return false;
        if (!(headEnd <= fieldsStart && fieldsStart < descIns && descIns < tdIns
              && tdIns <= attachStart && attachStart <= ssStart)) return false;
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
            if (br.BitPosition != headEnd) return false;
            if (ac1015 && !MLeaderCmcSpansUsable(ml.DownCmcSpans, fieldsStart, attachStart))
                return false;

            bool synth = MLSynth() != 0 && !ac1015 && t.Version == DwgVersion.AC1018
                         && rec.SourceVersion.IsR2010Plus();
            ulong appid = synth ? FindAppIdHandle(walk, "ACAD_MLEADERVER") : 0;
            if (appid == 0) synth = false;

            CopyBits(body, src, start, (int)(eedStart - start));
            if (synth && eedLen == 2) WriteMLeaderServerEed(body, appid);
            if (!TryWriteEedPreR2007Range(body, src, eedStart, eedLen))
                CopyBits(body, src, eedStart, eedLen);
            body.WriteBL((int)numReactors);
            if (!ac1015) body.WriteB(synth ? false : xdic);

            if (ac1015)
            {
                var cmcs = ml.DownCmcSpans;
                CopySpanCollapsingCmc(body, src, fieldsStart, descIns, cmcs);
                body.WriteTV(ml.DownDesc);
                CopyBits(body, src, descIns, (int)(tdIns - descIns));
                body.WriteTV(ml.DownTextDefault);
                CopySpanCollapsingCmc(body, src, tdIns, attachStart, cmcs);
            }
            else
            {
                CopyBits(body, src, fieldsStart, (int)(descIns - fieldsStart));
                body.WriteTV(ml.DownDesc);
                CopyBits(body, src, descIns, (int)(tdIns - descIns));
                body.WriteTV(ml.DownTextDefault);
                CopyBits(body, src, tdIns, (int)(attachStart - tdIns));
            }
            if (DebugTrace)
                System.Console.Error.WriteLine(
                    $"MLSDOWN h={rec.Handle:X} {rec.SourceVersion}→{t.Version} " +
                    $"desc=\"{ml.DownDesc}\" td=\"{ml.DownTextDefault}\" own={start} head={headEnd} " +
                    $"fields={fieldsStart} descIns={descIns} tdIns={tdIns} attach={attachStart} " +
                    $"ss={ssStart} xdic={xdic} out={body.BitPosition}");
            return true;
        }
        catch
        {
            return false;
        }
    }
}
