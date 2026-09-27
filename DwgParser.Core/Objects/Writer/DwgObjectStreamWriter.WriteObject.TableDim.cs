using DwgParser.Core.IO;

namespace DwgParser.Core.Objects;

public static partial class DwgObjectStreamWriter
{
    private static bool CarryAc1018TableDimGroup(DwgObjectRecord rec, Target t, BitWriter body, byte[] src,
        ref BitWriter? mlsHandles, ref long derivedHsBit)
    {
        if (CarriesTableContentR2007Up(rec, t)
                 && CarryTableContentR2007Up(body, rec, src, rec.CarryStartBit, rec.CarryBitLen,
                                             t, out var tc531Handles))
        {
            mlsHandles = tc531Handles;
            if (DebugTrace)
                System.Console.Error.WriteLine(
                    $"TC531 h={rec.Handle:X} body={body.BitPosition} " +
                    $"hs={tc531Handles?.BitPosition ?? 0} verbatim-native");
            return true;
        }
        if (CarriesThinTableContentUp(rec, t)
                 && ConvertThinTableContentUp(body, rec, src, rec.CarryStartBit, rec.CarryBitLen,
                                              t, out var tc531UpHandles))
        {
            mlsHandles = tc531UpHandles;
            return true;
        }
        if (CarriesTableContentR2007Down(rec, t)
                 && ConvertTableContentR2007Down(body, rec, src, rec.CarryStartBit,
                                                 rec.CarryBitLen, t, out var tc531DownHs))
        {
            mlsHandles = tc531DownHs;
            return true;
        }
        if (CarriesThinFieldUp(rec, t)
                 && ConvertThinFieldUp(body, rec, src, rec.CarryStartBit, rec.CarryBitLen,
                                       t, out var fieldUpHandles))
        {
            mlsHandles = fieldUpHandles;
            return true;
        }
        if (CarriesMediumFieldDown(rec, t)
                 && ConvertMediumFieldDown(body, rec, src, rec.CarryStartBit, rec.CarryBitLen,
                                           t, out var fieldDownHandles))
        {
            mlsHandles = fieldDownHandles;
            return true;
        }
        if (DimAssocDowngradesR2007(rec, t)
                 && CarryDimAssocDowngrade(body, rec, src, t))
        {
            derivedHsBit = rec.DimAssoc!.HsBit;
            return true;
        }
        return false;
    }
}
