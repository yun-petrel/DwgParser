using DwgParser.Core.IO;
using DwgParser.Core.Objects.Records;

namespace DwgParser.Core.Objects;

public static partial class DwgObjectStreamWriter
{
    internal const short TypeImageDefLogical = 543;

    internal static bool ImageDefDowngrades(DwgObjectRecord rec, Target t)
        => t.Version == DwgVersion.AC1018
           && rec.SourceVersion >= DwgVersion.AC1021
           && rec.LogicalType == TypeImageDefLogical
           && rec.ImageDef is { ValuesCommitted: true };

    private static int IdVar()
    {
        var s = System.Environment.GetEnvironmentVariable("IDVAR");
        return int.TryParse(s, out var v) ? v : 1;
    }

    internal static bool CarryImageDefDowngrade(BitWriter body, DwgObjectRecord rec, byte[] src,
        out long derivedHsBit)
    {
        derivedHsBit = 0;
        if (IdVar() == 0) return false;
        long start = rec.CarryStartBit;
        if (start <= 0 || rec.ImageDef is not { } cap) return false;
        bool srcHasRl = !rec.SourceVersion.IsR2010Plus();
        long dataBegin = srcHasRl ? start + 32 : start;
        long ss, dataEnd;
        BitReader? strBr;
        var br = new BitReader(src, 0);
        try
        {
            DwgObjectRecord.SeekTo(br, start);
            if (srcHasRl)
            {
                if (!CentralFrame.TryRlFrame(rec, rec.BodyStart, br, out ss, out dataEnd, out strBr))
                    return false;
            }
            else
            {
                if (!rec.HasHandleStream || rec.StringStreamStartBit <= 0
                    || rec.HandleStreamStartBit > (long)rec.BodyEnd * 8) return false;
                if (!CentralFrame.TryBotFrame(rec, br, out ss, out dataEnd, out strBr)) return false;
            }
            if (dataEnd <= ss || ss <= dataBegin) return false;

            if (br.ReadBL() is < 0 or > 8) return false;
            br.ReadB();
            long dsBit = -1;
            if (rec.SourceVersion >= DwgVersion.AC1027) { dsBit = br.BitPosition; br.ReadB(); }
            if (br.ReadBL() is < 0 or > 10) return false;
            br.Read2RD();
            long insertPos = br.BitPosition;
            br.ReadB();
            br.ReadRC();
            br.Read2RD();
            if (br.BitPosition != ss) return false;

            var sr = new BitReader(src, 0) { UnicodeStrings = true };
            DwgObjectRecord.SeekTo(sr, ss);
            long strBegin = sr.BitPosition;
            string path = sr.ReadTV();
            long consumed = sr.BitPosition - strBegin;
            if (path != cap.FilePath || path.Length > 255) return false;
            if (consumed != dataEnd - 17 - ss && consumed != dataEnd - 33 - ss) return false;

            if (dsBit >= 0)
            {
                CopyBits(body, src, dataBegin, (int)(dsBit - dataBegin));
                CopyBits(body, src, dsBit + 1, (int)(insertPos - dsBit - 1));
            }
            else CopyBits(body, src, dataBegin, (int)(insertPos - dataBegin));
            body.WriteTV(path);
            CopyBits(body, src, insertPos, (int)(ss - insertPos));
            derivedHsBit = dataEnd;
            if (DebugTrace)
                System.Console.Error.WriteLine(
                    $"IMDOWN h={rec.Handle:X} t={rec.TypeCode} {rec.SourceVersion}->AC1018 path='{path}' " +
                    $"data=[{dataBegin},{ss}) insert={insertPos} ds={dsBit} ss={ss} hs={dataEnd} " +
                    $"tv={consumed}->{8 + path.Length * 8}位 cv={cap.ClassVersion} out={body.BitPosition} " +
                    $"var={IdVar()}");
            return true;
        }
        catch { return false; }
    }

    internal static bool CarriesImageDefAc1015FromPreR2004(DwgObjectRecord rec, Target t)
        => t.Version == DwgVersion.AC1015
           && rec.SourceVersion >= DwgVersion.AC1018 && rec.SourceVersion < DwgVersion.AC1021
           && rec.LogicalType == TypeImageDefLogical
           && rec.HasHandleStream && rec.HandleStreamStartBit > 0;

