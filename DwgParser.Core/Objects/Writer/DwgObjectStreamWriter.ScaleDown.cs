using DwgParser.Core.IO;

namespace DwgParser.Core.Objects;

public static partial class DwgObjectStreamWriter
{
    private static bool CarriesScaleR2010ToAc1018(DwgObjectRecord rec, Target t)
    {
        if (System.Environment.GetEnvironmentVariable("DWGDBG") == "1"
            && rec.LogicalType == TypeScale)
            System.Console.Error.WriteLine(
                $"SCLGATE h={rec.Handle:X} tgt={t.Version} src={rec.SourceVersion} " +
                $"ss={rec.StringStreamStartBit} cap={rec.Scale?.Captured.ToString() ?? "null"} " +
                $"name=\"{rec.Scale?.Name}\" ins={rec.Scale?.DownNameInsertBit} tail={rec.Scale?.DownTailEndBit}");
        return t.Version == DwgVersion.AC1018
           && rec.SourceVersion.IsR2010Plus()
           && rec.LogicalType == TypeScale
           && rec.StringStreamStartBit > 0
           && rec.Scale is { Captured: true };
    }

    private static bool CarriesScaleAc1021Down(DwgObjectRecord rec, Target t)
        => t.Version == DwgVersion.AC1018
           && rec.SourceVersion == DwgVersion.AC1021
           && rec.LogicalType == TypeScale
           && rec.Scale is { Captured: true }
           && !rec.HasHandleStream;

    private static bool CarryScaleR2010ToAc1018(BitWriter body, DwgObjectRecord rec,
        byte[] src, Target t)
        => CarryScaleCore(body, rec, src, t, rec.CarryStartBit, rec.StringStreamStartBit);

    private static bool CarriesScaleR2010ToAc1015(DwgObjectRecord rec, Target t)
        => t.Version == DwgVersion.AC1015
           && rec.SourceVersion.IsR2010Plus()
           && rec.LogicalType == TypeScale
           && rec.StringStreamStartBit > 0
           && rec.Scale is { Captured: true }
           && rec.HasHandleStream && rec.HandleStreamStartBit > 0;

    internal static bool CarryScaleR2010ToAc1015(BitWriter body, DwgObjectRecord rec,
        byte[] src, Target t, out BitWriter? handles)
    {
        handles = null;
        if (!CarryScaleCore(body, rec, src, t, rec.CarryStartBit, rec.StringStreamStartBit,
                ac1015: true))
            return false;
        long hsStart = rec.HandleStreamStartBit;
        long hsEnd = LastMeaningfulHandleEnd(src, hsStart, (long)rec.BodyEnd * 8);
        if (hsEnd <= hsStart) return false;
        var hs = new BitWriter(16);
        CopyBits(hs, src, hsStart, (int)(hsEnd - hsStart));
        bool srcHasXdic = HandleStreamHasXdicSlot(hs.ToArray(), hs.BitPosition);
        if (!srcHasXdic) hs.WriteHandleRef(3, 0);
        handles = hs;
        if (DebugTrace)
            System.Console.Error.WriteLine(
                $"SCLDN15 h={rec.Handle:X} {rec.SourceVersion}→AC1015 name=\"{rec.Scale?.Name}\" " +
                $"body={body.BitPosition} hs={hs.BitPosition}位 srcXdic={srcHasXdic}");
        return true;
    }

    private static bool CarriesPreR2010XdicSlotAc1015(DwgObjectRecord rec, Target t)
        => t.Version == DwgVersion.AC1015
           && rec.SourceVersion == DwgVersion.AC1018
           && rec.LogicalType is TypeScale or TypeAppid or TypeLType or 0x35 or 0x41 or 500 or 512;

    private static bool CopyPreR2010HandlesWithXdicSlot(BitWriter handles, byte[] src,
        long hsStart, long hsEnd, DwgObjectRecord rec, bool strictHeadShape)
    {
        if (FindAfterNumReactors(rec, src, PrefixHasRl(rec.SourceVersion), out _, out uint nr) < 0)
            return false;
        if (nr > 1024) return false;
        long want = (long)nr + 1;
        long hp = hsStart, refsEnd = hsStart, insertAt = -1, seen = 0;
        var br = new BitReader(src, 0);
        while (hp + 8 <= hsEnd)
        {
            DwgObjectRecord.SeekTo(br, hp);
            int cc = br.ReadRC();
            if (cc == 0) break;
            int bits = 8 + (cc & 0xF) * 8;
            if (hp + bits > hsEnd) break;
            if ((cc >> 4) == 3) return false;
            if (strictHeadShape && seen < want && (cc >> 4) != 4) return false;
            seen++;
            hp += bits;
            refsEnd = hp;
            if (seen != want) continue;
            int anchorCode = cc >> 4;
            if (anchorCode != 4 && anchorCode != 12) return false;
            insertAt = hp;
        }
        if (insertAt < 0 || refsEnd <= hsStart) return false;
        CopyBits(handles, src, hsStart, (int)(insertAt - hsStart));
        handles.WriteRC(0x30);
        CopyBits(handles, src, insertAt, (int)(refsEnd - insertAt));
        if (DebugTrace)
            System.Console.Error.WriteLine(
                $"PRE15HS h={rec.Handle:X} t={rec.TypeCode} AC1018→AC1015 nr={nr} " +
                $"源 {refsEnd - hsStart}位 → {handles.BitPosition}位（第 {want} 枚引用后补 (3,0)）");
        return true;
    }

