using DwgParser.Core.Format;
using DwgParser.Core.IO;

namespace DwgParser.Core.Objects;

public static partial class DwgObjectStreamWriter
{
    private static bool CarriesLayoutR2010ToAc1015(DwgObjectRecord rec, Target t)
        => t.Version == DwgVersion.AC1015
           && rec.SourceVersion.IsR2010Plus()
           && rec.LogicalType == TypeLayout
           && rec.Layout is { DowngradeCaptured: true } cap
           && cap.Downgrade.Count == 5
           && rec.StringStreamStartBit > 0;

    private static bool CarryLayoutR2010ToAc1015(BitWriter body, DwgObjectRecord rec,
        byte[] src, out BitWriter? handles)
    {
        handles = null;
        if (rec.CarryStartBit <= 0 || rec.StringStreamStartBit <= rec.CarryStartBit) return false;
        if (rec.HandleStreamStartBit <= 0) return false;
        try
        {
            var t18 = new Target(DwgVersion.AC1018);
            var probeMid = new BitWriter(256);
            if (!CarryLayoutCore(probeMid, rec, src, t18, rec.CarryStartBit, rec.StringStreamStartBit))
                return false;
            long midEnd = probeMid.BitPosition;
            if (midEnd <= 0) return false;
            byte[] mid = probeMid.ToArray();

            var probeData = new BitWriter(256);
            if (!TryLayoutDataAc1015FromAc1018Form(probeData, mid, 0, midEnd, rec.Handle,
                    out int nr, out _)) return false;
            if (!TryLayoutHandlesAc1015(src, rec.HandleStreamStartBit, (long)rec.BodyEnd * 8, nr,
                    true, out var hw)) return false;

            CopyBits(body, probeData.ToArray(), 0, (int)probeData.BitPosition);
            handles = hw;

            if (DebugTrace)
                System.Console.Error.WriteLine(
                    $"LY1015DN h={rec.Handle:X} 82 {rec.SourceVersion}→AC1015 nr={nr} " +
                    $"mid={midEnd}位 body={body.BitPosition} hs={hw.BitPosition}");
            return true;
        }
        catch { return false; }
    }

    private static bool CarriesLayoutAc1021ToAc1015(DwgObjectRecord rec, Target t)
        => t.Version == DwgVersion.AC1015
           && rec.SourceVersion == DwgVersion.AC1021
           && rec.LogicalType == TypeLayout
           && rec.Layout is { DowngradeCaptured: true } cap
           && cap.Downgrade.Count == 5
           && !rec.HasHandleStream;

    private static bool CarryLayoutAc1021ToAc1015(BitWriter body, DwgObjectRecord rec,
        byte[] src, out BitWriter? handles)
    {
        handles = null;
        if (rec.CarryStartBit <= 0) return false;
        if (!DictionaryCarry.TryAc1021Spans(src, rec, out long dataEnd, out long ss,
                out bool hasStrings) || !hasStrings) return false;
        if (dataEnd <= rec.CarryStartBit || ss <= rec.CarryStartBit) return false;
        try
        {
            var t18 = new Target(DwgVersion.AC1018);
            var probeMid = new BitWriter(256);
            if (!CarryLayoutCore(probeMid, rec, src, t18, rec.CarryStartBit + 32, ss)) return false;
            long midEnd = probeMid.BitPosition;
            if (midEnd <= 0) return false;
            byte[] mid = probeMid.ToArray();

            var probeData = new BitWriter(256);
            if (!TryLayoutDataAc1015FromAc1018Form(probeData, mid, 0, midEnd, rec.Handle,
                    out int nr, out _)) return false;
            if (!TryLayoutHandlesAc1015(src, dataEnd, (long)rec.BodyEnd * 8, nr,
                    true, out var hw)) return false;

            CopyBits(body, probeData.ToArray(), 0, (int)probeData.BitPosition);
            handles = hw;

            if (DebugTrace)
                System.Console.Error.WriteLine(
                    $"LY21DN h={rec.Handle:X} 82 AC1021→AC1015 nr={nr} " +
                    $"mid={midEnd}位 body={body.BitPosition} hs={hw.BitPosition}@{dataEnd}");
            return true;
        }
        catch { return false; }
    }
}
