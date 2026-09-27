using DwgParser.Core.IO;

namespace DwgParser.Core.Objects;

public static partial class DwgObjectStreamWriter
{
    internal static bool TryCopyDataRegionUpgradingCmc(BitWriter body, byte[] src, long from, long to,
        DwgObjectRecord rec, out int addedBits)
    {
        addedBits = 0;
        if (rec.Kind == DwgObjectRecordKind.Entity) return false;
        if (!TrySourceDataEnd(src, rec, out long dataEnd)) return false;
        if (from < 0 || dataEnd <= from || dataEnd > to) return false;
        List<long>? cmcs = IsSunRecord(rec)
            ? LocateSunCmcBits(src, from, dataEnd)
            : rec.LogicalType switch
            {
                TypeDimStyle => LocateDimStyleCmcBits(src, from, dataEnd),
                TypeMLineStyle => LocateMLineStyleCmcBits(src, from, dataEnd),
                TypeLayer => LocateLayerCmcBits(src, from, dataEnd),
                TypeMLeaderStyle => Records.MLeaderStyleCarryAcR2000ToAc1021.TryLocateShortCmcBits(src, rec),
                TypeSectionViewStyle => LocateViewStyleCmcBits(src, from, dataEnd, section: true),
                TypeDetailViewStyle => LocateViewStyleCmcBits(src, from, dataEnd, section: false),
                _ => null,
            };
        if (cmcs is null || cmcs.Count == 0) return false;
        return TryWriteRegionUpgradingCmc(body, src, from, to, cmcs, dataEnd, rec, out addedBits);
    }

    private static bool CarriesMLeaderCmcR2004Up(DwgObjectRecord rec, Target t)
        => rec.LogicalType == TypeMLeader && t.Version == DwgVersion.AC1018
            && rec.SourceVersion < DwgVersion.AC1018 && rec.MLeaderShortCmcBits is { Count: > 0 };

    internal static bool TryCopyGeometryUpgradingMLeaderCmc(BitWriter body, byte[] src,
        long from, long to, DwgObjectRecord rec, out int addedBits)
    {
        addedBits = 0;
        var cmcs = rec.MLeaderShortCmcBits;
        if (cmcs is null || cmcs.Count == 0) return false;
        return TryWriteRegionUpgradingCmc(body, src, from, to, cmcs, to, rec, out addedBits);
    }

    private static void CarryMLeaderGeometryUpgradingCmc(BitWriter body, byte[] src, DwgObjectRecord rec,
        Target t, long geoStart, int geoLen, string? leadingString)
    {
        if (TryCopyGeometryUpgradingMLeaderCmc(body, src, geoStart, geoStart + geoLen, rec, out int added))
        {
            if (DebugTrace)
                System.Console.Error.WriteLine(
                    $"MLCMC h={rec.Handle:X} 几何区升宽 +{added} 位（{geoLen}→{geoLen + added}）");
            return;
        }
        CarryWithStrings(body, rec, src, geoStart, geoLen, t, leadingString);
    }

    private static bool TryWriteRegionUpgradingCmc(BitWriter body, byte[] src, long from, long to,
        List<long> cmcs, long anchorLimit, DwgObjectRecord rec, out int addedBits)
    {
        var vals = new short[cmcs.Count];
        var widths = new int[cmcs.Count];
        long prev = from;
        addedBits = 0;
        for (int i = 0; i < cmcs.Count; i++)
        {
            long p = cmcs[i];
            if (p < prev || p >= anchorLimit) return false;
            short idx = ReadBsAt(src, p, out int w);
            if (idx is < 0 or > 257) return false;
            vals[i] = idx;
            widths[i] = w;
            prev = p + w;
            addedBits += 44 - w;
        }

        long cur = from;
        for (int i = 0; i < cmcs.Count; i++)
        {
            CopyBits(body, src, cur, (int)(cmcs[i] - cur));
            body.WriteBS(0);
            body.WriteBL(unchecked((int)Records.VisualStyleCarryAcR2000ToAc1021.UpgradedRgb(vals[i])));
            body.WriteRC(0);
            cur = cmcs[i] + widths[i];
        }
        CopyBits(body, src, cur, (int)(to - cur));
        if (DebugTrace)
            System.Console.Error.WriteLine(
                $"CMCWID h={rec.Handle:X} t={rec.TypeCode} 升宽 {cmcs.Count} 枚短 CMC"
                + $" @ {string.Join(',', cmcs.Select(p => p - (long)rec.BodyStart * 8))}"
                + $"（窗 {to - from} 位 / 源数据区 {anchorLimit - from} 位闭合）");
        return true;
    }

