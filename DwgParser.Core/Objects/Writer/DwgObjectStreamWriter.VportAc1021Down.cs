using DwgParser.Core.IO;

namespace DwgParser.Core.Objects;

public static partial class DwgObjectStreamWriter
{
    private static bool CarriesVportAc1021Down(DwgObjectRecord rec, Target t)
        => t.Version == DwgVersion.AC1018
           && rec.SourceVersion == DwgVersion.AC1021
           && rec.LogicalType == TypeVport
           && rec.Vport is { ValuesCommitted: true, XrefResolved: 0 or 256,
               DownXrefEndBit: > 0, DownLightsStartBit: > 0, DownLightsEndBit: > 0,
               DownGridTailBit: > 0 } cap
           && !string.IsNullOrEmpty(cap.Name)
           && !rec.HasHandleStream;

    private static bool CarryVportAc1021Down(BitWriter body, DwgObjectRecord rec, byte[] src,
        Target t, out BitWriter? handles)
    {
        handles = null;
        if (!DictionaryCarry.TryAc1021Spans(src, rec, out long dataEnd, out long ss,
                out bool hasStrings) || !hasStrings) return false;

        var hs = new BitWriter(16);
        if (!CopyVportHandlesDownToAc1018(hs, src, dataEnd, (long)rec.BodyEnd * 8, rec)) return false;

        bool ok = CarryVportCore(body, rec, src, t, rec.CarryStartBit + 32, ss);
        if (!ok) { hs = null; }
        handles = hs;
        return ok;
    }

    private static bool CarriesVportAc1021ToAc1015(DwgObjectRecord rec, Target t)
        => t.Version == DwgVersion.AC1015
           && rec.SourceVersion == DwgVersion.AC1021
           && rec.LogicalType == TypeVport
           && rec.Vport is { ValuesCommitted: true, XrefResolved: 0 or 256,
               DownXrefEndBit: > 0, DownLightsStartBit: > 0, DownLightsEndBit: > 0,
               DownGridTailBit: > 0 } cap
           && !string.IsNullOrEmpty(cap.Name)
           && !rec.HasHandleStream;

    private static bool CarryVportAc1021ToAc1015(BitWriter body, DwgObjectRecord rec,
        byte[] src, Target t, out BitWriter? handles)
    {
        handles = null;
        if (!DictionaryCarry.TryAc1021Spans(src, rec, out long dataEnd, out long ss,
                out bool hasStrings) || !hasStrings) return false;

        var shaped = new BitWriter(16);
        if (!CopyVportHandlesDownToAc1018(shaped, src, dataEnd, (long)rec.BodyEnd * 8, rec))
            return false;
        var bytes = shaped.ToArray();
        BitWriter hs;
        if (HandleStreamHasXdicSlot(bytes, shaped.BitPosition)) hs = shaped;
        else
        {
            if (FindAfterNumReactors(rec, src, PrefixHasRl(rec.SourceVersion), out _, out uint nr) < 0)
                return false;
            if (nr > 64) return false;
            hs = new BitWriter(16);
            if (!InsertXdicSlotAfterHead(shaped, hs, nr + 1)) return false;
        }

        bool ok = CarryVportCore(body, rec, src, t, rec.CarryStartBit + 32, ss, ac1015: true);
        handles = ok ? hs : null;
        return ok;
    }
}
