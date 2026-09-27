using DwgParser.Core.IO;

namespace DwgParser.Core.Objects;

public static partial class DwgObjectStreamWriter
{
    private const int LtypeStringsAreaBits = 2048;

    private static bool CarriesLtypeR2010Down(DwgObjectRecord rec, Target t)
    {
        if (System.Environment.GetEnvironmentVariable("DWGDBG") == "1"
            && rec.LogicalType == TypeLType)
            System.Console.Error.WriteLine(
                $"LTPGATE h={rec.Handle:X} tgt={t.Version} src={rec.SourceVersion} " +
                $"ss={rec.StringStreamStartBit} dn={rec.LType?.HasDownAnchors.ToString() ?? "null"} " +
                $"name=\"{rec.LType?.Name}\"");
        return t.Version == DwgVersion.AC1018
           && rec.SourceVersion >= DwgVersion.AC1021
           && rec.LogicalType == TypeLType
           && rec.StringStreamStartBit > 0
           && rec.LType is { } cap && cap.HasDownAnchors;
    }

    private static bool CarriesLtypeR2010ToAc1015(DwgObjectRecord rec, Target t)
        => t.Version == DwgVersion.AC1015
           && rec.SourceVersion >= DwgVersion.AC1021
           && rec.LogicalType == TypeLType
           && rec.StringStreamStartBit > 0
           && rec.LType is { } cap && cap.HasDownAnchors;

    private static bool CarryLtypeR2010ToAc1015(BitWriter body, DwgObjectRecord rec, byte[] src,
        Target t, out BitWriter? handles)
        => CarryLtypeR2010DownCore(body, rec, src, t, ac1015: true, out handles);

    private static bool CarryLtypeR2010Down(BitWriter body, DwgObjectRecord rec, byte[] src,
        Target t)
        => CarryLtypeR2010DownCore(body, rec, src, t, ac1015: false, out _);

    private static bool CarryLtypeR2010DownCore(BitWriter body, DwgObjectRecord rec, byte[] src,
        Target t, bool ac1015, out BitWriter? handles)
    {
        handles = null;
        var cap = rec.LType!;
        long start = rec.CarryStartBit + (rec.SourceVersion.IsR2010Plus() ? 0 : 32);
        long ssStart = rec.StringStreamStartBit;
        long nameIns = cap.DownNameInsBit;
        long descIns = cap.DownDescInsBit;
        long patEnd = cap.DownPatEndBit;
        if (start <= 0 || ssStart <= start) return false;
        if (!(start < nameIns && nameIns < descIns && descIns < patEnd && patEnd == ssStart))
            return false;
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
            if (numReactors is < 0 or > 10000) return false;
            bool xdic = br.ReadB();
            if (rec.SourceVersion >= DwgVersion.AC1027) br.ReadB();
            if (br.BitPosition != nameIns) return false;
            int resolved = br.ReadBS();
            if (br.BitPosition != descIns) return false;

            CopyBits(body, src, start, (int)(eedStart - start));
            if (!TryWriteEedPreR2007Range(body, src, eedStart, eedLen))
                CopyBits(body, src, eedStart, eedLen);
            body.WriteBL((int)numReactors);
            if (!ac1015) body.WriteB(xdic);

            body.WriteTV(cap.Name);

            body.WriteB(true);
            body.WriteBS((short)resolved);
            body.WriteB(resolved == 256);

            body.WriteTV(cap.Description);

            if (!EmitLtypePatternAndStrings(body, src, descIns, patEnd, rec, t)) return false;
            if (ac1015)
            {
                long hsEnd = LastMeaningfulHandleEnd(src, hsAc15, (long)rec.BodyEnd * 8);
                var hw = new BitWriter(16);
                CopyBits(hw, src, hsAc15, (int)ownerBitsAc15);
                hw.WriteHandleRef(3, 0);
                CopyBits(hw, src, hsAc15 + ownerBitsAc15, (int)(hsEnd - hsAc15 - ownerBitsAc15));
                handles = hw;
            }
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool EmitLtypePatternAndStrings(BitWriter body, byte[] src,
        long descIns, long patEnd, DwgObjectRecord rec, Target t)
    {
        CopyBits(body, src, descIns, (int)(patEnd - descIns));
        for (int i = 0; i < LtypeStringsAreaBits; i++) body.WriteB(false);
        if (DebugTrace)
            System.Console.Error.WriteLine(
                $"LTYPDOWN h={rec.Handle:X} {rec.SourceVersion}→{t.Version} " +
                $"name=\"{rec.LType?.Name}\" desc=\"{rec.LType?.Description}\" " +
                $"own={rec.CarryStartBit} nameIns={rec.LType?.DownNameInsBit} " +
                $"descIns={descIns} patEnd={patEnd} ss={rec.StringStreamStartBit} " +
                $"out={body.BitPosition}");
        return true;
    }
}