    private static bool HandleStreamHasXdicSlot(byte[] stream, long endBit)
    {
        long limit = Math.Min(endBit, (long)stream.Length * 8);
        long pos = 0;
        while (pos + 8 <= limit)
        {
            int code = 0, size = 0;
            for (int k = 0; k < 4; k++) code = (code << 1) | (ReadSrcBit(stream, pos + k) ? 1 : 0);
            for (int k = 0; k < 4; k++) size = (size << 1) | (ReadSrcBit(stream, pos + 4 + k) ? 1 : 0);
            if (code == 3) return true;
            pos += 8 + size * 8L;
        }
        return false;
    }

    private static bool CarryScaleAc1021Down(BitWriter body, DwgObjectRecord rec, byte[] src,
        Target t, out BitWriter? handles)
    {
        handles = null;
        if (!DictionaryCarry.TryAc1021Spans(src, rec, out long dataEnd, out long ss,
                out bool hasStrings) || !hasStrings) return false;
        if (!DictionaryCarry.TryAc1021HandlesTail(rec, src, dataEnd, 1, out handles)) return false;
        return CarryScaleCore(body, rec, src, t, rec.CarryStartBit + 32, ss);
    }

    private static bool CarriesScaleAc1021ToAc1015Down(DwgObjectRecord rec, Target t)
        => t.Version == DwgVersion.AC1015
           && rec.SourceVersion == DwgVersion.AC1021
           && rec.LogicalType == TypeScale
           && rec.Scale is { Captured: true }
           && !rec.HasHandleStream;

    private static bool CarryScaleAc1021ToAc1015Down(BitWriter body, DwgObjectRecord rec,
        byte[] src, Target t, out BitWriter? handles)
    {
        handles = null;
        if (!DictionaryCarry.TryAc1021Spans(src, rec, out long dataEnd, out long ss,
                out bool hasStrings) || !hasStrings) return false;
        if (!DictionaryCarry.TryAc1021HandlesTail(rec, src, dataEnd, 1, out handles)) return false;
        bool srcHasXdic = HandleStreamHasXdicSlot(handles!.ToArray(), handles.BitPosition);
        if (!srcHasXdic) handles.WriteHandleRef(3, 0);
        if (!CarryScaleCore(body, rec, src, t, rec.CarryStartBit + 32, ss, ac1015: true))
            return false;
        if (DebugTrace)
            System.Console.Error.WriteLine(
                $"SCLDN15A21 h={rec.Handle:X} {rec.SourceVersion}→AC1015 name=\"{rec.Scale?.Name}\" " +
                $"body={body.BitPosition} hs={handles.BitPosition}位 srcXdic={srcHasXdic}");
        return true;
    }

    private static bool CarryScaleCore(BitWriter body, DwgObjectRecord rec, byte[] src, Target t,
        long start, long ssStart, bool ac1015 = false)
    {
        var cap = rec.Scale;
        if (cap is not { Captured: true }) return false;
        long nameIns = cap.DownNameInsertBit;
        long tailEnd = cap.DownTailEndBit;
        if (start <= 0 || ssStart <= start) return false;
        if (!(start < nameIns && nameIns < tailEnd && tailEnd == ssStart)) return false;
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
            long flagStart = br.BitPosition;
            br.ReadBS();
            if (br.BitPosition != nameIns) return false;
            br.ReadBD(); br.ReadBD(); br.ReadB();
            if (br.BitPosition != tailEnd) return false;

            CopyBits(body, src, start, (int)(eedStart - start));
            if (!TryWriteEedPreR2007Range(body, src, eedStart, eedLen))
                CopyBits(body, src, eedStart, eedLen);
            body.WriteBL((int)numReactors);
            if (!ac1015) body.WriteB(xdic);
            CopyBits(body, src, flagStart, (int)(nameIns - flagStart));
            body.WriteTV(cap.Name);
            CopyBits(body, src, nameIns, (int)(tailEnd - nameIns));
            if (DebugTrace)
                System.Console.Error.WriteLine(
                    $"SCLDOWN h={rec.Handle:X} {rec.SourceVersion}→{t.Version} name=\"{cap.Name}\" " +
                    $"own={start} flagStart={flagStart} nameIns={nameIns} tailEnd={tailEnd} " +
                    $"ss={ssStart} xdic={xdic} out={body.BitPosition}");
            return true;
        }
        catch
        {
            return false;
        }
    }
}
