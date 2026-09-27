using DwgParser.Core.IO;
using static DwgParser.Core.Objects.DwgObjectStreamWriter;

namespace DwgParser.Core.Objects.Records;

internal abstract class DimStyleCarryAc1018Up : DimStyleCarryPlan
{
    protected abstract bool TargetIsR2010 { get; }

    protected virtual bool SourcePreR2004 => false;

    public override bool IsCaptured(DwgObjectRecord rec) => rec.DimStyle?.Flag0EndBit > 0;

    internal override bool TryCarry(BitWriter body, byte[] src, long from, long to,
        DwgObjectRecord rec, Target t)
    {
        var cap = rec.DimStyle;
        if (cap == null) return false;
        if (cap.Flag0EndBit <= 0 || cap.BodyStartBit <= 0
            || cap.NineBdEndBit <= 0 || cap.ThreeBsEndBit <= 0
            || cap.CmcStartBit <= 0 || cap.FxlonEndBit <= 0)
            return false;
        bool pre2004 = SourcePreR2004;
        bool carriesRl = PrefixHasRl(rec.SourceVersion) && !TargetIsR2010;
        long ownStart = rec.CarryStartBit + 32;
        long expectFrom = carriesRl ? rec.CarryStartBit : ownStart;
        long srcEnd = pre2004 ? cap.HsBit : rec.HandleStreamStartBit;
        if (from != expectFrom || to < cap.Flag0EndBit) return false;
        if (srcEnd < cap.Flag0EndBit) return false;
        if (cap.CmcStartBit - 4 < cap.ThreeBsEndBit) return false;
        if (pre2004 && to != srcEnd) return false;

        string name, dimpost, dimapost;
        long bdStart;
        short dimlwd, dimlwe; bool flag0;        try
        {
            var br = new BitReader(src) { UnicodeStrings = false };
            DwgObjectRecord.SeekTo(br, cap.NameStartBit);
            name = br.ReadTV();
            DwgObjectRecord.SeekTo(br, cap.BodyStartBit);
            dimpost = br.ReadTV();
            dimapost = br.ReadTV();
            bdStart = br.BitPosition;
            DwgObjectRecord.SeekTo(br, cap.LwdStartBit);
            dimlwd = br.ReadBS();
            dimlwe = br.ReadBS();
            flag0 = br.ReadB();
        }
        catch { return false; }

        long lwdStart = cap.LwdStartBit;
        long srcFrameEnd = srcEnd;
        if (lwdStart <= 0 || cap.Flag0EndBit != srcFrameEnd) return false;

        var head = new BitWriter(16);
        head.WriteBD(1.0);
        head.WriteBD(0.7853981633974483);
        head.WriteBS(0);
        if (pre2004)
        {
            if (cap.PreR2004CmcIndices is not { Length: 3 }) return false;
            WriteCmcTriple(head, cap.PreR2004CmcIndices[0]);
        }
        else CopyBits(head, src, cap.CmcStartBit, 44);
        int headBits = (int)head.BitPosition;
        if (headBits != 114) return false;

        var r2010bFull = new BitWriter(16);
        if (TargetIsR2010)
        {
            foreach (var b in R2010bConst) r2010bFull.WriteRC(b);
            if (r2010bFull.BitPosition != 136) return false;
        }

        if (carriesRl) body.WriteRL(0);
        long eedStart = cap.EedStartBit, eedEnd = cap.EedStartBit + cap.EedBitLen;
        bool eedOk = t.UnicodeStrings && cap.EedBitLen > 2
            && eedStart >= ownStart && eedEnd <= cap.NameStartBit;
        bool eedSimple = false, eedXdata = false;
        if (eedOk)
        {
            eedSimple = TryCarryEedR2007Plus(new BitWriter(16), src, eedStart, eedEnd, rec);
            if (!eedSimple) eedXdata = TryCarryEedXdataR2007Plus(new BitWriter(16), src, eedStart, eedEnd, rec);
        }
        if (eedSimple || eedXdata)
        {
            CopyBits(body, src, ownStart, (int)(eedStart - ownStart));
            if (eedSimple) TryCarryEedR2007Plus(body, src, eedStart, eedEnd, rec);
            else TryCarryEedXdataR2007Plus(body, src, eedStart, eedEnd, rec);
            CopyBits(body, src, eedEnd, (int)(cap.NameStartBit - eedEnd));
            if (DebugTrace)
                System.Console.Error.WriteLine(
                    $"DSUP h={rec.Handle:X} EED [{eedStart},{eedEnd}) → {(eedSimple ? "简单" : "XDATA")} 化");
        }
        else
        {
            CopyBits(body, src, ownStart, (int)(cap.NameStartBit - ownStart));
        }
        if (pre2004)
            body.WriteB(SourceXdicRefIsNull(src, srcEnd, (long)rec.BodyEnd * 8));
        if (t.R2013Plus) body.WriteB(false);
        body.WriteBS((short)cap.XrefResolved);
        CopyBits(body, src, bdStart, (int)(cap.NineBdEndBit - bdStart));
        Append(body, head);
        CopyBits(body, src, cap.NineBdEndBit, (int)(cap.ThreeBsEndBit - cap.NineBdEndBit));
        body.WriteBS(0);
        if (pre2004)
        {
            if (cap.CmcEndBit <= cap.CmcStartBit || cap.FxlonEndBit < cap.CmcEndBit) return false;
            CopyBits(body, src, cap.ThreeBsEndBit, (int)(cap.CmcStartBit - cap.ThreeBsEndBit));
            for (int i = 0; i < 3; i++) WriteCmcTriple(body, cap.PreR2004CmcIndices![i]);
            CopyBits(body, src, cap.CmcEndBit, (int)(cap.FxlonEndBit - cap.CmcEndBit));
        }
        else
            CopyBits(body, src, cap.ThreeBsEndBit, (int)(cap.FxlonEndBit - cap.ThreeBsEndBit));
        body.WriteB(false);
        if (TargetIsR2010)
        {
            var r2010bBytes = r2010bFull.ToArray();
            CopyBits(body, r2010bBytes, 0, 133);
        }
        body.WriteBS(dimlwd);
        body.WriteBS(dimlwe);
        body.WriteB(flag0);

        long contentStart = body.BitPosition;
        WriteCentralTu(body, name);
        WriteCentralTu(body, dimpost);
        WriteCentralTu(body, dimapost);
        if (TargetIsR2010)
        {
            WriteCentralTu(body, "");
            WriteCentralTu(body, "");
        }
        int contentBits = (int)(body.BitPosition - contentStart);
        WriteStringStreamSize(body, (uint)contentBits);
        body.WriteB(true);

        if (carriesRl)
        {
            int typeBits = (int)(rec.CarryStartBit - (long)rec.BodyStart * 8);
            int dataBits = (int)body.BitPosition - 32;
            body.PatchRL(0, (uint)(typeBits + 32 + dataBits));
        }
        if (DebugTrace)
            System.Console.Error.WriteLine(
                $"DSUP h={rec.Handle:X} {rec.SourceVersion}→{t.Version} lwd={lwdStart} f0={cap.Flag0EndBit} 头段{headBits}+R2010b135 " +
                $"串流{contentBits}位 尾段0位(§77续:零尾段)");
        if (pre2004)
        {
            foreach (var sf in cap.DeferredStringFields!) rec.StringFields.Add(sf);
            if (DebugTrace)
                System.Console.Error.WriteLine(
                    $"DSUP15 h={rec.Handle:X} CMC 升宽 3 枚 + 自插 xd + 转登 {cap.DeferredStringFields!.Count} 串");
        }
        return true;
    }

    internal static void WriteCmcTriple(BitWriter w, short index)
    {
        w.WriteBS(0);
        w.WriteBL(unchecked((int)Records.VisualStyleCarryAcR2000ToAc1021.UpgradedRgb(index)));
        w.WriteRC(0);
    }
}

internal sealed class DimStyleCarryAc1018ToAc1021 : DimStyleCarryAc1018Up
{
    public override string Name => "AC1018→AC1021";

    protected override bool TargetIsR2010 => false;
}

internal sealed class DimStyleCarryAc1018ToR2010 : DimStyleCarryAc1018Up
{
    public override string Name => "AC1018→R2010+";

    protected override bool TargetIsR2010 => true;
}
