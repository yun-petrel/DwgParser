using DwgParser.Core.IO;

namespace DwgParser.Core.Objects;

public static partial class DwgObjectStreamWriter
{
    private static void WriteReencodedBody(BitWriter body, DwgObjectRecord rec, byte[] src, Target t)
    {
        body.WriteHandleRef(rec.OwnHandle.Code, rec.OwnHandle.Value);
        if (DropsMTextRtlSegmentEed(src, rec, t))
            body.WriteBS(0);
        else if (rec.EedBitLen > 0 && !TryWriteEedR2007PlusForEntity(body, src, rec, t)
            && !TryWriteEedPreR2007ForEntity(body, src, rec, t, out bool eedDownFailed,
                t.Version < DwgVersion.AC1018))
        {
            if (eedDownFailed && t.Version == DwgVersion.AC1018)
                body.WriteBS(0);
            else
                CopyBits(body, src, rec.EedStartBit, rec.EedBitLen);
        }
        else if (rec.EedBitLen <= 0)
            body.WriteBS(0);
        WriteCommonEntityData(body, rec, src, t);
        if (!t.R2000 && !rec.SourceVersion.IsR2004Plus() && rec.LogicalType == TypeHatch)
            WriteHatchGradientIfUpgrade(body, rec, t);
        if (rec.LogicalType == TypeMText && rec.HasMTextPrefix && rec.MTextTailBitLen > 0
            && (rec.SourceVersion >= DwgVersion.AC1021) != t.R2007Plus)
        {
            WriteMText(body, rec, src, t);
        }
        else
        {
            if (DebugTrace)
                System.Console.Error.WriteLine(
                    $"DBG h={rec.Handle:X} t={rec.TypeCode} srcV={rec.SourceVersion} dstV={t.Version} " +
                    $"bodyBits={body.BitPosition} geoStart={rec.GeoStartBit} geoLen={rec.GeoBitLen} " +
                    $"srcHandles@{rec.HandleStreamStartBit} hdlOK={rec.HasHandleStream} strF={rec.StringFields.Count}");
            var (geoStart, geoLen) = HatchGeometryWindow(rec, t);
            if (rec.LogicalType is 5 or 6 or 7 or 17 or 18 or 19 or 27 or 31 or 35 or 77
                && rec.SourceVersion >= DwgVersion.AC1021 && !t.R2007Plus && geoLen >= 1
                && rec.StringFields.Count == 0
                && geoStart + geoLen == rec.HandleStreamStartBit
                && !ReadSrcBit(src, geoStart + geoLen - 1))
                geoLen--;
            if (DebugTrace && rec.SourceVersion >= DwgVersion.AC1021 && !t.R2007Plus && geoLen >= 1
                && rec.StringFields.Count == 0 && geoStart + geoLen == rec.HandleStreamStartBit)
                System.Console.Error.WriteLine(
                    $"CEN520 h={rec.Handle:X} lt={rec.LogicalType} t={rec.TypeCode} len={geoLen} " +
                    $"tailbit={(ReadSrcBit(src, geoStart + geoLen - 1) ? 1 : 0)} " +
                    $"ss={rec.StringStreamStartBit}");
            string? hatchGradientName =
                rec.LogicalType == TypeHatch && t.R2007Plus && !rec.SourceVersion.IsR2004Plus()
                    ? string.Empty : null;
            if (CmcNamesNeedStream(rec, t))
            {
                CarryWithStrings(body, rec, src, geoStart, geoLen, t, hatchGradientName);
            }
            else if (rec.LogicalType is TypeInsert or TypeMInsert && rec.HasAttribs
                && rec.InsertAfterAttribsBit > geoStart
                && rec.SourceVersion.IsR2004Plus() != !t.R2000)
            {
                CarryInsertGeometry(body, rec, src, geoStart, geoLen, t);
            }
            else if (rec.LogicalType == TypeSpline
                     && rec.SourceVersion < DwgVersion.AC1027 && t.R2013Plus)
            {
                CarrySplineR2013Up(body, rec, src, geoStart, geoLen);
            }
            else if (rec.LogicalType == TypeSpline
                     && rec.SourceVersion >= DwgVersion.AC1027 && !t.R2013Plus)
            {
                if (!CarrySplineR2013Down(body, rec, src, geoStart, geoLen))
                    CarryWithStrings(body, rec, src, geoStart, geoLen, t, hatchGradientName);
            }
            else if (HatchDowngradesR2007(rec, t))
            {
                if (!CarryHatchDowngrade(body, rec, src, geoStart, geoLen, t))
                    CarryWithStrings(body, rec, src, geoStart, geoLen, t, hatchGradientName);
            }
            else if (DimensionDowngradesR2007(rec, t))
            {
                if (!CarryDimensionDowngrade(body, rec, src, geoStart, geoLen, t))
                    CarryWithStrings(body, rec, src, geoStart, geoLen, t, hatchGradientName);
            }
            else if (DimensionCrossesR2007(rec, t))
            {
                if (!CarryDimensionGeometry(body, rec, src, geoStart, geoLen, t, hatchGradientName))
                    CarryWithStrings(body, rec, src, geoStart, geoLen, t, hatchGradientName);
            }
            else if (AttributeTailCrossesR2007(rec, t))
            {
                long a2 = rec.AttTailKeepDuplicateBit;
                var extra = new List<BitInsert>();
                bool r2010b = TgtLocked(t) && !SrcLocked(rec) && rec.AttTailLockedBit > 0;
                if (r2010b)
                {
                    string locked = Rc8(AttTailLockedUp(rec));
                    if (TgtMtext(t) && !SrcMtext(rec)) locked += Rc8(AttTailMTextUp(rec));
                    extra.Add(new BitInsert(rec.AttTailLockedBit, locked));
                }
                bool keep = r2010b && rec.LogicalType == TypeAttDef;
                extra.Add(new BitInsert(a2, "1" + (keep ? Rc8(AttTailKeepUp(rec)) : "")));
                CarryWithStrings(body, rec, src, geoStart, geoLen, t, hatchGradientName,
                    -1, true, 1, extra);
            }
            else if (AttrTailDowngradesToAc1018(rec, t))
            {
                if (!CarryAttrTailDowngradeToAc1018(body, rec, src, geoStart, geoLen, t, hatchGradientName))
                    CarryWithStrings(body, rec, src, geoStart, geoLen, t, hatchGradientName);
            }
            else if (AttrTailDowngradesToAc1015(rec, t))
            {
                if (!CarryAttrTailDowngradeToAc1015(body, rec, src, geoStart, geoLen, t, hatchGradientName))
                    CarryWithStrings(body, rec, src, geoStart, geoLen, t, hatchGradientName);
            }
            else if (NeedsAttributeTailInsert(rec, t) || StripsAttributeTailInsert(rec, t))
            {
                if (!CarryAttributeTailGeometry(body, rec, src, geoStart, geoLen, t))
                    CarryWithStrings(body, rec, src, geoStart, geoLen, t, hatchGradientName);
            }
            else if (HatchR2010CrossRelevant(rec, t))
            {
                if (!CarryHatchR2010Cross(body, rec, src, geoStart, geoLen, t))
                    CarryWithStrings(body, rec, src, geoStart, geoLen, t, hatchGradientName);
            }
            else if (rec.LogicalType == TypeBlockEntity && !t.R2007Plus && rec.StringFields.Count > 0
                     && (rec.StringFields[0].BitLen > 0
                         || t.Version is DwgVersion.AC1018 or DwgVersion.AC1015))
            {
                long fieldEnd = rec.StringStreamStartBit > geoStart ? rec.StringStreamStartBit : geoStart;
                CopyBits(body, src, geoStart, (int)(fieldEnd - geoStart));
                body.WriteTV(rec.StringFields[0].Value, false);
                if (DebugTrace)
                    System.Console.Error.WriteLine(
                        $"BLKINLINE h={rec.Handle:X} name=\"{rec.StringFields[0].Value}\" " +
                        $"fieldBits={fieldEnd - geoStart} → {t.Version}");
            }
            else if (CarriesMTextBgFillToAc1018(rec, t)
                     && CarryMTextBgFillToAc1018(body, rec, src, geoStart, geoLen, t))
            {
            }
            else if (NeedsMTextAnnotative(rec, t))
            {
                CarryMTextAnnotativeR2018(body, rec, src, geoStart, geoLen);
            }
            else if (StripsMTextAnnotative(rec, t))
            {
                CarryMTextStripAnnotative(body, rec, src, geoStart, geoLen);
            }
            else if (RewritesViewportR2007Up(rec, t) || AppendsViewportShadePlotR2004(rec, t))
            {
                if (!(t.Version >= DwgVersion.AC1021
                        ? RewriteViewportR2007Up(body, rec, src, geoStart, geoLen, t)
                        : AppendViewportShadePlotR2004(body, rec, src, geoStart, geoLen)))
                    CarryWithStrings(body, rec, src, geoStart, geoLen, t, hatchGradientName);
            }
            else if (RewritesViewportR2007Down(rec, t))
            {
                if (!RewriteViewportR2007Down(body, rec, src, geoStart, geoLen, t))
                    CarryWithStrings(body, rec, src, geoStart, geoLen, t, hatchGradientName);
            }
            else if (RewritesTableR2013Up(rec, t))
            {
                if (!RewriteTableR2013Up(body, rec, src, geoStart, geoLen))
                    CarryWithStrings(body, rec, src, geoStart, geoLen, t, hatchGradientName);
            }
            else if (RewritesTableR2013Down(rec, t))
            {
                if (!RewriteTableR2013Down(body, rec, src, geoStart, geoLen))
                    CarryWithStrings(body, rec, src, geoStart, geoLen, t, hatchGradientName);
            }
            else if (CarriesTableR2007Up(rec, t))
            {
                if (!CarryTableR2007Up(body, rec, src, geoStart, geoLen))
                    CarryWithStrings(body, rec, src, geoStart, geoLen, t, hatchGradientName);
            }
            else if (CarriesTableR2007Down(rec, t))
            {
                if (!CarryTableR2007Down(body, rec, src, geoStart, geoLen))
                    CarryWithStrings(body, rec, src, geoStart, geoLen, t, hatchGradientName);
            }
            else if (CarriesTableR2010Up(rec, t))
            {
                CarryTableR2010Up(body, rec, t);
            }
            else if (CarriesTableR2010Down(rec, t))
            {
                CarryTableR2010Down(body, rec, t);
            }
            else if (rec.LogicalType == TypeLeader && rec.SourceVersion.IsR2010Plus()
                     && t.Version == DwgVersion.AC1018
                     && CarryLeaderR2010ToAc1018(body, rec, src, geoStart, geoLen))
            {
            }
            else if (rec.LogicalType == TypeLeader && rec.SourceVersion.IsR2010Plus()
                     && !t.R2010Plus && t.R2007Plus
                     && CarryLeaderR2010Down(body, rec, src, geoStart, geoLen))
            {
            }
            else if (rec.LogicalType == TypeLeader && !rec.SourceVersion.IsR2010Plus()
                     && t.R2010Plus
                     && CarryLeaderPreR2010Up(body, rec, src, geoStart, geoLen, t))
            {
            }
            else if (CarriesMLeaderR2007Down(rec, t)
                     && CarryMLeaderR2007Down(body, rec, src, geoStart, geoLen, t))
            {
            }
            else if (CarriesMLeaderR2010ToAc1018(rec, t)
                     && ConvertMLeaderR2010ToAc1018(body, rec, src, t, geoStart, geoLen))
            {
            }
            else if (CarriesMLeaderCmcR2004Up(rec, t))
            {
                CarryMLeaderGeometryUpgradingCmc(body, src, rec, t, geoStart, geoLen, hatchGradientName);
            }
            else
            {
                CarryWithStrings(body, rec, src, geoStart, geoLen, t, hatchGradientName,
                    extraInserts: LocateHatchFitInsertsForPreR2007Source(rec, src, t, geoStart, geoLen));
            }
        }
    }
}
