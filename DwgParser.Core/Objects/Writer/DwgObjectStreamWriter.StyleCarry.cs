using DwgParser.Core.IO;

namespace DwgParser.Core.Objects;

public static partial class DwgObjectStreamWriter
{
    private const short TypeAppid = 0x43;

    private static void CopyDataRegionCentralizing(BitWriter body, byte[] src, long from, long to,
        DwgObjectRecord rec, Target t, long mleSkip, long mleInsert, bool rlInBody = false,
        long commonHeaderBit = -1, bool insertXdicMissing = false, bool xdicMissingValue = false,
        bool insertHasDsData = false, bool overwriteXdicAtAnchor = false)
    {
        bool mlsAc1021Down = rec.LogicalType == TypeMLeaderStyle && rec.SourceVersion.IsR2010Plus()
            && t.Version == DwgVersion.AC1021 && rec.MLeader?.DownCaptured == true;
        if (to <= from || from < 0 || ((mleSkip > 0 || mleInsert > 0) && !mlsAc1021Down))
        {
            CopyBitsWithMle(body, src, from, to, mleSkip, mleInsert);
            return;
        }
        if (TryCarryEditedAttribStrings(body, src, from, to, rec, t)) return;
        bool cmcWidenGate = t.Version == DwgVersion.AC1018
            || (t.Version > DwgVersion.AC1018 && IsSunRecord(rec));
        if (cmcWidenGate && rec.SourceVersion < DwgVersion.AC1018
            && TryCopyDataRegionUpgradingCmc(body, src, from, to, rec, out int cmcAdded))
        {
            if (rlInBody && cmcAdded > 0)
            {
                uint rlNow = new BitReader(body.ToArray(), 0).ReadRL();
                body.PatchRL(0, unchecked((uint)(rlNow + cmcAdded)));
                if (DebugTrace)
                    System.Console.Error.WriteLine(
                        $"CMCWID-RL h={rec.Handle:X} RL {rlNow}→{rlNow + cmcAdded}（升宽 +{cmcAdded} 位）");
            }
            return;
        }
        if (t.Version == DwgVersion.AC1018 && rec.SourceVersion < DwgVersion.AC1018
            && TryCopyRegionStrippingNullXdic(body, src, from, to, rec))
            return;
        if (t.Version == DwgVersion.AC1018 && rec.LogicalType == TypeBlockHeader
            && rec.SourceVersion < DwgVersion.AC1018
            && TryCarryBlockHeaderOwnedCount(body, src, from, to, rec, rlInBody))
            return;
        if (t.Version == DwgVersion.AC1018 && rec.LogicalType == TypeLayout
            && rec.SourceVersion < DwgVersion.AC1018
            && TryCopyDataRegionLayoutToR2004(body, src, from, to, rec, rlInBody))
            return;
        if (t.Version == DwgVersion.AC1018 && rec.LogicalType == TypeProxyObj
            && rec.SourceVersion < DwgVersion.AC1018
            && TryCopyDataRegionProxyClassNameToR2004(body, src, from, to, rec, rlInBody))
            return;
        if (t.Version == DwgVersion.AC1015 && rec.LogicalType == TypeProxyObj
            && rec.SourceVersion == DwgVersion.AC1018
            && TryDropProxyClassNameToR2000(body, src, from, to, rec, rlInBody))
            return;
        if (rec.LogicalType == TypeTableStyle && ObjectFactory.CreateTableStyleCarry(rec, t) is { } tsPlan)
        {
            if (tsPlan.IsCaptured(rec) && tsPlan.TryCarry(body, src, from, to, rec, t))
                return;
            CopyBits(body, src, from, (int)(to - from));
            return;
        }
        if ((rec.LogicalType == TypeSectionViewStyle || rec.LogicalType == TypeDetailViewStyle)
            && (t.R2010Plus || t.Version == DwgVersion.AC1021) && t.UnicodeStrings
            && rec.SourceVersion >= DwgVersion.AC1015 && rec.SourceVersion <= DwgVersion.AC1021)
        {
            if (rec.ViewStyle is { TemplateCaptured: true } && TryCarryViewStyleR2010(body, src, from, to, rec))
                return;
            if (!(t.Version == DwgVersion.AC1021 && CentralizeInlineStrings(rec, t)))
            {
                CopyBits(body, src, from, (int)(to - from));
                return;
            }
        }
        if (rec.LogicalType == TypeLType && ObjectFactory.CreateLtypeCarry(rec, t) is { } ltPlan)
        {
            if (ltPlan.IsCaptured(rec) && ltPlan.TryCarry(body, src, from, to, rec, t))
                return;
            CopyBits(body, src, from, (int)(to - from));
            return;
        }
        if (rec.LogicalType == TypeDimStyle
            && ObjectFactory.CreateDimStyleCarry(rec, t) is { } dsPlan)
        {
            if (dsPlan.IsCaptured(rec) && dsPlan.TryCarry(body, src, from, to, rec, t))
                return;
            CopyBits(body, src, from, (int)(to - from));
            return;
        }
        if (rec.LogicalType == TypeMLeaderStyle && ObjectFactory.CreateMLeaderStyleCarry(rec, t) is { } mlsPlan)
        {
            if (mlsPlan.IsCaptured(rec) && mlsPlan.TryCarry(body, src, from, to, rec, t))
                return;
            CopyBits(body, src, from, (int)(to - from));
            return;
        }
        if (rec.LogicalType == TypeVisualStyle
            && ObjectFactory.CreateVisualStyleCarry(rec, t) is { } vsPlan)
        {
            if (vsPlan.IsCaptured(rec) && vsPlan.TryCarry(body, src, from, to, rec, t))
                return;
            CopyBits(body, src, from, (int)(to - from));
            return;
        }
        if (rec.LogicalType == TypeStyle && ObjectFactory.CreateStyleCarry(rec, t) is { } styPlan)
        {
            if (styPlan.IsCaptured(rec) && styPlan.TryCarry(body, src, from, to, rec, t))
                return;
            CopyBits(body, src, from, (int)(to - from));
            return;
        }
        if (rec.LogicalType == TypeBlockHeader && t.R2007Plus
            && rec.SourceVersion >= DwgVersion.AC1015 && rec.SourceVersion < DwgVersion.AC1021
            && TryCarryBlockHeaderR2007(body, src, from, to, rec, t))
            return;
        if (CarriesRenderSettingsTvToAc1015(rec, t)
            && CarryRenderSettingsTvToAc1015(body, src, from, to, rec, rlInBody))
            return;
        if (!CentralizeInlineStrings(rec, t))
        {
            CopyBitsWithMle(body, src, from, to, mleSkip, mleInsert);
            return;
        }
        foreach (var f in rec.StringFields)
            if (f.Start < from || f.Start + f.BitLen > to)
            {
                if (DebugTrace) System.Console.Error.WriteLine(
                    $"CENT? h={rec.Handle:X} t={rec.TypeCode} 回退: f.Start={f.Start} len={f.BitLen} 区间=[{from},{to})");
                CopyBitsWithMle(body, src, from, to, mleSkip, mleInsert);
                return;
            }
        long pos = from;
        if (t.UnicodeStrings
            && rec.SourceVersion >= DwgVersion.AC1015 && rec.SourceVersion < DwgVersion.AC1021
            && rec.EedStartBit > 0 && rec.EedBitLen > 2
            && rec.EedStartBit >= from && rec.EedStartBit + rec.EedBitLen <= to)
        {
            long eedStart = rec.EedStartBit;
            long eedEnd = eedStart + rec.EedBitLen;
            var probe = new BitWriter(16);
            if (TryCarryEedR2007Plus(probe, src, eedStart, eedEnd, rec))
            {
                CopyBits(body, src, pos, (int)(eedStart - pos));
                TryCarryEedR2007Plus(body, src, eedStart, eedEnd, rec);
                pos = eedEnd;
                if (DebugTrace) System.Console.Error.WriteLine(
                    $"EEDOK h={rec.Handle:X} t={rec.TypeCode} EED [{eedStart},{eedEnd}) → TU 化 {probe.BitPosition / 8}B/item 位");
            }
        }
        bool dsAtAnchor = false;
        if (commonHeaderBit > pos && commonHeaderBit <= to && (insertXdicMissing || insertHasDsData))
        {
            CopyBits(body, src, pos, (int)(commonHeaderBit - pos));
            if (insertXdicMissing) body.WriteB(xdicMissingValue);
            bool srcXdicWritten = false;
            if (insertHasDsData && !insertXdicMissing && rec.SourceVersion.IsR2004Plus())
            {
                body.WriteB(overwriteXdicAtAnchor
                    ? xdicMissingValue : ReadSrcBit(src, commonHeaderBit));
                srcXdicWritten = true;
            }
            if (insertHasDsData) { body.WriteB(false); dsAtAnchor = true; }
            pos = srcXdicWritten ? commonHeaderBit + 1 : commonHeaderBit;
        }
        if (overwriteXdicAtAnchor && commonHeaderBit > pos && commonHeaderBit <= to)
        {
            CopyBits(body, src, pos, (int)(commonHeaderBit - pos));
            body.WriteB(xdicMissingValue);
            pos = commonHeaderBit + 1;
        }
        var vals = new List<string>(rec.StringFields.Count);
        bool nameDone = false;
        foreach (var f in rec.StringFields)
        {
            if (f.Start < pos || f.Start + f.BitLen > to) continue;
            CopyBits(body, src, pos, (int)(f.Start - pos));
            if (!nameDone)
            {
                nameDone = true;
                if (t.R2013Plus && rec.SourceVersion < DwgVersion.AC1027 && !dsAtAnchor)
                    body.WriteB(false);
                if (TableXrefShrinkApplies(rec, t)
                    && TryReadTableXrefGroup(src, f.Start + f.BitLen, to, out short xrefResolved, out int xrefGroupBits))
                {
                    body.WriteBS(xrefResolved);
                    pos = f.Start + f.BitLen + xrefGroupBits;
                    vals.Add(f.Value);
                    continue;
                }
            }
            vals.Add(f.Value);
            pos = f.Start + f.BitLen;
        }
        CopyCentralizedTail(body, src, pos, to, rec, t);

        if (rec.LogicalType == TypeBlockHeader && t.R2007Plus)
        {
            vals.Add("");
            vals.Add("");
        }
        if (rec.LogicalType == TypeMaterial && ObjectFactory.CreateMaterialCarry(rec, t) is { } matPlan
            && matPlan.IsCaptured(rec))
            matPlan.WriteUpgradeTail(body, rec, t);
        WriteVportR2007aGrid(body, rec, t);
        long contentStart = body.BitPosition;
        foreach (var v in vals) WriteCentralTu(body, v);
        int contentBits = (int)(body.BitPosition - contentStart);
        if (contentBits > 0)
        {
            WriteStringStreamSize(body, (uint)contentBits);
            body.WriteB(true);
        }
        if (DebugTrace)
            System.Console.Error.WriteLine(
                $"STRUP h={rec.Handle:X} t={rec.TypeCode} {rec.SourceVersion}→{t.Version} " +
                $"挖 {vals.Count} 串 → 集中流 {contentBits} 位（数据区 [{from},{to})）");
    }

    private static bool TableXrefShrinkApplies(DwgObjectRecord rec, Target t)
        => t.R2007Plus
           && rec.SourceVersion >= DwgVersion.AC1015 && rec.SourceVersion < DwgVersion.AC1021
           && (rec.LogicalType == TypeAppid || rec.LogicalType == TypeLayer || rec.LogicalType == TypeBlockHeader
               || rec.LogicalType == TypeDimStyle);

    private static bool TryReadTableXrefGroup(byte[] src, long start, long to,
        out short resolved, out int bits)
    {
        resolved = 0; bits = 0;
        if (start < 0 || start + 4 > to || start + 4 > (long)src.Length * 8) return false;
        var br = new BitReader(src, 0);
        br.SetPosition((int)(start >> 3), (int)(start & 7));
        if (!br.ReadB()) return false;
        short r = br.ReadBS();
        br.ReadB();
        int n = (int)(br.BitPosition - start);
        if (n <= 0 || start + n > to) return false;
        resolved = r; bits = n;
        return true;
    }
}
