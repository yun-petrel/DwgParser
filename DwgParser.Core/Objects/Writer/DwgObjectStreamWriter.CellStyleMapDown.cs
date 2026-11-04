using DwgParser.Core.IO;

namespace DwgParser.Core.Objects;

public static partial class DwgObjectStreamWriter
{
    private static bool CarriesCellStyleMapR2007Down(DwgObjectRecord rec, Target t)
        => t.Version == DwgVersion.AC1018
           && rec.SourceVersion.IsR2010Plus()
           && rec.LogicalType == TypeCellStyleMap
           && rec.CellStyleMap is { Captured: true }
           && rec.CellStyleMap.Strings.Count > 0
           && rec.StringStreamStartBit > 0;

    private static bool CarriesCellStyleMapR2007ToAc1015Down(DwgObjectRecord rec, Target t)
        => t.Version == DwgVersion.AC1015
           && rec.SourceVersion.IsR2010Plus()
           && rec.LogicalType == TypeCellStyleMap
           && rec.CellStyleMap is { Captured: true }
           && rec.CellStyleMap.Strings.Count > 0
           && rec.StringStreamStartBit > 0
           && rec.HasHandleStream
           && rec.HandleStreamStartBit > 0;

    internal static bool CarryCellStyleMapR2007ToAc1015Down(BitWriter body, DwgObjectRecord rec,
        byte[] src, out BitWriter? handles)
    {
        handles = null;
        if (!CarryCellStyleMapCore(body, rec, src, rec.StringStreamStartBit, 0, writeXdic: false))
            return false;
        long hsStart = rec.HandleStreamStartBit;
        long hsEnd = LastMeaningfulHandleEnd(src, hsStart, (long)rec.BodyEnd * 8);
        if (hsEnd <= hsStart) return false;
        var hs = new BitWriter(16);
        CopyBits(hs, src, hsStart, (int)(hsEnd - hsStart));
        long insertAt = DictionaryCarry.ScanOwnerReactorPrefix(hs.ToArray());
        if (insertAt <= 0 || insertAt > hs.BitPosition) return false;
        var probe = new BitReader(hs.ToArray(), 0);
        DwgObjectRecord.SeekTo(probe, insertAt);
        bool srcHasXdic = probe.ReadRC() == 3;
        if (!srcHasXdic)
        {
            long total = hs.BitPosition;
            var shifted = new BitWriter(16);
            CopyBits(shifted, hs.ToArray(), 0, (int)insertAt);
            shifted.WriteHandleRef(3, 0);
            CopyBits(shifted, hs.ToArray(), (int)insertAt, (int)(total - insertAt));
            hs = shifted;
        }
        handles = hs;
        if (DebugTrace)
            System.Console.Error.WriteLine(
                $"CSM15DN h={rec.Handle:X} {rec.SourceVersion}→AC1015 body={body.BitPosition} " +
                $"hs={hs.BitPosition}位@insert{insertAt} srcXdic={srcHasXdic}");
        return true;
    }

    internal static bool CarriesCellStyleMapAc1021Down(DwgObjectRecord rec, Target t)
        => t.Version == DwgVersion.AC1018
           && rec.SourceVersion == DwgVersion.AC1021
           && rec.LogicalType == TypeCellStyleMap
           && rec.CellStyleMap is { Strings.Count: > 0 }
           && !rec.HasHandleStream;

    internal static bool CarryCellStyleMapAc1021Down(BitWriter body, DwgObjectRecord rec, byte[] src,
        out BitWriter? handles)
    {
        handles = null;
        if (!DictionaryCarry.TryAc1021Spans(src, rec, out long dataEnd, out long ss,
                out bool hasStrings) || !hasStrings) return false;
        if (!DictionaryCarry.TryAc1021HandlesTail(rec, src, dataEnd, 1, out handles)) return false;
        return CarryCellStyleMapCore(body, rec, src, ss, 32);
    }

    internal static bool CarriesCellStyleMapAc1021ToAc1015Down(DwgObjectRecord rec, Target t)
        => t.Version == DwgVersion.AC1015
           && rec.SourceVersion == DwgVersion.AC1021
           && rec.LogicalType == TypeCellStyleMap
           && rec.CellStyleMap is { Strings.Count: > 0 }
           && !rec.HasHandleStream;