    internal static bool CarryImageDefAc1015FromPreR2004(BitWriter body, DwgObjectRecord rec, byte[] src,
        out BitWriter? handles)
    {
        handles = null;
        if (IdVar() == 0) return false;
        long start = rec.CarryStartBit;
        long hsStart = rec.HandleStreamStartBit;
        if (start <= 0 || hsStart <= start) return false;
        try
        {
            long xdBit = FindAfterNumReactors(rec, src, PrefixHasRl(rec.SourceVersion), out _, out _);
            long dataBegin = PrefixHasRl(rec.SourceVersion) ? start + 32 : start;
            if (xdBit <= dataBegin || xdBit >= hsStart) return false;
            CopyBits(body, src, dataBegin, (int)(xdBit - dataBegin));
            CopyBits(body, src, xdBit + 1, (int)(hsStart - xdBit - 1));

            long hsEnd = LastMeaningfulHandleEnd(src, hsStart, (long)rec.BodyEnd * 8);
            if (hsEnd <= hsStart) return false;
            var hb = new BitWriter(16);
            CopyBits(hb, src, hsStart, (int)(hsEnd - hsStart));
            hb.WriteHandleRef(3, 0);
            handles = hb;
            if (DebugTrace)
                System.Console.Error.WriteLine(
                    $"IMDN15P h={rec.Handle:X} t={rec.TypeCode} {rec.SourceVersion}->AC1015 xd@{xdBit} " +
                    $"hsSrc=[{hsStart},{hsEnd}) hs={hb.BitPosition}位 body={body.BitPosition}");
            return true;
        }
        catch { return false; }
    }


    internal static bool CarriesImageDefAc1015Down(DwgObjectRecord rec, Target t)
        => t.Version == DwgVersion.AC1015
           && rec.SourceVersion >= DwgVersion.AC1021
           && rec.LogicalType == TypeImageDefLogical
           && rec.ImageDef is { ValuesCommitted: true };

    internal static bool CarryImageDefAc1015Down(BitWriter body, DwgObjectRecord rec, byte[] src,
        out BitWriter? handles)
    {
        handles = null;
        if (IdVar() == 0) return false;
        long start = rec.CarryStartBit;
        if (start <= 0 || rec.ImageDef is not { } cap) return false;
        bool srcHasRl = !rec.SourceVersion.IsR2010Plus();
        long dataBegin = srcHasRl ? start + 32 : start;
        long ss, dataEnd; BitReader? strBr;
        var br = new BitReader(src, 0);
        try
        {
            DwgObjectRecord.SeekTo(br, start);
            if (srcHasRl)
            { if (!CentralFrame.TryRlFrame(rec, rec.BodyStart, br, out ss, out dataEnd, out strBr)) return false; }
            else
            {
                if (!rec.HasHandleStream || rec.StringStreamStartBit <= 0 || rec.HandleStreamStartBit > (long)rec.BodyEnd * 8) return false;
                if (!CentralFrame.TryBotFrame(rec, br, out ss, out dataEnd, out strBr)) return false;
            }
            if (dataEnd <= ss || ss <= dataBegin) return false;

            long nrVal = br.ReadBL(); if (nrVal is < 0 or > 8) return false;
            long xdBit = br.BitPosition; br.ReadB();
            long dsBit = -1;
            if (rec.SourceVersion >= DwgVersion.AC1027) { dsBit = br.BitPosition; br.ReadB(); }
            if (br.ReadBL() is < 0 or > 10) return false;
            br.Read2RD();
            long insertPos = br.BitPosition;
            br.ReadB(); br.ReadRC(); br.Read2RD();
            if (br.BitPosition != ss) return false;

            var sr = new BitReader(src, 0) { UnicodeStrings = true };
            DwgObjectRecord.SeekTo(sr, ss);
            long strBegin = sr.BitPosition; string path = sr.ReadTV(); long consumed = sr.BitPosition - strBegin;
            if (path != cap.FilePath || path.Length > 255) return false;
            if (consumed != dataEnd - 17 - ss && consumed != dataEnd - 33 - ss) return false;

            long cur = dataBegin;
            foreach (var b in new[] { xdBit, dsBit })
                if (b >= cur && b < insertPos) { CopyBits(body, src, cur, (int)(b - cur)); cur = b + 1; }
            CopyBits(body, src, cur, (int)(insertPos - cur));
            body.WriteTV(path);
            CopyBits(body, src, insertPos, (int)(ss - insertPos));

            long hsStart = dataEnd, hsEnd = LastMeaningfulHandleEnd(src, hsStart, (long)rec.BodyEnd * 8);
            if (hsEnd <= hsStart) return false;
            var hbr = new BitReader(src, 0);
            long p = hsStart, keepTo = -1;
            for (int i = 0; i <= nrVal; i++)
            {
                if (p + 8 > hsEnd) return false;
                DwgObjectRecord.SeekTo(hbr, p);
                int cc = hbr.ReadRC(); if (cc == 0) return false;
                int bits = 8 + (cc & 0xF) * 8; if (p + bits > hsEnd) return false;
                p += bits; if (i == nrVal) keepTo = p;
            }
            if (keepTo < 0 || keepTo > hsEnd) return false;
            var hb = new BitWriter(16);
            CopyBits(hb, src, hsStart, (int)(keepTo - hsStart));
            hb.WriteHandleRef(3, 0);
            handles = hb;
            if (DebugTrace)
                System.Console.Error.WriteLine(
                    $"IMDN15 h={rec.Handle:X} t={rec.TypeCode} {rec.SourceVersion}->AC1015 path='{path}' nr={nrVal} " +
                    $"xd@{xdBit} ds={dsBit} insert={insertPos} hs={hb.BitPosition}位 body={body.BitPosition}");
            return true;
        }
        catch { return false; }
    }
}
