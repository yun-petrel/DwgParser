using DwgParser.Core.IO;

namespace DwgParser.Core.Objects;

public static partial class DwgObjectStreamWriter
{
    private static bool CarriesMLineStyleAc1021Down(DwgObjectRecord rec, Target t)
        => t.Version == DwgVersion.AC1018
           && rec.SourceVersion == DwgVersion.AC1021
           && rec.LogicalType == TypeMLineStyle
           && !rec.HasHandleStream;

    private static bool CarryMLineStyleAc1021Down(BitWriter body, DwgObjectRecord rec, byte[] src,
        Target t, out BitWriter? handles)
    {
        handles = null;
        if (!DictionaryCarry.TryAc1021Spans(src, rec, out long dataEnd, out long ss,
                out bool hasStrings) || !hasStrings) return false;
        if (!DictionaryCarry.TryAc1021HandlesTail(rec, src, dataEnd, 2, out handles)) return false;
        return CarryMLineStyleCore(body, rec, src, t, rec.CarryStartBit + 32, ss, dataEnd);
    }
}