    internal static bool CarryCellStyleMapAc1021ToAc1015Down(BitWriter body, DwgObjectRecord rec,
        byte[] src, out BitWriter? handles)
    {
        handles = null;
        long start = rec.CarryStartBit;
        if (start <= 0) return false;
        if (!DictionaryCarry.TryAc1021Spans(src, rec, out long dataEnd, out long ss,
                out bool hasStrings) || !hasStrings) return false;
        if (!DictionaryCarry.TryAc1021HandlesTail(rec, src, dataEnd, 1, out var hs)) return false;
        long xdicBit = DictionaryCarry.FindXdBitAc1021(rec, src);
        if (xdicBit <= start + 32 || xdicBit >= ss) return false;
        bool srcXdicMissing = ReadSrcBit(src, xdicBit);
        long insertAt = -1;
        if (srcXdicMissing)
        {
            long total = hs!.BitPosition;
            insertAt = DictionaryCarry.ScanOwnerReactorPrefix(hs.ToArray());
            if (insertAt <= 0)
            {
                var (prefCnt, prefEnd) = WalkMeaningfulHandleRefs(hs.ToArray(), 0, total);
                if (prefCnt == 0 || prefEnd <= 0 || prefEnd > total) return false;
                insertAt = prefEnd;
            }
            if (insertAt <= 0 || insertAt > total) return false;
            var shifted = new BitWriter(16);
            CopyBits(shifted, hs.ToArray(), 0, (int)insertAt);
            shifted.WriteHandleRef(3, 0);
            CopyBits(shifted, hs.ToArray(), (int)insertAt, (int)(total - insertAt));
            hs = shifted;
        }
        if (!CarryCellStyleMapCore(body, rec, src, ss, 32, writeXdic: false)) return false;
        handles = hs;
        if (DebugTrace)
            System.Console.Error.WriteLine(
                $"CSM15A21 h={rec.Handle:X} {rec.SourceVersion}→AC1015 body={body.BitPosition} " +
                $"hs={hs!.BitPosition}位@ins{insertAt} srcXdicMiss={srcXdicMissing}");
        return true;
    }

    private static bool CarryCellStyleMapR2007Down(BitWriter body, DwgObjectRecord rec, byte[] src,
        Target t)
        => CarryCellStyleMapCore(body, rec, src, rec.StringStreamStartBit, 0);

    private static bool CarryCellStyleMapCore(BitWriter body, DwgObjectRecord rec, byte[] src,
        long ssStart, int rlSkip, bool writeXdic = true)
    {
        var cap = rec.CellStyleMap!;
        long start = rec.CarryStartBit + rlSkip;
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
            if (numReactors is < 0 or > 8) return false;
            bool xdic = br.ReadB();
            if (rec.SourceVersion >= DwgVersion.AC1027) br.ReadB();
            long numericStart = br.BitPosition;
            if (numericStart <= start || numericStart >= ssStart) return false;

            CopyBits(body, src, start, (int)(eedStart - start));
            if (!TryWriteEedPreR2007Range(body, src, eedStart, eedLen))
                CopyBits(body, src, eedStart, eedLen);
            body.WriteBL((int)numReactors);
            if (writeXdic) body.WriteB(xdic);

            long cursor = numericStart;
            foreach (var s in cap.Strings)
            {
                if (s.Pos < cursor || s.Pos > ssStart) return false;
                CopyBits(body, src, cursor, (int)(s.Pos - cursor));
                body.WriteTV(s.Value);
                cursor = s.Pos;
            }
            CopyBits(body, src, cursor, (int)(ssStart - cursor));
            if (DebugTrace)
                System.Console.Error.WriteLine(
                    $"CSMDOWN h={rec.Handle:X} {rec.SourceVersion}→" +
                    $"{(writeXdic ? "AC1018" : "AC1015")} " +
                    $"cells={cap.NumCells} ins={cap.Strings.Count} start={start} eed={eedStart}+{eedLen} " +
                    $"numStart={numericStart} ss={ssStart} out={body.BitPosition} " +
                    $"[{string.Join(",", cap.Strings.Select(x => $"'{x.Value}'@{x.Pos}"))}]");
            return true;
        }
        catch
        {
            return false;
        }
    }
}
