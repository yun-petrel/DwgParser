using DwgParser.Core.IO;

namespace DwgParser.Core.Objects;

public static partial class DwgObjectStreamWriter
{
    private static (BitWriter handles, int handlesBitLen) BuildHandleStream(
        DwgObjectRecord rec, byte[] src, Target t, BitWriter? mlsHandles, bool reencode,
        long cfDataEnd, long cfXdicBit, long cfEnd, int cfXdicOp, long derivedHsBit,
        out bool handlesSplitFromSource)
    {
        handlesSplitFromSource = false;
        var handles = new BitWriter(16);
        int handlesBitLen = -1;

        if (rec.LogicalType == TypeBlockHeader
            && rec.SourceVersion >= DwgVersion.AC1018
            && rec.BlockOwnedHandles is not null
            && rec.BlockOwnedHandles.Count != rec.BlockOwnedSourceCount
            && rec.BodyEnd > rec.BodyStart
            && TryBlockHeaderHandleStreamStart(rec, src, out long bhHs)
            && bhHs >= rec.CarryStartBit && bhHs < (long)rec.BodyEnd * 8)
        {
            var bhBuf = new BitWriter(16);
            if (TryRewriteBlockHeaderOwnedHandles(bhBuf, src, rec, bhHs, (long)rec.BodyEnd * 8,
                    rec.BlockOwnedSourceCount))
            {
                Append(handles, bhBuf);
                handlesBitLen = (int)handles.BitPosition;
                handlesSplitFromSource = true;
                if (DebugTrace) System.Console.Error.WriteLine(
                    $"BHOWN60 h={rec.Handle:X} src={rec.SourceVersion} → {t.Version} " +
                    $"owned={rec.BlockOwnedHandles!.Count} hs={handlesBitLen}位 bhHs={bhHs}");
                return (handles, handlesBitLen);
            }
        }

        if (mlsHandles != null)
        {
            handles = mlsHandles;
        }
        else if (reencode)
        {
            WriteEntityHandleStream(handles, rec, t);
            if (!(rec.LogicalType == TypeMText && StripsMTextAnnotative(rec, t))
                && !DropsMTextAppidTail(rec, t)
                && !CarriesTableR2010Up(rec, t)
                && !CarriesTableR2010Down(rec, t)
                && !DictionaryCarry.CarriesDictDropDroppedEntriesDown(rec, t))
                CarryUnmodelledHandleTail(handles, src, rec);
        }
        else if (cfDataEnd >= 0
            && !(rec.LogicalType == TypeVport && rec.Vport is { Captured: true }
                && rec.SourceVersion == DwgVersion.AC1015 && t.Version == DwgVersion.AC1021))
        {
            handlesBitLen = BuildXdicSlotHandleStream(handles, rec, src, t,
                cfDataEnd, cfXdicBit, cfEnd, cfXdicOp);
            if (CarriesRenderSettingsTvToAc1015(rec, t))
                handlesBitLen = AppendRenderSettingsXdicSlotAc1015(handles, handlesBitLen, rec);
        }
        else
        {
            long hsStartBit = rec.HandleStreamStartBit;
            long dataEndBit = (long)rec.BodyEnd * 8;
            long hsLenBits = dataEndBit - hsStartBit;
            if (rec.LogicalType == TypeTableStyle && rec.TableStyle is { ValuesCommitted: true }
                && rec.TableStyle is { Captured: true } or { DownReady: true }
                && (t.Version == DwgVersion.AC1021 || t.Version == DwgVersion.AC1018
                    || t.Version == DwgVersion.AC1015)
                && rec.SourceVersion >= DwgVersion.AC1024
                && hsStartBit >= rec.CarryStartBit && hsStartBit < dataEndBit
                && rec.TableStyle!.HsCommonEndBit > hsStartBit
                && rec.TableStyle!.HsCommonEndBit < dataEndBit)
            {
                CopyBits(handles, src, hsStartBit,
                    (int)(rec.TableStyle!.HsCommonEndBit - hsStartBit));
                handles.WriteHandleRef(5, rec.TableStyle!.Tsty[1]);
                handles.WriteHandleRef(5, rec.TableStyle!.Tsty[2]);
                handles.WriteHandleRef(5, rec.TableStyle!.Tsty[0]);
                handlesBitLen = (int)handles.BitPosition;
            }
            else if (rec.LogicalType == TypeTableStyle && rec.TableStyle is { Captured: true } && t.R2010Plus
                && hsStartBit >= rec.CarryStartBit && hsStartBit < dataEndBit
                && rec.TableStyle!.HsCommonEndBit > hsStartBit
                && rec.TableStyle!.HsCommonEndBit < dataEndBit)
            {
                CopyBits(handles, src, hsStartBit,
                    (int)(rec.TableStyle!.HsCommonEndBit - hsStartBit));
                handles.WriteRC(0x30);
                WriteTableStyleTstyBlock(handles, 0);
                WriteTableStyleTstyBlock(handles, rec.TableStyle!.Tsty[1]);
                WriteTableStyleTstyBlock(handles, rec.TableStyle!.Tsty[2]);
                WriteTableStyleTstyBlock(handles, rec.TableStyle!.Tsty[0]);
                handlesBitLen = (int)handles.BitPosition;
            }
            else if (rec.LogicalType == TypeMLeaderStyle
                && (rec.MLeader is { Captured: true } || rec.MLeader is { UpCaptured: true }
                    || rec.MLeader is { MLeaderStyleR2000ToR2010: true })
                && (rec.SourceVersion == DwgVersion.AC1021 || rec.SourceVersion == DwgVersion.AC1018
                    || rec.SourceVersion == DwgVersion.AC1015)
                && t.R2010Plus
                && hsStartBit >= rec.CarryStartBit && hsStartBit < dataEndBit
                && hsLenBits > 0 && hsLenBits <= rec.CarryBitLen)
            {
                var hbr = new BitReader(src);
                long hp = hsStartBit;
                while (hp + 8 <= dataEndBit)
                {
                    DwgObjectRecord.SeekTo(hbr, hp);
                    int cc = hbr.ReadRC();
                    int bits = 8 + (cc & 0xF) * 8;
                    if (hp + bits > dataEndBit) break;
                    int code = cc >> 4;
                    if (code != 3 && code != 0)
                        CopyBits(handles, src, hp, bits);
                    hp += bits;
                }
                handlesBitLen = (int)handles.BitPosition;
                handlesSplitFromSource = true;
            }
            else if (rec.LogicalType == TypeStyle && rec.Style is { HasAnchors: true }
                && rec.SourceVersion == DwgVersion.AC1015 && t.Version == DwgVersion.AC1021
                && hsStartBit >= rec.CarryStartBit && hsStartBit < dataEndBit)
            {
                var hbr = new BitReader(src);
                long hp = hsStartBit;
                while (hp + 8 <= dataEndBit)
                {
                    DwgObjectRecord.SeekTo(hbr, hp);
                    int cc = hbr.ReadRC();
                    int bits = 8 + (cc & 0xF) * 8;
                    if (hp + bits > dataEndBit) break;
                    bool strip = (cc >> 4) == 3;
                    if (strip)
                    {
                        for (int i = 8; i < bits && strip; i++)
                            if (ReadSrcBit(src, hp + i)) strip = false;
                    }
                    if (!strip)
                        CopyBits(handles, src, hp, bits);
                    hp += bits;
                }
                handlesBitLen = (int)handles.BitPosition;
                handlesSplitFromSource = true;
            }
            else if (rec.LogicalType == TypeVport && rec.Vport is { Captured: true }
                && rec.SourceVersion is DwgVersion.AC1015 or DwgVersion.AC1018
                && t.Version >= DwgVersion.AC1021
                && (rec.SourceVersion == DwgVersion.AC1015 || rec.Vport.VisualStyleHandle != 0)
                && hsStartBit >= rec.CarryStartBit && hsStartBit < dataEndBit
                && hsLenBits > 0 && hsLenBits <= rec.CarryBitLen)
            {
                var vpc = rec.Vport!;
                var hbr = new BitReader(src);
                long hp = hsStartBit;
                int seen = 0;
                while (hp + 8 <= dataEndBit)
                {
                    DwgObjectRecord.SeekTo(hbr, hp);
                    int cc = hbr.ReadRC();
                    int bits = 8 + (cc & 0xF) * 8;
                    if (hp + bits > dataEndBit) break;
                    bool strip = rec.SourceVersion == DwgVersion.AC1015 && (cc >> 4) == 3;
                    if (strip)
                    {
                        for (int i = 8; i < bits && strip; i++)
                            if (ReadSrcBit(src, hp + i)) strip = false;
                    }
                    if (!strip)
                    {
                        if (seen == 2)
                        {
                            handles.WriteHandleRef(4, 0);
                            handles.WriteHandleRef(5, vpc.VisualStyleHandle);
                            handles.WriteHandleRef(3, 0);
                        }
                        CopyBits(handles, src, hp, bits);
                        seen++;
                    }
                    hp += bits;
                }
                if (rec.SourceVersion == DwgVersion.AC1018 && hp < dataEndBit)
                    CopyBits(handles, src, hp, (int)(dataEndBit - hp));
                handlesBitLen = (int)handles.BitPosition;
                handlesSplitFromSource = true;
                if (DebugTrace) System.Console.Error.WriteLine(
                    $"VPHS h={rec.Handle:X} src={seen}引用{rec.SourceVersion} → +3 R_2007a 槽 (visualstyle={vpc.VisualStyleHandle:X}) hs={handlesBitLen}位");
            }
            else if (LayerR2007MaterialSlot(rec, src, t)
                && TryRewriteLayerR2007Handles(handles, src, rec, hsStartBit, dataEndBit))
            {
                handlesBitLen = (int)handles.BitPosition;
                handlesSplitFromSource = true;
                if (t.R2013Plus && rec.SourceVersion < DwgVersion.AC1027)
                {
                    handles.WriteRC(0x50);
                    handlesBitLen += 8;
                }
                if (DebugTrace) System.Console.Error.WriteLine(
                    $"LAYHS h={rec.Handle:X} 剥 (3,xdic) + 末枚引用前插 (5,{rec.Layer!.DefaultMaterialHandle:X}) hs={handlesBitLen}位");
            }
            else if (rec.HasHandleStream && hsStartBit >= rec.CarryStartBit && hsStartBit < dataEndBit
                && hsLenBits > 0 && hsLenBits <= rec.CarryBitLen)
            {
                long copyLen = hsLenBits;
                bool hsRewritten = false;
                if (rec.SourceVersion != t.Version && hsLenBits > 8
                    && (rec.SourceVersion >= DwgVersion.AC1024
                        || (rec.SourceVersion <= DwgVersion.AC1018
                            && t.Version >= DwgVersion.AC1021)
                        || (rec.SourceVersion == DwgVersion.AC1021 && t.Version != DwgVersion.AC1021)))
                {
                    long meaningful = LastMeaningfulHandleEnd(src, hsStartBit, dataEndBit);
                    if (meaningful > hsStartBit && meaningful < dataEndBit)
                        copyLen = meaningful - hsStartBit;
                }
                else if (rec.SourceVersion != t.Version && rec.LogicalType == TypeLType
                         && rec.LType is { SaStartBit: > 0, NumDashes: 0 } && hsLenBits > 8)
                {
                    long meaningful = LastMeaningfulHandleEnd(src, hsStartBit, dataEndBit);
                    if (meaningful > hsStartBit && meaningful < dataEndBit)
                        copyLen = meaningful - hsStartBit;
                }
                if (rec.SourceVersion < DwgVersion.AC1018 && t.Version >= DwgVersion.AC1018)
                {
                    if (System.Environment.GetEnvironmentVariable("DWGDBG") == "1")
                        System.Console.Error.WriteLine($"XDSTRIP h={rec.Handle:X} t={rec.TypeCode} hs={hsStartBit} len={hsLenBits} src={rec.SourceVersion} tgt={t.Version}");
                    bool layMat = (t.Version == DwgVersion.AC1021 || t.R2010Plus)
                        && rec.SourceVersion == DwgVersion.AC1015
                        && rec.LogicalType == TypeLayer && rec.Layer?.DefaultMaterialHandle > 0;
                    long lastRefStart = -1;
                    if (layMat)
                    {
                        var w2 = new BitReader(src);
                        for (long q = hsStartBit; ; )
                        {
                            if (q + 8 > dataEndBit) break;
                            DwgObjectRecord.SeekTo(w2, q);
                            int c2 = w2.ReadRC();
                            int b2 = 8 + (c2 & 0xF) * 8;
                            if (q + b2 > dataEndBit) break;
                            if ((c2 >> 4) != 0) lastRefStart = q;
                            q += b2;
                        }
                        if (lastRefStart < 0) layMat = false;
                    }
                    var hbr = new BitReader(src);
                    long hp = hsStartBit;
                    bool bhFold = rec.LogicalType == TypeBlockHeader
                        && (t.R2007Plus || t.Version == DwgVersion.AC1018)
                        && rec.SourceVersion < DwgVersion.AC1018
                        && rec.BlockOwnedHandles != null;
                    int bhPrevCode = -1;
                    bool bhFolded = false, bhSkipLast = false;
                    while (hp + 8 <= dataEndBit)
                    {
                        DwgObjectRecord.SeekTo(hbr, hp);
                        int cc = hbr.ReadRC();
                        int bits = 8 + (cc & 0xF) * 8;
                        if (hp + bits > dataEndBit) break;
                        int code = cc >> 4;
                        if (bhFold && !bhFolded && code == 4 && bhPrevCode == 3)
                        {
                            foreach (var oh in rec.BlockOwnedHandles!)
                                handles.WriteHandleRef(3, oh);
                            bhFolded = true;
                            bhSkipLast = true;
                            bhPrevCode = code;
                            hp += bits;
                            continue;
                        }
                        if (bhFold && bhSkipLast && code == 4)
                        {
                            bhSkipLast = false;
                            bhPrevCode = code;
                            hp += bits;
                            continue;
                        }
                        bhPrevCode = code;
                        bool strip = (cc >> 4) == 3;
                        if (strip && !layMat)
                        {
                            for (int i = 8; i < bits && strip; i++)
                                if (ReadSrcBit(src, hp + i)) strip = false;
                        }
                        if (!strip)
                        {
                            if (layMat && hp == lastRefStart)
                                handles.WriteHandleRef(5, rec.Layer!.DefaultMaterialHandle);
                            CopyBits(handles, src, hp, bits);
                        }
                        hp += bits;
                    }
                    handlesBitLen = (int)handles.BitPosition;
                    handlesSplitFromSource = true;
                    hsRewritten = true;
                }
                else if (t.Version == DwgVersion.AC1018 && rec.SourceVersion >= DwgVersion.AC1021
                         && rec.LogicalType == TypeLayer
                         && CopyLayerHandlesDownToAc1018(handles, src, hsStartBit, dataEndBit, rec))
                {
                    handlesBitLen = (int)handles.BitPosition;
                    handlesSplitFromSource = true;
                }
                else if ((t.Version == DwgVersion.AC1021 || t.Version == DwgVersion.AC1024)
                         && rec.SourceVersion >= DwgVersion.AC1027
                         && rec.LogicalType == TypeLayer
                         && CopyLayerHandlesDownToAc1018(handles, src, hsStartBit, dataEndBit, rec,
                                dropMaterial: false))
                {
                    handlesBitLen = (int)handles.BitPosition;
                    handlesSplitFromSource = true;
                    if (DebugTrace) System.Console.Error.WriteLine(
                        $"LAYVISDN h={rec.Handle:X} {rec.SourceVersion}→{t.Version} hs={handlesBitLen}位 (剥 visualstyle 末枚 code5)");
                }
                else if (t.Version == DwgVersion.AC1018 && rec.SourceVersion.IsR2010Plus()
                         && rec.LogicalType == TypeVport
                         && CopyVportHandlesDownToAc1018(handles, src, hsStartBit, dataEndBit, rec))
                {
                    handlesBitLen = (int)handles.BitPosition;
                    handlesSplitFromSource = true;
                }
                else if (t.Version == DwgVersion.AC1018 && rec.SourceVersion >= DwgVersion.AC1021
                         && rec.LogicalType == TypeDimStyle
                         && CopyDimStyleHandlesDownToAc1018(handles, src, hsStartBit, dataEndBit, rec))
                {
                    handlesBitLen = (int)handles.BitPosition;
                    handlesSplitFromSource = true;
                }
                else if (t.Version == DwgVersion.AC1018 && rec.SourceVersion >= DwgVersion.AC1032
                         && rec.LogicalType == TypeMLineStyle
                         && RebuildMLineStyleHandlesAc1032ToAc1018(handles, src, hsStartBit, dataEndBit, rec))
                {
                    handlesBitLen = (int)handles.BitPosition;
                    handlesSplitFromSource = true;
                }
                else if (CarriesPreR2010XdicSlotAc1015(rec, t)
                         && CopyPreR2010HandlesWithXdicSlot(handles, src, hsStartBit, dataEndBit,
                             rec, strictHeadShape: false))
                {
                    handlesBitLen = (int)handles.BitPosition;
                    handlesSplitFromSource = true;
                }
                else if (CarriesProxyHandlesAc1015(rec, t)
                         && CopyProxyHandlesAc1015(handles, src, hsStartBit, dataEndBit, rec))
                {
                    handlesBitLen = (int)handles.BitPosition;
                    handlesSplitFromSource = true;
                }
                else if (LayoutR2007ShadeplotSlot(rec, t)
                    && TryInsertLayoutR2007Shadeplot(handles, src, hsStartBit, copyLen))
                {
                    handlesBitLen = (int)handles.BitPosition;
                    if (DebugTrace) System.Console.Error.WriteLine(
                        $"LAY82HS h={rec.Handle:X} 源{rec.SourceVersion} → 第 3 枚后插 (4,0) shadeplot 槽 hs={handlesBitLen}位");
                }
                else if (LayoutR2007ShadeplotSlotDowngrade(rec, t)
                    && TryRemoveLayoutR2007Shadeplot(handles, src, hsStartBit, copyLen))
                {
                    handlesBitLen = (int)handles.BitPosition;
                    if (DebugTrace) System.Console.Error.WriteLine(
                        $"LAY82HSD h={rec.Handle:X} 源{rec.SourceVersion} → 剥 (4,0) shadeplot 槽 hs={handlesBitLen}位");
                }
                else if (NeedsRelativeHandleNormalization(rec, t)
                    && TryNormalizeRelativeHandleRefs(handles, src, hsStartBit, hsStartBit + copyLen, rec.Handle))
                {
                    handlesBitLen = (int)handles.BitPosition;
                    if (DebugTrace) System.Console.Error.WriteLine(
                        $"RELREF h={rec.Handle:X} t={rec.TypeCode} {rec.SourceVersion}→{t.Version} " +
                        $"相对句柄引用已归一为绝对码 hs={handlesBitLen}位（源 {copyLen}位）");
                }
                else
                    CopyBits(handles, src, hsStartBit, (int)copyLen);
                int? dvPatched = DictionaryCarry.PatchDictVarNullTailAc1015(handles, rec, t);
                handlesBitLen = dvPatched ?? (handlesBitLen >= 0 ? handlesBitLen : (int)copyLen);
                handlesSplitFromSource = true;
                if (rec.LogicalType == TypeLayer && t.R2013Plus
                    && rec.SourceVersion < DwgVersion.AC1027)
                {
                    var hbr = new BitReader(src);
                    long hp = hsStartBit;
                    long refsEnd = hp;
                    while (hp + 8 <= dataEndBit)
                    {
                        DwgObjectRecord.SeekTo(hbr, hp);
                        int cc = hbr.ReadRC();
                        int bits = 8 + (cc & 0xF) * 8;
                        if (hp + bits > dataEndBit) break;
                        hp += bits;
                        refsEnd = hp;
                    }
                    long drop = hsRewritten ? 0 : handlesBitLen - (refsEnd - hsStartBit);
                    if (drop > 0) { handles.TrimLastBits(drop); handlesBitLen -= (int)drop; }
                    handles.WriteRC(0x50);
                    handlesBitLen += 8;
                }
                if (rec.LogicalType == TypeDimStyle
                    && (t.Version == DwgVersion.AC1021 || t.R2010Plus)
                    && rec.SourceVersion < DwgVersion.AC1021)
                {
                    var hbr = new BitReader(src);
                    long hp = hsStartBit;
                    long refsEnd = hp;
                    while (hp + 8 <= dataEndBit)
                    {
                        DwgObjectRecord.SeekTo(hbr, hp);
                        int cc = hbr.ReadRC();
                        int bits = 8 + (cc & 0xF) * 8;
                        if (hp + bits > dataEndBit) break;
                        hp += bits;
                        refsEnd = hp;
                    }
                    long drop = handlesBitLen - (refsEnd - hsStartBit);
                    if (drop > 0) { handles.TrimLastBits(drop); handlesBitLen -= (int)drop; }
                    handles.WriteHandleRef(5, 0);
                    handles.WriteHandleRef(5, 0);
                    handles.WriteHandleRef(5, 0);
                    handlesBitLen += 24;
                    if (DebugTrace) System.Console.Error.WriteLine(
                        $"DIMSLOT h={rec.Handle:X} {rec.SourceVersion}→AC1021 hs={handlesBitLen}位 (+3×[5]0 R_2007a 槽)");
                }
            }
            else if (derivedHsBit > 0)
            {
                long hsEnd = (long)rec.BodyEnd * 8;
                int n = (int)(hsEnd - derivedHsBit);
                if (n > 8)
                {
                    long meaningful = LastMeaningfulHandleEnd(src, derivedHsBit, hsEnd);
                    if (meaningful > derivedHsBit && meaningful < hsEnd)
                        n = (int)(meaningful - derivedHsBit);
                }
                if (System.Environment.GetEnvironmentVariable("DWGDBG") == "1")
                    System.Console.Error.WriteLine($"DERIVEDHS h={rec.Handle:X} t={rec.TypeCode} derivedHsBit={derivedHsBit} hsEnd={hsEnd} n={n}");
                if (CarriesDataLinkHandlesUp(rec, t)
                    && RebuildDataLinkHandlesUp(handles, src, derivedHsBit, hsEnd, rec))
                {
                    handlesBitLen = (int)handles.BitPosition;
                    handlesSplitFromSource = true;
                }
                else if (rec.SourceVersion < DwgVersion.AC1018 && t.Version >= DwgVersion.AC1018)
                {
                    bool lay82 = (t.Version == DwgVersion.AC1021 || t.R2010Plus)
                        && rec.SourceVersion == DwgVersion.AC1015
                        && rec.LogicalType == TypeLayout && rec.Layout is { Captured: true };
                    int layNr = 0, laySeen = 0;
                    if (lay82)
                    {
                        var nbr = new BitReader(src);
                        DwgObjectRecord.SeekTo(nbr, rec.CarryStartBit + 32);
                        nbr.ReadHandle();
                        if (!DwgObjectReader.TrySkipEed(nbr)) lay82 = false;
                        else
                        {
                            int n2 = nbr.ReadBL();
                            if (n2 is < 0 or > 8) lay82 = false;
                            else layNr = n2;
                        }
                    }
                    bool bhFold = rec.LogicalType == TypeBlockHeader
                        && (t.R2007Plus || t.Version == DwgVersion.AC1018)
                        && rec.BlockOwnedHandles != null;
                    int bhPrevCode = -1;
                    bool bhFolded = false, bhSkipLast = false;
                    var hbr = new BitReader(src);
                    long hp = derivedHsBit;
                    while (hp + 8 <= hsEnd)
                    {
                        DwgObjectRecord.SeekTo(hbr, hp);
                        int cc = hbr.ReadRC();
                        int bits = 8 + (cc & 0xF) * 8;
                        if (hp + bits > hsEnd) break;
                        int code = cc >> 4;
                        if (bhFold && !bhFolded && code == 4 && bhPrevCode == 3)
                        {
                            foreach (var oh in rec.BlockOwnedHandles!)
                                handles.WriteHandleRef(3, oh);
                            bhFolded = true;
                            bhSkipLast = true;
                            bhPrevCode = code;
                            hp += bits;
                            continue;
                        }
                        if (bhFold && bhSkipLast && code == 4)
                        {
                            bhSkipLast = false;
                            bhPrevCode = code;
                            hp += bits;
                            continue;
                        }
                        bhPrevCode = code;
                        bool strip = (cc >> 4) == 3;
                        if (strip)
                        {
                            for (int i = 8; i < bits && strip; i++)
                                if (ReadSrcBit(src, hp + i)) strip = false;
                        }
                        if (!strip)
                        {
                            if (lay82 && laySeen == 1 + layNr)
                            {
                                handles.WriteHandleRef(5, 0);
                                handles.WriteHandleRef(4, 0);
                            }
                            CopyBits(handles, src, hp, bits);
                            laySeen++;
                        }
                        hp += bits;
                    }
                    handlesBitLen = (int)handles.BitPosition;
                }
                else
                {
                    CopyBits(handles, src, derivedHsBit, n);
                    handlesBitLen = n;
                }
            }
            else
            {
                handlesBitLen = 0;
            }
        }
        return (handles, handlesBitLen);
    }
}