    private static bool TrySourceDataEnd(byte[] src, DwgObjectRecord rec, out long dataEnd)
    {
        dataEnd = -1;
        if (rec.BodyStart <= 0 || rec.BodyEnd <= rec.BodyStart) return false;
        var br = new BitReader(src, rec.BodyStart);
        _ = br.ReadBS();
        uint rl = br.ReadRL();
        if (rl == 0 || rl > ((long)rec.BodyEnd - rec.BodyStart + 8) * 8) return false;
        dataEnd = (long)rec.BodyStart * 8 + rl;
        return dataEnd <= (long)rec.BodyEnd * 8;
    }

    private static List<long>? LocateDimStyleCmcBits(byte[] src, long from, long dataEnd)
    {
        var br = new BitReader(src);
        DwgObjectRecord.SeekTo(br, from);
        SkipTv(br);
        br.ReadB(); _ = br.ReadBS(); br.ReadB();
        SkipTv(br); SkipTv(br);
        for (int i = 0; i < 9; i++) br.ReadBD();
        for (int i = 0; i < 6; i++) br.ReadB();
        for (int i = 0; i < 3; i++) _ = br.ReadBS();
        for (int i = 0; i < 9; i++) br.ReadBD();
        br.ReadB(); _ = br.ReadBS();
        for (int i = 0; i < 4; i++) br.ReadB();
        var cmcs = new List<long>(3);
        for (int i = 0; i < 3; i++)
        {
            if (br.BitPosition >= dataEnd) return null;
            cmcs.Add(br.BitPosition);
            short idx = br.ReadBS();
            if (idx is < 0 or > 257) return null;
        }
        for (int i = 0; i < 10; i++) _ = br.ReadBS();
        _ = br.ReadBS(); br.ReadB(); br.ReadB();
        _ = br.ReadBS();
        for (int i = 0; i < 3; i++) _ = br.ReadBS();
        br.ReadB();
        _ = br.ReadBS();
        _ = br.ReadBS(); _ = br.ReadBS();
        br.ReadB();
        return br.BitPosition == dataEnd ? cmcs : null;
    }

    private static List<long>? LocateMLineStyleCmcBits(byte[] src, long from, long dataEnd)
    {
        var br = new BitReader(src);
        DwgObjectRecord.SeekTo(br, from);
        SkipTv(br);
        SkipTv(br);
        _ = br.ReadBS();
        var cmcs = new List<long>(3) { br.BitPosition };
        _ = br.ReadBS();
        br.ReadBD(); br.ReadBD();
        int n = br.ReadRC();
        if (n is < 0 or > 32) return null;
        for (int i = 0; i < n; i++)
        {
            br.ReadBD();
            cmcs.Add(br.BitPosition);
            _ = br.ReadBS();
            _ = br.ReadBS();
            if (br.BitPosition > dataEnd) return null;
        }
        return br.BitPosition == dataEnd ? cmcs : null;
    }

    private static List<long>? LocateLayerCmcBits(byte[] src, long from, long dataEnd)
    {
        var br = new BitReader(src);
        DwgObjectRecord.SeekTo(br, from);
        SkipTv(br);
        br.ReadB(); _ = br.ReadBS(); br.ReadB();
        _ = br.ReadBS();
        long cmc = br.BitPosition;
        _ = br.ReadBS();
        return br.BitPosition == dataEnd ? new List<long> { cmc } : null;
    }

    private static bool IsSunRecord(DwgObjectRecord rec)
        => rec.Kind != DwgObjectRecordKind.Entity
           && ClassTypeCodes.Normalize(rec.ClassName) == "SUN";

    private static List<long>? LocateSunCmcBits(byte[] src, long from, long dataEnd)
    {
        var br = new BitReader(src);
        DwgObjectRecord.SeekTo(br, from);
        _ = br.ReadBL();
        br.ReadB();
        long cmc = br.BitPosition;
        short idx = br.ReadBS();
        if (idx is < 0 or > 257) return null;
        br.ReadBD();
        br.ReadB();
        _ = br.ReadBL(); _ = br.ReadBL();
        br.ReadB();
        _ = br.ReadBL();
        _ = br.ReadBS();
        br.ReadRC();
        return br.BitPosition == dataEnd ? new List<long> { cmc } : null;
    }

