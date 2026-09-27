using DwgParser.Core.IO;

namespace DwgParser.Core.Objects;

public static partial class DwgObjectStreamWriter
{
    private static bool CarriesMLineStyleAc1018ToAc1015(DwgObjectRecord rec, Target t)
        => t.Version == DwgVersion.AC1015
           && rec.SourceVersion == DwgVersion.AC1018
           && rec.LogicalType == TypeMLineStyle
           && rec.MLineStyle is { CmcSpans.Count: >= 1 }
           && rec.HasHandleStream && rec.HandleStreamStartBit > 0;

    private static bool CarryMLineStyleAc1018ToAc1015(BitWriter body, DwgObjectRecord rec, byte[] src,
        out BitWriter? handles)
    {
        handles = null;
        var cap = rec.MLineStyle!;
        var cmcs = cap.CmcSpans;
        long start = rec.CarryStartBit + 32;
        long hs = rec.HandleStreamStartBit;
        if (start <= 0 || hs <= start || cmcs[0].Start <= start || cmcs[^1].End > hs) return false;
        try
        {
            var br = new BitReader(src, 0);
            DwgObjectRecord.SeekTo(br, start);
            br.ReadHandle();
            long eedStart = br.BitPosition;
            if (!SkipEedWriter(br)) return false;
            int eedLen = (int)(br.BitPosition - eedStart);
            int nr = br.ReadBL();
            if (nr is < 0 or > 8) return false;
            br.ReadB();
            long headEnd = br.BitPosition;
            if (headEnd > cmcs[0].Start) return false;

            CopyBits(body, src, start, (int)(eedStart - start));
            if (!TryWriteEedPreR2007Range(body, src, eedStart, eedLen))
                CopyBits(body, src, eedStart, eedLen);
            body.WriteBL(nr);
            long cur = headEnd;
            foreach (var (cs, ce) in cmcs)
            {
                if (cs < cur || ce <= cs || ce > hs) return false;
                CopyBits(body, src, cur, (int)(cs - cur));
                if (!WriteFoldedCmc(body, src, cs, ce)) return false;
                cur = ce;
            }
            CopyBits(body, src, cur, (int)(hs - cur));

            long hsEnd = LastMeaningfulHandleEnd(src, hs, (long)rec.BodyEnd * 8);
            if (hsEnd <= hs) return false;
            var hw = new BitWriter(16);
            CopyBits(hw, src, hs, (int)(hsEnd - hs));
            if (!HandleStreamHasXdicSlot(hw.ToArray(), hw.BitPosition)) hw.WriteRC(0x30);
            handles = hw;
            if (DebugTrace)
                System.Console.Error.WriteLine(
                    $"MLSDN1015 h={rec.Handle:X} AC1018→AC1015 折 {cmcs.Count} 枚 CMC " +
                    $"body={body.BitPosition} hs={hw.BitPosition}位");
            return true;
        }
        catch { return false; }
    }
}

public static partial class DwgObjectStreamWriter
{
    private static bool TryR2010PlusSpan(byte[] src, long start, long hs, out long ssStart)
    {
        ssStart = 0;
        long flagBit = hs - 1;
        if (flagBit < start || flagBit >= (long)src.Length * 8) return false;
        var fr = new BitReader(src, 0);
        DwgObjectRecord.SeekTo(fr, flagBit);
        if (!fr.ReadB()) return false;
        DwgObjectRecord.SeekTo(fr, hs - 17);
        uint dataSize = fr.ReadRS();
        int sizeFieldBits = 17;
        if ((dataSize & 0x8000) != 0)
        {
            DwgObjectRecord.SeekTo(fr, hs - 33);
            dataSize = (dataSize & 0x7FFF) | ((uint)fr.ReadRS() << 15);
            sizeFieldBits = 33;
        }
        ssStart = hs - sizeFieldBits - dataSize;
        return ssStart > start && ssStart < hs;
    }

    private static bool CarriesMLineStyleR2010ToAc1015(DwgObjectRecord rec, Target t)
        => t.Version == DwgVersion.AC1015
           && rec.SourceVersion >= DwgVersion.AC1021
           && rec.LogicalType == TypeMLineStyle
           && rec.MLineStyle is { Captured: true }
           && (rec.HandleStreamStartBit > 0 || rec.SourceVersion == DwgVersion.AC1021);

