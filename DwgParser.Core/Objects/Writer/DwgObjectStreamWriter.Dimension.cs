using DwgParser.Core.IO;

namespace DwgParser.Core.Objects;

public static partial class DwgObjectStreamWriter
{
    internal static bool DimensionCrossesR2007(DwgObjectRecord rec, Target t)
        => IsDimensionFamily(rec)
           && rec.SourceVersion >= DwgVersion.AC1015 && rec.SourceVersion < DwgVersion.AC1021
           && t.Version >= DwgVersion.AC1021
           && rec.DimActMeasurementEndBit > 0;

    internal static bool CarryDimensionGeometry(BitWriter body, DwgObjectRecord rec, byte[] src,
        long geoStart, int geoLen, Target t, string? hatchGradientName)
    {
        if (!DimensionCrossesR2007(rec, t)) return false;
        long anchor = rec.DimActMeasurementEndBit;
        long geoEnd = geoStart + geoLen;
        if (anchor <= geoStart || anchor > geoEnd) return false;

        if (HasStringFieldInRange(rec, geoStart, geoLen))
            CarryWithStrings(body, rec, src, geoStart, geoLen, t, hatchGradientName, anchor, false, 3);
        else
            CopyBitsInsertingOne(body, src, geoStart, geoEnd, anchor, false, 3);

        if (DebugTrace)
            System.Console.Error.WriteLine(
                $"DIMR2007 h={rec.Handle:X} t={rec.TypeCode} {rec.SourceVersion}→{t.Version} " +
                $"insert3@{anchor - geoStart}/{geoLen}");
        return true;
    }

    internal static bool DimensionDowngradesR2007(DwgObjectRecord rec, Target t)
        => IsDimensionFamily(rec)
           && rec.SourceVersion >= DwgVersion.AC1021 && !t.R2007Plus
           && rec.DimActMeasurementEndBit > 0 && rec.DimUserTextSlotBit > 0
           && t.Version == DwgVersion.AC1018;

    internal static bool CarryDimensionDowngrade(BitWriter body, DwgObjectRecord rec, byte[] src,
        long geoStart, int geoLen, Target t)
    {
        if (!DimensionDowngradesR2007(rec, t)) return false;
        long anchor = rec.DimActMeasurementEndBit;
        long rawGeoEnd = geoStart + geoLen;
        if (anchor <= geoStart || anchor + 3 > rawGeoEnd) return false;
        long ut = rec.DimUserTextSlotBit;
        if (ut < geoStart || ut > anchor) return false;
        long geoEnd = rec.StringStreamStartBit > anchor + 3 && rec.StringStreamStartBit < rawGeoEnd
            ? rec.StringStreamStartBit
            : rawGeoEnd;
        if (DebugTrace)
            System.Console.Error.WriteLine(
                $"DIMDN h={rec.Handle:X} t={rec.TypeCode} {rec.SourceVersion}→{t.Version} " +
                $"geo=({geoStart},{geoLen}) ss@{rec.StringStreamStartBit}→窗收至 {geoEnd - geoStart} " +
                $"ut@{ut - geoStart} anchor@{anchor - geoStart} txLen={rec.DimUserText.Length}");
        CopyBits(body, src, geoStart, (int)(ut - geoStart));
        body.WriteTV(rec.DimUserText, false);
        CopyBits(body, src, ut, (int)(anchor - ut));
        CopyBits(body, src, anchor + 3, (int)(geoEnd - anchor - 3));
        return true;
    }
}