    private static bool TryCopyDataRegionLayoutToR2004(BitWriter body, byte[] src, long from, long to,
        DwgObjectRecord rec, bool rlInBody)
    {
        if (rec.Layout is not { Captured: true } cap) return false;
        long pvStart = cap.PlotViewStartBit, pvEnd = cap.PlotViewEndBit;
        if (pvStart <= from || pvEnd <= pvStart || pvEnd > to) return false;
        if (!TrySourceDataEnd(src, rec, out long dataEnd) || dataEnd < from || dataEnd > to) return false;
        bool hsInBody = dataEnd < to;
        if (hsInBody && (cap.ViewportHandles is null || cap.XdicEndBit <= dataEnd
                || cap.LastRefEndBit <= cap.XdicEndBit || cap.LastRefEndBit > to)) return false;

        var br = new BitReader(src);
        DwgObjectRecord.SeekTo(br, pvEnd);
        if (!FiniteBr(br)) return false;
        long duStart = br.BitPosition;
        double duVal = br.ReadBD();
        if (double.IsNaN(duVal) || double.IsInfinity(duVal)) return false;
        long duEnd = br.BitPosition;
        long stylesheetAt = br.BitPosition;
        _ = br.ReadBS();
        if (!FiniteBr(br) || !FiniteBr(br) || !FiniteBr(br)) return false;
        long shadeAt = br.BitPosition;
        if (cap.StringStarts is not { Length: > 3 } strs || strs[3] != shadeAt) return false;
        SkipTv(br);
        _ = br.ReadBS(); _ = br.ReadBS();
        for (int i = 0; i < 3; i++) if (!FiniteBr(br)) return false;
        for (int i = 0; i < 2; i++) if (!FiniteRd(br)) return false;
        for (int i = 0; i < 2; i++) if (!FiniteRd(br)) return false;
        for (int i = 0; i < 9; i++) if (!FiniteBr(br)) return false;
        if (!FiniteBr(br)) return false;
        _ = br.ReadBS();
        for (int i = 0; i < 6; i++) if (!FiniteBr(br)) return false;
        if (br.BitPosition != dataEnd) return false;

        long bodyAt = body.BitPosition;
        CopyBits(body, src, from, (int)(pvStart - from));
        CopyBits(body, src, pvEnd, (int)(duStart - pvEnd));
        if (duVal == 0.0) body.WriteBD(1.0);
        else CopyBits(body, src, duStart, (int)(duEnd - duStart));
        CopyBits(body, src, duEnd, (int)(stylesheetAt - duEnd));
        body.WriteBS(0);
        CopyBits(body, src, stylesheetAt, (int)(shadeAt - stylesheetAt));
        body.WriteBS(0); body.WriteBS(2); body.WriteBS(300);
        CopyBits(body, src, shadeAt, (int)(dataEnd - shadeAt));
        int nvp = hsInBody ? cap.ViewportHandles!.Count : 0;
        body.WriteBL(nvp);
        int added = (int)(body.BitPosition - bodyAt) - (int)(dataEnd - from);
        if (hsInBody) CopyLayoutR2004HandleRegion(body, src, dataEnd, cap);
        if (rlInBody && added != 0)
        {
            uint rlNow = new BitReader(body.ToArray(), 0).ReadRL();
            body.PatchRL(0, unchecked((uint)(rlNow + added)));
            if (DebugTrace)
                System.Console.Error.WriteLine(
                    $"LAY18-RL h={rec.Handle:X} RL {rlNow}→{rlNow + added}（数据区 +{added} 位）");
        }
        if (DebugTrace)
            System.Console.Error.WriteLine(
                $"LAY18 h={rec.Handle:X} t={rec.TypeCode} from={from} to={to} dataEnd={dataEnd} hsInBody={hsInBody}"
                + $" 剥 pv {pvEnd - pvStart} 位 + 插 stylesheet/shade/nviewp={cap.ViewportHandles?.Count ?? 0}"
                + $" + 句柄区重写 {body.BitPosition - bodyAt - added} 位（源窗 {to - from} → 目标 {body.BitPosition - bodyAt} 位）");
        return true;
    }

    private static void SkipTv(BitReader br)
    {
        int len = br.ReadBS();
        for (int i = 0; i < len && !br.EndOfStream; i++) _ = br.ReadRC();
    }

    private static bool FiniteBr(BitReader br)
    {
        double v = br.ReadBD();
        return !double.IsNaN(v) && !double.IsInfinity(v);
    }

    private static bool FiniteRd(BitReader br)
    {
        double v = br.ReadRD();
        return !double.IsNaN(v) && !double.IsInfinity(v);
    }

    private static List<long>? LocateViewStyleCmcBits(byte[] src, long from, long dataEnd, bool section)
        => LocateViewStylePreR2004(src, from, dataEnd, section)?.CmcBits;

    private static short ReadBsAt(byte[] src, long bitPos, out int width)
    {
        var br = new BitReader(src);
        DwgObjectRecord.SeekTo(br, bitPos);
        short v = br.ReadBS();
        width = (int)(br.BitPosition - bitPos);
        return v;
    }
}
