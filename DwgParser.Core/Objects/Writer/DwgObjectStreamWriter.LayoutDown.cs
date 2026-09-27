using DwgParser.Core.IO;

namespace DwgParser.Core.Objects;

public static partial class DwgObjectStreamWriter
{
    private static bool CarriesLayoutR2010Down(DwgObjectRecord rec, Target t)
        => t.Version == DwgVersion.AC1018
           && rec.SourceVersion.IsR2010Plus()
           && rec.LogicalType == TypeLayout
           && rec.Layout is { DowngradeCaptured: true } cap
           && cap.Downgrade.Count == 5
           && rec.StringStreamStartBit > 0;

    private static bool CarriesLayoutAc1021Down(DwgObjectRecord rec, Target t)
        => t.Version == DwgVersion.AC1018
           && rec.SourceVersion == DwgVersion.AC1021
           && rec.LogicalType == TypeLayout
           && rec.Layout is { DowngradeCaptured: true } cap && cap.Downgrade.Count == 5
           && !rec.HasHandleStream;

    private static bool CarryLayoutAc1021Down(BitWriter body, DwgObjectRecord rec, byte[] src,
        Target t, out BitWriter? handles)
    {
        handles = null;
        if (!DictionaryCarry.TryAc1021Spans(src, rec, out long dataEnd, out long ss,
                out bool hasStrings) || !hasStrings) return false;
        long hsEnd = LastMeaningfulHandleEnd(src, dataEnd, (long)rec.BodyEnd * 8);
        if (hsEnd <= dataEnd) return false;
        var hs = new BitWriter(16);
        if (TryRemoveLayoutR2007Shadeplot(hs, src, dataEnd, hsEnd - dataEnd))
        {
            handles = hs;
            if (DebugTrace) System.Console.Error.WriteLine(
                $"LAY82HSD h={rec.Handle:X} 源{rec.SourceVersion} → 剥 (4,0) shadeplot 槽 hs={(int)hs.BitPosition}位");
        }
        else if (!DictionaryCarry.TryAc1021HandlesTail(rec, src, dataEnd, 1, out handles))
            return false;
        return CarryLayoutCore(body, rec, src, t, rec.CarryStartBit + 32, ss);
    }

    private static bool CarryLayoutR2010Down(BitWriter body, DwgObjectRecord rec, byte[] src,
        Target t)
        => CarryLayoutCore(body, rec, src, t, rec.CarryStartBit, rec.StringStreamStartBit);

    private static bool CarryLayoutCore(BitWriter body, DwgObjectRecord rec, byte[] src, Target t,
        long start, long ssStart)
    {
        var cap = rec.Layout!;
        if (start <= 0 || ssStart <= start) return false;
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
            long numericStart = br.BitPosition;
            if (numericStart <= start || numericStart >= ssStart) return false;

            CopyBits(body, src, start, (int)(eedStart - start));
            if (!TryWriteEedPreR2007Range(body, src, eedStart, eedLen))
                CopyBits(body, src, eedStart, eedLen);
            body.WriteBL((int)numReactors);
            body.WriteB(xdic);

            long cursor = numericStart;
            foreach (var s in cap.Downgrade)
            {
                if (s.Pos < cursor || s.Pos > ssStart) return false;
                CopyBits(body, src, cursor, (int)(s.Pos - cursor));
                body.WriteTV(s.Value);
                cursor = s.Pos;
            }
            CopyBits(body, src, cursor, (int)(ssStart - cursor));
            if (DebugTrace)
                System.Console.Error.WriteLine(
                    $"LAYDOWN h={rec.Handle:X} {rec.SourceVersion}→{t.Version} ins={cap.Downgrade.Count} " +
                    $"start={start} eed={eedStart}+{eedLen} numStart={numericStart} ss={ssStart} out={body.BitPosition} " +
                    $"[{string.Join(",", cap.Downgrade.Select(x => $"'{x.Value}'@{x.Pos}"))}]");
            return true;
        }
        catch
        {
            return false;
        }
    }
}
