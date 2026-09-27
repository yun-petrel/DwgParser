using DwgParser.Core.IO;

namespace DwgParser.Core.Objects;

public static partial class DwgObjectStreamWriter
{
    private static bool CarriesStyleR2007Down(DwgObjectRecord rec, Target t)
        => t.Version == DwgVersion.AC1018
           && rec.SourceVersion >= DwgVersion.AC1021
           && rec.LogicalType == TypeStyle
           && rec.StringStreamStartBit > 0;

    private static bool CarriesStyleR2007ToAc1015(DwgObjectRecord rec, Target t)
        => t.Version == DwgVersion.AC1015
           && rec.SourceVersion >= DwgVersion.AC1021
           && rec.LogicalType == TypeStyle
           && rec.StringStreamStartBit > 0
           && rec.Style is { DownStringsAlive: true };

    private static bool CarryStyleR2007Down(BitWriter body, DwgObjectRecord rec, byte[] src,
        Target t)
        => CarryStyleR2007DownCore(body, rec, src, t, ac1015: false, out _);

    private static bool CarryStyleR2007ToAc1015(BitWriter body, DwgObjectRecord rec, byte[] src,
        Target t, out BitWriter? handles)
        => CarryStyleR2007DownCore(body, rec, src, t, ac1015: true, out handles);

    private static bool CarryStyleR2007DownCore(BitWriter body, DwgObjectRecord rec, byte[] src,
        Target t, bool ac1015)
        => CarryStyleR2007DownCore(body, rec, src, t, ac1015, out _);

    private static bool CarryStyleR2007DownCore(BitWriter body, DwgObjectRecord rec, byte[] src,
        Target t, bool ac1015, out BitWriter? handles)
    {
        handles = null;
        var cap = rec.Style;
        if (cap is null || !cap.DownStringsAlive) return false;
        long start = rec.CarryStartBit + (PrefixHasRl(rec.SourceVersion) ? 32 : 0);
        long ssStart = rec.StringStreamStartBit;
        if (start <= 0 || ssStart <= start) return false;
        long hsAc15 = 0, ownerBitsAc15 = 0;
        if (ac1015)
        {
            if (!rec.HasHandleStream || rec.HandleStreamStartBit <= 0) return false;
            hsAc15 = rec.HandleStreamStartBit;
            long hsEndAc15 = LastMeaningfulHandleEnd(src, hsAc15, (long)rec.BodyEnd * 8);
            if (hsEndAc15 <= hsAc15) return false;
            var hbrAc15 = new BitReader(src, 0);
            DwgObjectRecord.SeekTo(hbrAc15, hsAc15);
            ownerBitsAc15 = 8 + (hbrAc15.ReadRC() & 0xF) * 8L;
            if (ownerBitsAc15 >= hsEndAc15 - hsAc15) return false;
        }
        try
        {
            var br = new BitReader(src, 0);
            DwgObjectRecord.SeekTo(br, start);
            br.ReadHandle();
            long eedStart = br.BitPosition;
            if (!SkipEedWriter(br)) return false;
            int eedLen = (int)(br.BitPosition - eedStart);
            long numReactors = br.ReadBL();
            if (numReactors is < 0 or > 100_000) return false;
            bool xdic = br.ReadB();
            if (rec.SourceVersion >= DwgVersion.AC1027) br.ReadB();
            int xrefResolved = br.ReadBS();
            long numericStart = br.BitPosition;
            if (numericStart <= start || numericStart >= ssStart) return false;

            CopyBits(body, src, start, (int)(eedStart - start));
            if (!TryWriteEedPreR2007Range(body, src, eedStart, eedLen))
                CopyBits(body, src, eedStart, eedLen);
            body.WriteBL((int)numReactors);
            if (!ac1015) body.WriteB(xdic);
            body.WriteTV(cap.Name);
            body.WriteB(true);
            body.WriteBS((short)xrefResolved);
            body.WriteB(false);
            CopyBits(body, src, numericStart, (int)(ssStart - numericStart));
            body.WriteTV(cap.Font);
            body.WriteTV(cap.BigFont);
            if (ac1015)
            {
                long hsEnd = LastMeaningfulHandleEnd(src, hsAc15, (long)rec.BodyEnd * 8);
                var hw = new BitWriter(16);
                CopyBits(hw, src, hsAc15, (int)ownerBitsAc15);
                hw.WriteHandleRef(3, 0);
                CopyBits(hw, src, hsAc15 + ownerBitsAc15, (int)(hsEnd - hsAc15 - ownerBitsAc15));
                handles = hw;
            }
            if (DebugTrace)
                System.Console.Error.WriteLine(
                    $"STYDOWN h={rec.Handle:X} {rec.SourceVersion}→{t.Version} ac1015={ac1015} name=\"{cap.Name}\" " +
                    $"font=\"{cap.Font}\" bigfont=\"{cap.BigFont}\" xref={xrefResolved} " +
                    $"numBits={ssStart - numericStart}");
            return true;
        }
        catch
        {
            return false;
        }
    }
}
