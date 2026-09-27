using DwgParser.Core.IO;

namespace DwgParser.Core.Objects;

public static partial class DwgObjectStreamWriter
{
    private static void CarryObjectBody(BitWriter body, DwgObjectRecord rec, byte[] src, Target t,
        long start, int len, bool bodyCarriesRl, int commonDelta, int xdicDelta, int dsDelta,
        int bitsizeDelta, long srcRlValue, long patchBit, bool xdicMissing, long bodyEndBit,
        bool mlsUp, LtypeTable? ltypeTable, bool srcR4, bool dstR4, bool srcR13, bool dstR13,
        bool isObject, bool layUp, ref BitWriter? mlsHandles, int typeBitsDelta,
        long derivedHsBit,
        Records.StyleCarryPreR2007Up? styAc15Plan, Records.TableStyleCarryAc1018ToAc1021? tsAc15Plan,
        Records.LtypeCarryPreR2007Up? ltAc15Plan, Records.LayerCarryPreR2007Up? lyAc15Plan,
        Records.XrecordCarryPreR2007Up? xrAc15Plan, Records.DimStyleCarryAcR2000Up? dsAc15Plan,
        ref long cfDataEnd, ref int cfXdicOp)
    {

        if (patchBit > start && patchBit + Math.Max(-commonDelta, 0) <= bodyEndBit)
        {
            bool sty15Done = false;
            bool r2007HasStringsTail = !t.R2007Plus
                && rec.SourceVersion >= DwgVersion.AC1021
                && rec.Kind != DwgObjectRecordKind.Entity
                && !rec.HasHandleStream && rec.HandleStreamStartBit <= 0
                && PrefixHasRl(rec.SourceVersion)
                && srcRlValue > 0 && srcRlValue <= (long)rec.BodyEnd * 8;
            long r2007TailBit = r2007HasStringsTail
                ? (long)rec.BodyStart * 8 + srcRlValue - 1
                : -1;
            if (System.Environment.GetEnvironmentVariable("DWGDBG") == "1" && ltAc15Plan != null && rec.LogicalType == TypeLType)
                System.Console.Error.WriteLine(
                    $"LTTRY h={rec.Handle:X} captured={ltAc15Plan.IsCaptured(rec)} start={start} carry={rec.CarryStartBit} bodyEnd={bodyEndBit} hs={rec.HandleStreamStartBit} SF={rec.StringFields.Count}");
            if (ObjectFactory.CreateProxyObjectCarry(rec, t) is { } pxp
                && pxp.IsCaptured(rec)
                && pxp.TryCarry(body, src, start, bodyEndBit, rec, t))
            {
                sty15Done = true;
                if (DebugTrace)
                    System.Console.Error.WriteLine(
                        $"PXDONE h={rec.Handle:X} body={body.BitPosition} to={bodyEndBit} from={start}");
            }
            else if (styAc15Plan is { } sty15 && sty15.IsCaptured(rec)
                && sty15.TryCarryFrameStart(body, src, start, bodyEndBit, rec, t))
            {
                sty15Done = true;
                if (DebugTrace)
                    System.Console.Error.WriteLine(
                        $"STYDONE h={rec.Handle:X} body={body.BitPosition} to={bodyEndBit} from={start}");
            }
            else if (rec.LogicalType == TypeStyle && t.R2010Plus
                && rec.SourceVersion >= DwgVersion.AC1015 && rec.SourceVersion < DwgVersion.AC1021
                && ObjectFactory.CreateStyleCarry(rec, t) is Records.StyleCarryPreR2007Up styUp2010
                && styUp2010.IsCaptured(rec)
                && styUp2010.TryCarryR2010FrameStart(body, src, start, bodyEndBit, rec, t))
            {
                sty15Done = true;
                if (DebugTrace)
                    System.Console.Error.WriteLine(
                        $"STYDONE10 h={rec.Handle:X} body={body.BitPosition} to={bodyEndBit} from={start}");
            }
            else if (tsAc15Plan is { } ts15 && ts15.IsCaptured(rec)
                && ts15.TryCarry(body, src, start, bodyEndBit, rec, t))
            {
                sty15Done = true;
                if (DebugTrace)
                    System.Console.Error.WriteLine(
                        $"TSDONE h={rec.Handle:X} body={body.BitPosition} to={bodyEndBit} from={start}");
            }
            else if (ltAc15Plan is { } lt15 && lt15.IsCaptured(rec)
                && lt15.TryCarry(body, src, start, bodyEndBit, rec, t))
            {
                sty15Done = true;
                if (DebugTrace)
                    System.Console.Error.WriteLine(
                        $"LTDONE h={rec.Handle:X} body={body.BitPosition} to={bodyEndBit} from={start}");
            }
            else if (lyAc15Plan is { } ly15 && ly15.IsCaptured(rec)
                && ly15.TryCarryFrameStart(body, src, start, bodyEndBit, rec, t))
            {
                sty15Done = true;
                if (DebugTrace)
                    System.Console.Error.WriteLine(
                        $"LYDONE h={rec.Handle:X} body={body.BitPosition} to={bodyEndBit} from={start}");
            }
            else if (rec.LogicalType == TypeVisualStyle && rec.SourceVersion == DwgVersion.AC1015
                && t.Version == DwgVersion.AC1021
                && new Records.VisualStyleCarryAcR2000ToAc1021()
                    is Records.VisualStyleCarryAcR2000ToAc1021 vs15
                && vs15.TryCarryFrameStart(body, src, start, bodyEndBit, rec, t))
            { sty15Done = true; }
            else if (rec.LogicalType == TypeMLineStyle && rec.SourceVersion == DwgVersion.AC1015
                && t.Version == DwgVersion.AC1021
                && new Records.MLineStyleCarryPreR2007ToAc1021() is { } ml73
                && ml73.TryCarryFrameStart(body, src, start, bodyEndBit, rec, t))
            { sty15Done = true; }
            else if (rec.LogicalType == TypeMLineStyle
                && (rec.SourceVersion == DwgVersion.AC1015
                    || (rec.SourceVersion == DwgVersion.AC1018
                        && t.Version >= DwgVersion.AC1032))
                && t.R2010Plus
                && new Records.MLineStyleCarryAcR2000ToR2010Plus() is { } ml73r10
                && ml73r10.TryCarryFrameStart(body, src, start, bodyEndBit, rec, t,
                    ltypeTable, out mlsHandles))
            {
                sty15Done = true;
                if (DebugTrace)
                    System.Console.Error.WriteLine(
                        $"MLS73DONE10 h={rec.Handle:X} body={body.BitPosition} to={bodyEndBit} from={start}");
            }
            else if (xrAc15Plan is { } xr15
                && xr15.TryCarryFrameStart(body, src, start, bodyEndBit, rec, t))
            {
                sty15Done = true;
                if (DebugTrace)
                    System.Console.Error.WriteLine(
                        $"XRDONE h={rec.Handle:X} body={body.BitPosition} to={bodyEndBit} from={start}");
            }
            else if (rec.LogicalType == TypeMLeaderStyle && rec.SourceVersion == DwgVersion.AC1015
                && t.Version == DwgVersion.AC1021
                && new Records.MLeaderStyleCarryAcR2000ToAc1021()
                    is Records.MLeaderStyleCarryAcR2000ToAc1021 mls15
                && mls15.TryCarryFrameStart(body, src, start, bodyEndBit, rec, t))
            {
                sty15Done = true;
                if (DebugTrace)
                    System.Console.Error.WriteLine(
                        $"MLSDONE h={rec.Handle:X} body={body.BitPosition} to={bodyEndBit} from={start}");
            }
            else if (rec.LogicalType == TypeMLeaderStyle && rec.SourceVersion == DwgVersion.AC1015
                && t.R2010Plus
                && new Records.MLeaderStyleCarryAcR2000ToR2010Plus()
                    is Records.MLeaderStyleCarryAcR2000ToR2010Plus mls15r10
                && mls15r10.TryCarryFrameStart(body, src, start, bodyEndBit, rec, t))
            {
                sty15Done = true;
                if (DebugTrace)
                    System.Console.Error.WriteLine(
                        $"MLSDONE10 h={rec.Handle:X} body={body.BitPosition} to={bodyEndBit} from={start}");
            }
            else if (rec.LogicalType == TypeMLeaderStyle && rec.SourceVersion == DwgVersion.AC1018
                && t.R2010Plus
                && new Records.MLeaderStyleCarryAc1018ToR2010Plus()
                    is Records.MLeaderStyleCarryAc1018ToR2010Plus mls18r10
                && mls18r10.TryCarry(body, src, start, bodyEndBit, rec, t))
            {
                sty15Done = true;
                if (DebugTrace)
                    System.Console.Error.WriteLine(
                        $"MLSDONE18R10 h={rec.Handle:X} body={body.BitPosition} to={bodyEndBit} from={start}");
            }
            else if (rec.LogicalType == TypeVisualStyle && rec.SourceVersion == DwgVersion.AC1018
                && t.R2010Plus
                && new Records.VisualStyleCarryAc1018ToR2010Plus()
                    is Records.VisualStyleCarryAc1018ToR2010Plus vs18r10
                && vs18r10.TryCarry(body, src, start, bodyEndBit, rec, t))
            {
                sty15Done = true;
                if (DebugTrace)
                    System.Console.Error.WriteLine(
                        $"VSDONE18R10 h={rec.Handle:X} body={body.BitPosition} to={bodyEndBit} from={start}");
            }
            else if (rec.LogicalType == TypeVisualStyle && rec.SourceVersion == DwgVersion.AC1015
                && t.R2010Plus
                && new Records.VisualStyleCarryAcR2000ToR2010Plus()
                    is Records.VisualStyleCarryAcR2000ToR2010Plus vs15r10
                && vs15r10.TryCarry(body, src, start, bodyEndBit, rec, t))
            {
                sty15Done = true;
                if (DebugTrace)
                    System.Console.Error.WriteLine(
                        $"VSDONE15R10 h={rec.Handle:X} body={body.BitPosition} to={bodyEndBit} from={start}");
            }
            else if (rec.LogicalType == TypeLayout && rec.SourceVersion == DwgVersion.AC1015
                && (t.Version == DwgVersion.AC1021 || t.R2010Plus)
                && new Records.LayoutCarryPreR2007ToAc1021() is { } lay82
                && lay82.TryCarryFrameStart(body, src, start, bodyEndBit, rec, t))
            {
                sty15Done = true;
                if (DebugTrace)
                    System.Console.Error.WriteLine(
                        $"LAYDONE h={rec.Handle:X} body={body.BitPosition} to={bodyEndBit} from={start}");
            }
            else if (isObject && t.R2010Plus
                && rec.SourceVersion >= DwgVersion.AC1015 && rec.SourceVersion < DwgVersion.AC1021
                && rec.StringFields.Count > 0)
            {
                long xhsUp = rec.HandleStreamStartBit > 0 ? rec.HandleStreamStartBit : derivedHsBit;
                bool layOverwriteXdic = layUp && srcR4;
                CopyDataRegionCentralizing(body, src, start, bodyEndBit, rec, t, -1, -1,
                    commonHeaderBit: patchBit,
                    insertXdicMissing: xdicDelta > 0 && !layOverwriteXdic,
                    xdicMissingValue: xdicMissing || layOverwriteXdic
                        || SourceXdicRefIsNull(src, xhsUp, (long)rec.BodyEnd * 8),
                    insertHasDsData: dsDelta > 0,
                    overwriteXdicAtAnchor: layOverwriteXdic);
                sty15Done = true;
                if (DebugTrace)
                    System.Console.Error.WriteLine(
                        $"GENUP h={rec.Handle:X} t={rec.TypeCode} body={body.BitPosition} to={bodyEndBit} from={start}");
            }
            else if (dsAc15Plan is { } ds15 && ds15.TryCarryFrameStart(
                body, src, start, bodyEndBit, rec, t, out mlsHandles))
            { sty15Done = true; }
            else if (rec.LogicalType == 0x4F && t.R2010Plus
                && rec.SourceVersion >= DwgVersion.AC1015 && rec.SourceVersion < DwgVersion.AC1021
                && new Records.XrecordCarryPreR2007Up() is { } xrR2010
                && xrR2010.TryCarryFrameStartR2010(body, src, start, bodyEndBit, rec, t))
            {
                sty15Done = true;
                if (DebugTrace)
                    System.Console.Error.WriteLine(
                        $"XRDONE10 h={rec.Handle:X} body={body.BitPosition} to={bodyEndBit} from={start}");
            }
            else if (bodyCarriesRl && patchBit > start + 32)
            {
                body.WriteRL((uint)((long)srcRlValue + bitsizeDelta + typeBitsDelta
                    - (r2007HasStringsTail ? 1 : 0)));
                CopyCommonHeadWithEedDown(body, src, start + 32, patchBit, rec, t);
            }
            else if (mlsUp && rec.EedStartBit >= start
                     && rec.EedStartBit + rec.EedBitLen <= patchBit)
            {
                CopyBits(body, src, start, (int)(rec.EedStartBit - start));
                body.WriteBS(0);
                CopyBits(body, src, rec.EedStartBit + rec.EedBitLen,
                    (int)(patchBit - rec.EedStartBit - rec.EedBitLen));
            }
            else
            {
                CopyCommonHeadWithEedDown(body, src, start, patchBit, rec, t);
            }
            long srcWin = patchBit;
            if (DebugTrace && rec.LogicalType == TypeStyle) { IO.BitWriter.DebugWatch = body; System.Console.Error.WriteLine($"STYX0 h={rec.Handle:X} body={body.BitPosition}"); }
            if (isObject && !sty15Done)
            {
                if (DebugTrace && rec.LogicalType == TypeStyle) System.Console.Error.WriteLine($"STYGATE h={rec.Handle:X} 窗口块执行");
                if (dstR4)
                {
                    if (srcR4)
                    {
                        if (mlsUp)
                        {
                            body.WriteB(true);
                            srcWin++;
                        }
                        else if (layUp) { body.WriteB(true); srcWin++; }
                        else { body.WriteB(ReadSrcBit(src, srcWin)); srcWin++; }
                    }
                    else
                    {
                        long xhs = rec.HandleStreamStartBit > 0 ? rec.HandleStreamStartBit : derivedHsBit;
                        if (xhs <= 0 && srcRlValue > 0 && !rec.SourceVersion.IsR2010Plus()
                            && t.Version == DwgVersion.AC1018)
                            xhs = (long)rec.BodyStart * 8 + srcRlValue;
                        bool xdicNull = SourceXdicRefIsNull(src, xhs, (long)rec.BodyEnd * 8);
                        if (DebugTrace && rec.LogicalType == TypeStyle) System.Console.Error.WriteLine($"STYWB-xdic h={rec.Handle:X}");
                        body.WriteB(xdicMissing || xdicNull);
                    }
                }
                else if (srcR4) srcWin++;
                if (dstR13)
                {
                    if (srcR13) { body.WriteB(ReadSrcBit(src, srcWin)); srcWin++; }
                    else { if (DebugTrace && rec.LogicalType == TypeStyle) System.Console.Error.WriteLine($"STYWB-ds h={rec.Handle:X}"); body.WriteB(false); }
                }
                else if (srcR13) srcWin++;
                if (mlsUp) body.WriteBS(2);
            }
            else
            {
                if (!isObject)
                {
                    int skip = bitsizeDelta < 0 ? -bitsizeDelta : 0;
                    if (commonDelta > 0)
                    {
                        if (xdicDelta > 0) body.WriteB(xdicMissing);
                        if (dsDelta > 0) body.WriteB(false);
                    }
                    srcWin = patchBit + skip;
                }
            }
            if (isObject && rec.LogicalType == TypeBlockHeader && !t.R2007Plus && srcR4
                && rec.StringStreamStartBit > 0
                && TryReadCentralString(src, rec.StringStreamStartBit, out string bhName) && bhName.Length > 0)
            {
                body.WriteTV(bhName, false);
                var xbr = new BitReader(src);
                DwgObjectRecord.SeekTo(xbr, srcWin);
                int resolved = xbr.ReadBS();
                body.WriteB(true);
                body.WriteBS((short)resolved);
                body.WriteB(false);
                srcWin = xbr.BitPosition;
                long afterBase = SkipBlockHeaderToBasePt(src, srcWin, bodyEndBit);
                if (afterBase > 0
                    && TryCarryBlockHeaderTailAc1018(body, src, srcWin, afterBase, bodyEndBit,
                                                     rec.StringStreamStartBit, out long tailNext))
                    srcWin = tailNext;
                if (DebugTrace)
                    System.Console.Error.WriteLine(
                        $"BLKHDOWN h={rec.Handle:X} name=\"{bhName}\" resolved={resolved} →{t.Version}");
            }
            if (DebugTrace && rec.LogicalType == TypeStyle) { System.Console.Error.WriteLine($"STYX3 h={rec.Handle:X} body={body.BitPosition}"); IO.BitWriter.DebugWatch = null; }
            if (srcWin < bodyEndBit && !sty15Done)
            {
                long mleSkip = -1, mleInsert = -1;
                if (rec.LogicalType == TypeMLeaderStyle
                    && srcR13 != dstR13
                    && rec.StringStreamStartBit > srcWin && rec.StringStreamStartBit < bodyEndBit)
                {
                    if (dstR13) mleInsert = rec.StringStreamStartBit;
                    else mleSkip = rec.StringStreamStartBit;
                }
                if (r2007TailBit > srcWin && r2007TailBit < bodyEndBit) mleSkip = r2007TailBit;
                if (NeedsCmcUpgrade(rec, t))
                {
                    long fieldEnd = (long)rec.BodyStart * 8 + srcRlValue;
                    if (System.Environment.GetEnvironmentVariable("DWG509") == "1"
                        && rec.TypeCode == 509)
                        System.Console.Error.WriteLine(
                            $"W509 h={rec.Handle:X} bodyStartBit={(long)rec.BodyStart * 8} srcWin={srcWin} (rel {srcWin - (long)rec.BodyStart * 8}) " +
                            $"fieldEnd={fieldEnd} (rel {fieldEnd - (long)rec.BodyStart * 8}) bodyEndBit={bodyEndBit} " +
                            $"srcRl={srcRlValue} bitsizeDelta={bitsizeDelta} typeBitsDelta={typeBitsDelta} " +
                            $"bodyBitNow={body.BitPosition} srcVer={rec.SourceVersion} tgt={t.Version}");
                    if (fieldEnd > srcWin && fieldEnd <= bodyEndBit)
                    {
                        int cmcDelta = ReencodeClassBodyFields(body, src, rec, t,
                            srcWin, fieldEnd);
                        long hsTailEnd = LastMeaningfulHandleEnd(src, fieldEnd, bodyEndBit);
                        if (hsTailEnd <= fieldEnd) hsTailEnd = bodyEndBit;
                        if (fieldEnd < hsTailEnd)
                            CopyBits(body, src, fieldEnd, (int)(hsTailEnd - fieldEnd));
                        if (cmcDelta != 0 && bodyCarriesRl)
                        {
                            uint newRl = (uint)((long)srcRlValue + bitsizeDelta + typeBitsDelta + cmcDelta);
                            body.PatchRL(0, newRl);
                            if (DebugTrace)
                                System.Console.Error.WriteLine(
                                    $"CMC h={rec.Handle:X} t={rec.TypeCode} Δ={cmcDelta} RL {srcRlValue}→{newRl}");
                        }
                    }
                    else
                    {
                        CopyBitsWithMle(body, src, srcWin, bodyEndBit, mleSkip, mleInsert);
                    }
                }
                else if (IsVisualStyleR2013bBoundary(rec, t)
                         && TryCarryVisualStyleR2013b(body, rec, src, srcWin, bodyEndBit, t))
                {
                }
                else
                    CopyDataRegionCentralizing(body, src, srcWin,
                        TrimPreR2004BodyHandlePad(src, rec, t, srcRlValue, srcWin, bodyEndBit),
                        rec, t, mleSkip, mleInsert, bodyCarriesRl);
            }
        }
        else
        {
            if (bodyCarriesRl && typeBitsDelta != 0 && len > 32)
            {
                uint rl0 = ReadRlAt(src, start);
                body.WriteRL((uint)((long)rl0 + typeBitsDelta));
                CopyBits(body, src, start + 32, len - 32);
            }
            else
            {
                CopyDataRegionCentralizing(body, src, start, start + len, rec, t, -1, -1);
            }
            cfXdicOp = 0; cfDataEnd = -1;
        }
    }
}