    private static bool CarryMLineStyleR2010ToAc1015(BitWriter body, DwgObjectRecord rec, byte[] src,
        Target t, LtypeTable? ltypeTable, out BitWriter? handles)
    {
        handles = null;
        var cap = rec.MLineStyle!;
        long start = rec.CarryStartBit;
        long hs = rec.HandleStreamStartBit;
        bool ac1021 = rec.SourceVersion == DwgVersion.AC1021;
        if (start <= 0) return false;
        long ss;
        if (ac1021)
        {
            if (!DictionaryCarry.TryAc1021Spans(src, rec, out long de, out ss, out bool hasStr)
                || !hasStr) return false;
            hs = de;
        }
        else if (!TryR2010PlusSpan(src, start, hs, out ss)) return false;
        if (hs <= start || ss <= start || ss >= hs) return false;
        try
        {
            var br = new BitReader(src, 0);
            DwgObjectRecord.SeekTo(br, start);
            long frameStart = start;
            if (ac1021) { br.ReadRL(); frameStart = br.BitPosition; }
            br.ReadHandle();
            long eedStart = br.BitPosition;
            if (!SkipEedWriter(br)) return false;
            int eedLen = (int)(br.BitPosition - eedStart);
            int nr = br.ReadBL();
            if (nr is < 0 or > 8) return false;
            br.ReadB();
            if (rec.SourceVersion >= DwgVersion.AC1027) br.ReadB();
            long numericStart = br.BitPosition;
            if (numericStart > ss) return false;

            string name, desc;
            {
                var sr = new BitReader(src) { UnicodeStrings = true };
                DwgObjectRecord.SeekTo(sr, ss);
                name = sr.ReadTV();
                desc = sr.ReadTV();
                if (sr.BitPosition + 17 != hs) return false;
            }
            if (name.Length == 0 || name.Length > 255 || desc.Length > 255) return false;
            if (name != cap.Name || desc != cap.Description) return false;

            bool srcHasLt = rec.SourceVersion < DwgVersion.AC1032;
            if (!srcHasLt && ltypeTable is null) return false;

            CopyBits(body, src, frameStart, (int)(eedStart - frameStart));
            if (!TryWriteEedPreR2007Range(body, src, eedStart, eedLen))
                CopyBits(body, src, eedStart, eedLen);
            body.WriteBL(nr);
            body.WriteTV(name);
            body.WriteTV(desc);

            var nb = new BitReader(src, 0);
            DwgObjectRecord.SeekTo(nb, numericStart);
            long cur = numericStart;
            nb.ReadBS();
            if (!EmitUpTo(body, src, nb, ref cur, ss)) return false;
            if (!FoldCmcAt(body, nb, ref cur, ss)) return false;
            nb.ReadBD(); nb.ReadBD();
            if (!EmitUpTo(body, src, nb, ref cur, ss)) return false;
            int numLines = nb.ReadRC();
            if (!EmitUpTo(body, src, nb, ref cur, ss)) return false;
            if (numLines is < 0 or > 256) return false;
            for (int i = 0; i < numLines; i++)
            {
                nb.ReadBD();
                if (!EmitUpTo(body, src, nb, ref cur, ss)) return false;
                if (!FoldCmcAt(body, nb, ref cur, ss)) return false;
                if (srcHasLt)
                {
                    nb.ReadBS();
                    if (!EmitUpTo(body, src, nb, ref cur, ss)) return false;
                }
                else
                {
                    body.WriteBS((short)MLineStyleLineIndex(src, rec, i, ltypeTable!));
                }
            }
            if (nb.BitPosition != ss) return false;
            EmitRange(body, src, cur, (int)(ss - cur));

            var hw = new BitWriter(16);
            if (!srcHasLt)
            {
                var hr = new BitReader(src, 0);
                DwgObjectRecord.SeekTo(hr, hs);
                var hdr = new BitReader(src, 0);
                DwgObjectRecord.SeekTo(hdr, start);
                hdr.ReadHandle();
                if (!SkipEedWriter(hdr)) return false;
                int prefixCount = 1 + (int)hdr.ReadBL() + (hdr.ReadB() ? 0 : 1);
                hdr.ReadB();
                for (int k = 0; k < prefixCount; k++)
                {
                    long rs = hr.BitPosition;
                    hr.ReadHandle();
                    CopyBits(hw, src, rs, (int)(hr.BitPosition - rs));
                }
            }
            else
            {
                long hsEnd = LastMeaningfulHandleEnd(src, hs, (long)rec.BodyEnd * 8);
                if (hsEnd <= hs) return false;
                CopyBits(hw, src, hs, (int)(hsEnd - hs));
            }
            if (!HandleStreamHasXdicSlotAc1015(hw.ToArray(), hw.BitPosition)) hw.WriteRC(0x30);
            handles = hw;
            if (DebugTrace)
                System.Console.Error.WriteLine(
                    $"MLSDN15 h={rec.Handle:X} {rec.SourceVersion}→AC1015 name='{name}' nr={nr} " +
                    $"lines={numLines} ltInData={srcHasLt} body={body.BitPosition} hs={hw.BitPosition}");
            return true;
        }
        catch { return false; }
    }

    private static bool EmitUpTo(BitWriter body, byte[] src, BitReader nb, ref long cur, long ss)
    {
        long now = nb.BitPosition;
        if (now < cur || now > ss) return false;
        if (now > cur) EmitRange(body, src, cur, (int)(now - cur));
        cur = now;
        return true;
    }

    private static void EmitRange(BitWriter body, byte[] src, long from, int len)
    {
        if (len > 0) CopyBits(body, src, from, len);
    }

    private static bool FoldCmcAt(BitWriter body, BitReader nb, ref long cur, long ss)
    {
        long cs = nb.BitPosition;
        var tri = DwgObjectReader.ReadCmcTriple(nb);
        long ce = nb.BitPosition;
        if (ce <= cs || ce > ss) return false;
        if (tri.Idx is < 0 or > 256 || (tri.Rgb >> 24) is < 0xC0 or > 0xC8) return false;
        body.WriteBS(FoldCmcToPreR2004Index(tri.Rgb));
        cur = ce;
        return true;
    }

    private static bool HandleStreamHasXdicSlotAc1015(byte[] stream, long endBit)
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
}
