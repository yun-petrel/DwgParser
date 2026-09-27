using DwgParser.Core.IO;

namespace DwgParser.Core.Objects;

public static partial class DwgObjectStreamWriter
{
    private static bool WriteObject(BitWriter main, DwgObjectRecord rec, byte[] src, Target t,
        Dictionary<int, short>? classTypeMap = null,
        LtypeTable? ltypeTable = null,
        DwgObjectReader.WalkResult? walk = null)
    {
        if (IsSynthetic(rec))
        {
            if (!WriteSyntheticFrame(main, rec, t)) return false;
            return true;
        }

        short srcTypeCode = rec.TypeCode;
        short outTypeCode = srcTypeCode;
        if (classTypeMap != null
            && classTypeMap.TryGetValue(srcTypeCode - 500, out short mapped) && mapped > 0)
            outTypeCode = mapped;
        if (SynthesizesRenderSettingsProxy(rec, t)) outTypeCode = TypeProxyObj;

        var typeW = new BitWriter(8);
        if (t.R2010Plus) typeW.WriteBOT(outTypeCode);
        else typeW.WriteBS(outTypeCode);
        int typeBits = (int)typeW.BitPosition;

        int srcTypeBits = 0, typeBitsDelta = 0;
        if (outTypeCode != srcTypeCode)
        {
            var srcTypeW = new BitWriter(8);
            if (rec.SourceVersion.IsR2010Plus()) srcTypeW.WriteBOT(srcTypeCode);
            else srcTypeW.WriteBS(srcTypeCode);
            srcTypeBits = (int)srcTypeW.BitPosition;
            typeBitsDelta = typeBits - srcTypeBits;
            if (DebugTrace)
                System.Console.Error.WriteLine(
                    $"TYPE_MAP h={rec.Handle:X} {srcTypeCode}→{outTypeCode} " +
                    $"typeBits {srcTypeBits}→{typeBits} (Δ{typeBitsDelta})");
        }

        var body = new BitWriter(64);
        bool bodyCarriesRl = false;
        long cfDataEnd = -1;
        long cfEnd = -1;
        long cfXdicBit = -1;
        int cfXdicOp = 0;
        long derivedHsBit = -1;
        bool handlesSplitFromSource = false;

        bool reencode = !SameObjectFrameFormat(rec.SourceVersion, t.Version)
                        && CanReencode(rec, t.Version);

        BitWriter? mlsHandles = null;
        bool ac1015StrCapped = false;
        var (mlsPlan, vsPlan, styAc15Plan, tsAc15Plan, ltAc15Plan, lyAc15Plan, xrAc15Plan,
            dsAc15Plan) = FrameCarryPlansOf(rec, t, ltypeTable);

        if (reencode)
        {
            WriteReencodedBody(body, rec, src, t);
        }
        else if ((rec.LogicalType == TypeBlockControl || rec.LogicalType == TypeLtypeControl)
                 && rec.SourceVersion >= DwgVersion.AC1021 && t.R2010Plus
                 && rec.SourceVersion != DwgVersion.AC1027 && rec.SourceVersion != t.Version
                 && TryCarryControlDropNullEntries(body, rec, src, t, out mlsHandles))
        {
            if (DebugTrace)
                System.Console.Error.WriteLine(
                    $"CTRLDROP h={rec.Handle:X} t={rec.TypeCode} {rec.SourceVersion}→{t.Version}");
        }
        else if (rec.LogicalType == TypeDimStyleControl
                 && rec.SourceVersion >= DwgVersion.AC1021 && t.R2010Plus
                 && rec.SourceVersion != DwgVersion.AC1027 && rec.SourceVersion != t.Version
                 && TryCarryDimStyleControlDowngrade(body, rec, src, t, out mlsHandles))
        {
            if (DebugTrace)
                System.Console.Error.WriteLine(
                    $"DSCDROP h={rec.Handle:X} {rec.SourceVersion}→{t.Version}");
        }
        else if (vsPlan is { } vsP && vsP.IsCaptured(rec)
                 && vsP.TryCarry(body, rec, src, t, out mlsHandles))
        {
        }
        else if (mlsPlan is { } mlsP && mlsP.IsCaptured(rec)
                 && mlsP.TryCarry(body, rec, src, ltypeTable!, t, out mlsHandles))
        {
        }
        else if (CarriesStyleR2007Down(rec, t) && CarryStyleR2007Down(body, rec, src, t))
        {
        }
        else if (CarriesProxyR2007Down(rec, t) && CarryProxyR2007Down(body, rec, src, t))
        {
        }
        else if (CarriesProxyR2007ToAc1015Down(rec, t) && CarryProxyR2007ToAc1015Down(body, rec, src, t))
        {
        }
        else if (CarriesCellStyleMapR2007ToAc1015Down(rec, t)
                 && CarryCellStyleMapR2007ToAc1015Down(body, rec, src, out var csm15Handles))
        {
            mlsHandles = csm15Handles;
        }
        else if (CarriesCellStyleMapR2007Down(rec, t) && CarryCellStyleMapR2007Down(body, rec, src, t))
        {
        }
        else if (CarriesMLineStyleAc1018ToAc1015(rec, t)
                 && CarryMLineStyleAc1018ToAc1015(body, rec, src, out var mls18bHandles))
        {
            mlsHandles = mls18bHandles;
        }
        else if (CarriesLayerR2007ToAc1015(rec, t)
                 && CarryLayerR2007ToAc1015(body, rec, src, out var layR15Handles))
        {
            mlsHandles = layR15Handles;
        }
        else if (CarriesLayerAc1018ToAc1015(rec, t)
                 && CarryLayerAc1018ToAc1015(body, rec, src, out var lay18Handles))
        {
            mlsHandles = lay18Handles;
        }
        else if (CarriesMLeaderStyleAc1018ToAc1015(rec, t)
                 && CarryMLeaderStyleAc1018ToAc1015(body, rec, src, out var mls18Handles))
        {
            mlsHandles = mls18Handles;
        }
        else if (CarriesMLeaderStyleR2010ToAc1015(rec, t)
                 && CarryMLeaderStyleR2010ToAc1015(body, rec, src, t, out var mls15Handles))
        {
            mlsHandles = mls15Handles;
        }
        else if (CarriesVisualStyleR2010ToAc1015(rec, t)
                 && CarryVisualStyleR2010ToAc1015(body, rec, src, out var vs15Handles))
        {
            mlsHandles = vs15Handles;
        }
        else if (CarriesVisualStyleAc1018ToAc1015(rec, t)
                 && CarryVisualStyleAc1018ToAc1015(body, rec, src, out var vs18Handles))
        {
            mlsHandles = vs18Handles;
        }
        else if (CarriesVisualStyleAc1021ToAc1015(rec, t)
                 && CarryVisualStyleAc1021ToAc1015(body, rec, src, out var vs2115Handles))
        {
            mlsHandles = vs2115Handles;
        }
        else if (CarriesViewStyleAc1018ToAc1015(rec, t)
                 && CarryViewStyleAc1018ToAc1015(body, rec, src, out var vw18Handles))
        {
            mlsHandles = vw18Handles;
        }
        else if (CarriesViewStyleR2010ToAc1015(rec, t)
                 && CarryViewStyleR2010ToAc1015(body, rec, src, out var vw1015Handles))
        {
            mlsHandles = vw1015Handles;
        }
        else if (CarriesDataLinkR2010ToAc1015(rec, t)
                 && CarryDataLinkR2010ToAc1015(body, rec, src, t, out var dl1015Handles))
        {
            mlsHandles = dl1015Handles;
        }
        else if (CarriesBlockHeaderFrameAc1018ToAc1015(rec, t)
                 && CarryBlockHeaderFrameAc1018ToAc1015(body, rec, src, out var bhAc15Handles))
        {
            mlsHandles = bhAc15Handles;
        }
        else if (CarriesBlockHeaderR2010ToAc1015(rec, t)
                 && CarryBlockHeaderR2010ToAc1015(body, rec, src, out var bh1015Handles))
        {
            mlsHandles = bh1015Handles;
        }
        else if (CarriesSunAc1018ToAc1015(rec, t)
                 && CarrySunAc1018ToAc1015(body, rec, src, out var sun15Handles))
        {
            mlsHandles = sun15Handles;
        }
        else if (CarriesLayoutAc1018ToAc1015(rec, t)
                 && CarryLayoutAc1018ToAc1015(body, rec, src, out var ly18Handles))
        {
            mlsHandles = ly18Handles;
        }
        else if (CarriesLtypeR2010ToAc1015(rec, t)
                 && CarryLtypeR2010ToAc1015(body, rec, src, t, out var lt15Handles))
        {
            mlsHandles = lt15Handles;
        }
        else if (CarriesStyleR2007ToAc1015(rec, t)
                 && CarryStyleR2007ToAc1015(body, rec, src, t, out var sty15Handles))
        {
            mlsHandles = sty15Handles;
        }
        else if (CarriesLayoutR2010ToAc1015(rec, t)
                 && CarryLayoutR2010ToAc1015(body, rec, src, out var ly1015Handles))
        {
            mlsHandles = ly1015Handles;
        }
        else if (CarriesLayoutAc1021ToAc1015(rec, t)
                 && CarryLayoutAc1021ToAc1015(body, rec, src, out var ly21Handles))
        {
            mlsHandles = ly21Handles;
        }
        else if (CarriesMLeaderStyleR2010ToAc1018(rec, t)
                 && CarryMLeaderStyleR2010ToAc1018(body, rec, src, t, out var mlHs, walk))
        {
            mlsHandles = mlHs;
        }
        else if (CarriesScaleR2010ToAc1015(rec, t)
                 && CarryScaleR2010ToAc1015(body, rec, src, t, out var sc15Handles))
        {
            mlsHandles = sc15Handles;
        }
                else if (CarriesScaleR2010ToAc1018(rec, t) && CarryScaleR2010ToAc1018(body, rec, src, t))
                {
                }
                else if (CarriesLayoutR2010Down(rec, t) && CarryLayoutR2010Down(body, rec, src, t))
                {
                }
                else if (CarriesLayerR2010Down(rec, t) && CarryLayerR2010Down(body, rec, src, t))
                {
                }
                else if (CarriesLtypeR2010Down(rec, t) && CarryLtypeR2010Down(body, rec, src, t))
                {
                }
                else if (XrecordDowngradesR2007(rec, t)
                         && CarryXrecordDowngrade(body, rec, src, t, out var xrDownHs, out var xrXdicBit))
                {
                    derivedHsBit = xrDownHs;
                    if (xrXdicBit > 0)
                    {
                        cfXdicOp = 1; cfXdicBit = xrXdicBit;
                        cfDataEnd = xrDownHs; cfEnd = (long)rec.BodyEnd * 8;
                    }
                }
                else if (CarriesImageDefAc1015Down(rec, t)
                         && CarryImageDefAc1015Down(body, rec, src, out var idAc15Handles)) mlsHandles = idAc15Handles;
                else if (CarriesImageDefAc1015FromPreR2004(rec, t)
                         && CarryImageDefAc1015FromPreR2004(body, rec, src, out var idAc15PreHandles)) mlsHandles = idAc15PreHandles;
                else if (ImageDefDowngrades(rec, t)
                         && CarryImageDefDowngrade(body, rec, src, out var idDownHs)) derivedHsBit = idDownHs;
                else if (SynthesizesRenderSettingsProxy(rec, t) && CarryRenderSettingsProxyDown(body, rec, src, t)) { }
                else if (CarryAc1018NarrowGroup(rec, t, body, src, ltypeTable, ref mlsHandles)) { }
                else if (CarriesTableStyleR2010ToAc1015(rec, t)
                         && CarryTableStyleR2010ToAc1015(body, rec, src, t, out var ts15Handles)) mlsHandles = ts15Handles;
                else if (CarryAc1018TableDimGroup(rec, t, body, src, ref mlsHandles, ref derivedHsBit)) { }
                else
                {
            bool srcHasRl = PrefixHasRl(rec.SourceVersion);
            long start = rec.CarryStartBit;
            int len = rec.CarryBitLen;
            if (len <= 0) return false;

            if (srcHasRl && t.R2010Plus)
            {
                if (len <= 32) return false;
                start += 32;
                len -= 32;
            }
            else if (!srcHasRl && !t.R2010Plus)
            {
                bodyCarriesRl = false;
            }
            else
            {
                bodyCarriesRl = srcHasRl;
            }

            long trunc = rec.HandleStreamStartBit;
            if (!t.R2007Plus && rec.StringStreamStartBit > 0 && rec.StringStreamStartBit < trunc)
                trunc = rec.StringStreamStartBit;
            if (rec.HasHandleStream && trunc > start && trunc < start + len)
                len = (int)(trunc - start);

            if (!rec.HasHandleStream && PrefixHasRl(rec.SourceVersion)
                && (t.R2010Plus || t.R2007Plus)
                && rec.BodyEnd > rec.BodyStart)
            {
                uint rl = ReadRlAt(src, rec.CarryStartBit);
                long hs = (long)rec.BodyStart * 8 + rl;
                if (hs > start && hs < (long)rec.BodyEnd * 8)
                {
                    derivedHsBit = hs;
                    len = (int)(hs - start);
                }
            }

            int xdicDelta = 0, dsDelta = 0;
            bool srcR4 = rec.SourceVersion.IsR2004Plus(), dstR4 = !t.R2000;
            bool srcR13 = rec.SourceVersion >= DwgVersion.AC1027, dstR13 = t.R2013Plus;
            if (rec.SourceVersion >= DwgVersion.AC1015 && t.Version >= DwgVersion.AC1015)
            {
                if (dstR4 != srcR4) xdicDelta = dstR4 ? 1 : -1;
                if (dstR13 != srcR13) dsDelta = dstR13 ? 1 : -1;
            }
            int commonDelta = xdicDelta + dsDelta;
            bool isObject = rec.Kind != DwgObjectRecordKind.Entity;
            int bitsizeDelta;
            if (isObject)
                bitsizeDelta = commonDelta;
            else if (xdicDelta != 0 && dsDelta != 0)
                bitsizeDelta = xdicDelta > 0 ? 1 : -1;
            else
                bitsizeDelta = dsDelta;
            uint srcRlValue = 0, numReactors = 0;
            long patchBit = -1;
            bool vsDown = rec.LogicalType == TypeVisualStyle && rec.SourceVersion.IsR2010Plus()
                && t.Version == DwgVersion.AC1021 && rec.VisualStyle is { DownCaptured: true };
            bool layUp = LayerR2007MaterialSlot(rec, src, t);
            bool up18 = t.Version == DwgVersion.AC1021 && rec.SourceVersion == DwgVersion.AC1018
                && (rec.LogicalType == 0x4F || rec.LogicalType == 512
                    || rec.LogicalType == TypeSectionViewStyle || rec.LogicalType == TypeDetailViewStyle
                    || rec.LogicalType == 499);
            bool bhDown = isObject && rec.LogicalType == TypeBlockHeader
                && rec.SourceVersion.IsR2010Plus() && !t.R2007Plus
                && rec.StringStreamStartBit > 0;
            if (commonDelta != 0 || (rec.SourceVersion.IsR2004Plus() && t.R2010Plus) || vsDown || layUp || up18 || bhDown)
                patchBit = FindAfterNumReactors(rec, src, PrefixHasRl(rec.SourceVersion),
                                               out srcRlValue, out numReactors);
            bool xdicMissing = false;
            bool splitHandles = false;
            bool hsKnown = rec.HasHandleStream && rec.HandleStreamStartBit > start;
            long hsEnd = rec.SourceVersion.IsR2010Plus() && hsKnown
                ? (long)rec.BodyEnd * 8
                : start + len;
            if (patchBit > start && patchBit + Math.Max(-commonDelta, 0) <= start + len
                && xdicDelta != 0 && (PrefixHasRl(rec.SourceVersion) || hsKnown)
                && (!rec.HasHandleStream || hsKnown))
            {
                long dataEnd = hsKnown
                    ? rec.HandleStreamStartBit
                    : (long)rec.BodyStart * 8 + srcRlValue;
                if (dataEnd > patchBit && dataEnd < hsEnd)
                {
                    long xb = LocateHandleRef(src, dataEnd, hsEnd, (int)numReactors + 2);
                    if (xdicDelta > 0)
                    {
                        if (xb > 0 && RefIsNullXdic(src, xb))
                        {
                            cfXdicOp = -1; cfXdicBit = xb; xdicMissing = true; splitHandles = true;
                        }
                    }
                    else
                    {
                        var br = new BitReader(src, 0);
                        DwgObjectRecord.SeekTo(br, patchBit);
                        if (br.ReadB())
                        {
                            long ins = xb;
                            if (ins <= 0)
                            {
                                var (cnt, refEnd) = WalkMeaningfulHandleRefs(src, dataEnd, hsEnd);
                                if (cnt == (int)numReactors + 1) ins = refEnd;
                            }
                            if (ins > 0) { cfXdicOp = 1; cfXdicBit = ins; splitHandles = true; }
                        }
                    }
                    if (splitHandles) { cfDataEnd = dataEnd; cfEnd = hsEnd; }
                    else { cfXdicOp = 0; cfXdicBit = -1; }
                }
            }
            if (System.Environment.GetEnvironmentVariable("DWGDBG") == "1")
            {
                long dbgDataEnd = (long)rec.BodyStart * 8 + srcRlValue;
                System.Console.Error.WriteLine(
                    $"PATCH h={rec.Handle:X} t={rec.TypeCode} d={commonDelta} bsd={bitsizeDelta} start={start} len={len} " +
                    $"patch={patchBit} rl={srcRlValue} nr={numReactors} carriesRl={bodyCarriesRl} " +
                    $"split={splitHandles} xdicOp={cfXdicOp} xdicBit={cfXdicBit} xm={xdicMissing} " +
                    $"dataEnd={dbgDataEnd} xbLoc={LocateHandleRef(src, dbgDataEnd, start + len, (int)numReactors + 2)} " +
                    $"hsk={hsKnown} HHS={rec.HasHandleStream} HSB={rec.HandleStreamStartBit} BS={rec.BodyStart} BE={rec.BodyEnd} CS={rec.CarryStartBit}");
            }

            long bodyEndBit = splitHandles ? cfDataEnd : start + len;
            if (rec.LogicalType == TypeBlockHeader
                && rec.SourceVersion >= DwgVersion.AC1018
                && rec.BlockOwnedHandles is not null
                && rec.BlockOwnedHandles.Count != rec.BlockOwnedSourceCount
                && TryBlockHeaderHandleStreamStart(rec, src, out long bhHsBody)
                && bhHsBody > start && bhHsBody < bodyEndBit)
            {
                bodyEndBit = bhHsBody;
                len = (int)(bhHsBody - start);
                if (DebugTrace) System.Console.Error.WriteLine(
                    $"BH60BODY h={rec.Handle:X} 搬运窗止于句柄流起点 {bhHsBody}（len→{len}）");
            }
            long srcHsFromRl = (long)rec.BodyStart * 8 + srcRlValue;
            if (rec.SourceVersion >= DwgVersion.AC1021 && !t.R2007Plus
                && bodyEndBit > start
                && (bodyEndBit == rec.HandleStreamStartBit || bodyEndBit == srcHsFromRl))
            {
                bodyEndBit--;
                len--;
            }
            if (DictionaryCarry.TryCapBodyAtCentralStringsAc1015(
                    rec, t, start, ref bodyEndBit, ref len)) ac1015StrCapped = true;
            bool mlsUp = rec.LogicalType == TypeMLeaderStyle && t.R2010Plus
                && rec.SourceVersion == DwgVersion.AC1021 && rec.MLeader?.Captured == true;

            CarryObjectBody(body, rec, src, t, start, len, bodyCarriesRl, commonDelta, xdicDelta,
                dsDelta, bitsizeDelta, srcRlValue, patchBit, xdicMissing, bodyEndBit, mlsUp,
                ltypeTable, srcR4, dstR4, srcR13, dstR13, isObject, layUp, ref mlsHandles,
                typeBitsDelta, derivedHsBit, styAc15Plan, tsAc15Plan, ltAc15Plan, lyAc15Plan,
                xrAc15Plan, dsAc15Plan, ref cfDataEnd, ref cfXdicOp);
        }

        if (DebugTrace && rec.LogicalType == TypeStyle)
            System.Console.Error.WriteLine($"STYX1 h={rec.Handle:X} body={body.BitPosition}");
        DictionaryCarry.AppendDowngradedStrings(body, rec, t, ac1015StrCapped);
        if (rec.LogicalType == TypeBlockHeader
            && rec.SourceVersion >= DwgVersion.AC1018
            && rec.BlockOwnedHandles is not null
            && rec.BlockOwnedSourceCount >= 0
            && rec.BlockOwnedHandles.Count != rec.BlockOwnedSourceCount)
            RewriteBlockHeaderOwnedCountInBody(body, src, rec);

        var (handles, handlesBitLen) = BuildHandleStream(rec, src, t, mlsHandles, reencode,
            cfDataEnd, cfXdicBit, cfEnd, cfXdicOp, derivedHsBit, out handlesSplitFromSource);
        if (DebugTrace && rec.LogicalType == TypeStyle)
            System.Console.Error.WriteLine($"STYX2 h={rec.Handle:X} body={body.BitPosition} hsSplit={handlesSplitFromSource}");

        return FinalizeObjectFrame(main, body, handles, handlesBitLen, typeW, typeBits,
            bodyCarriesRl, rec, src, t, reencode, cfDataEnd, derivedHsBit, handlesSplitFromSource);
    }
}
