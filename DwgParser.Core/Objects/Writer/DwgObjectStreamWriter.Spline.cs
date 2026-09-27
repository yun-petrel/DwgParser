using DwgParser.Core.IO;

namespace DwgParser.Core.Objects;

public static partial class DwgObjectStreamWriter
{
    private const short TypeSpline = 0x24;

    internal static void CarrySplineR2013Up(BitWriter w, DwgObjectRecord rec, byte[] src,
        long geoStart, long geoLen)
    {
        var br = new BitReader(src, 0);
        DwgObjectRecord.SeekTo(br, geoStart);
        int scenario = (int)br.ReadBL();
        (int flags1, int knotparam) = scenario switch
        {
            1 => (0, 15),
            2 => (1, 0),
            _ => (0, 0),
        };
        w.WriteBL(scenario);
        w.WriteBL(flags1);
        w.WriteBL(knotparam);
        long rest = geoStart + geoLen - br.BitPosition;
        if (rest > 0)
            CopyBits(w, src, br.BitPosition, (int)rest);
        if (DebugTrace)
            System.Console.Error.WriteLine(
                $"SPLUP h={rec.Handle:X} scenario={scenario} → splineflags={flags1} knotparam={knotparam} 余 {rest} 位");
    }

    internal static bool CarrySplineR2013Down(BitWriter w, DwgObjectRecord rec, byte[] src,
        long geoStart, long geoLen)
    {
        long geoEnd = geoStart + geoLen;
        if (geoLen <= 0 || geoEnd > (long)src.Length * 8) return false;
        var tmp = new BitWriter(64);
        try
        {
            var br = new BitReader(src, 0);
            DwgObjectRecord.SeekTo(br, geoStart);
            long scenario = br.ReadBL();
            long flags1 = br.ReadBL();
            long knotparam = br.ReadBL();
            if (br.BitPosition > geoEnd) return false;
            long eff = scenario;
            if ((flags1 & 1) != 0) eff = 2;
            if (knotparam == 15) eff = 1;
            tmp.WriteBL((int)eff);
            long rest = geoEnd - br.BitPosition;
            if (rest > 0) CopyBits(tmp, src, br.BitPosition, (int)rest);
            if (DebugTrace)
                System.Console.Error.WriteLine(
                    $"SPLDN h={rec.Handle:X} scenario={scenario} flags1={flags1} kp={knotparam} " +
                    $"→ eff={eff}，剥 2 枚 BL，余 {rest} 位");
        }
        catch
        {
            return false;
        }
        Append(w, tmp);
        return true;
    }
}
