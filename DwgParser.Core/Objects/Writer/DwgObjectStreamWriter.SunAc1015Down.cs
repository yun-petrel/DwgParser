using DwgParser.Core.IO;

namespace DwgParser.Core.Objects;

public static partial class DwgObjectStreamWriter
{
    private static bool CarriesSunAc1018ToAc1015(DwgObjectRecord rec, Target t)
        => t.Version == DwgVersion.AC1015
           && rec.SourceVersion == DwgVersion.AC1018
           && IsSunRecord(rec);

    private static bool CarrySunAc1018ToAc1015(BitWriter body, DwgObjectRecord rec, byte[] src,
        out BitWriter? handles)
    {
        handles = null;
        long start = rec.CarryStartBit + 32;
        if (rec.CarryStartBit <= 0 || rec.BodyEnd <= rec.BodyStart) return false;
        if (!TrySourceDataEnd(src, rec, out long dataEnd)) return false;
        if (dataEnd <= start) return false;
        try
        {
            var br = new BitReader(src, 0);
            DwgObjectRecord.SeekTo(br, start);
            br.ReadHandle();
            long eedStart = br.BitPosition;
            if (!SkipEedWriter(br)) return false;
            int eedLen = (int)(br.BitPosition - eedStart);
            int nr = br.ReadBL();
            if (nr is < 0 or > 1000) return false;
            long xdicBit = br.BitPosition;
            bool xdicMissing = br.ReadB();
            _ = br.ReadBL();
            br.ReadB();
            long cmcStart = br.BitPosition;
            _ = DwgObjectReader.ReadCmc(br, out uint trueColor, out string cName, out string cBook);
            long cmcEnd = br.BitPosition;
            if (cName.Length > 0 || cBook.Length > 0) return false;
            if (cmcEnd <= cmcStart || cmcEnd > dataEnd) return false;
            if ((trueColor >> 24) is < 0xC0 or > 0xC8) return false;
            short folded = FoldCmcToPreR2004Index(trueColor);
            br.ReadBD();
            br.ReadB();
            _ = br.ReadBL(); _ = br.ReadBL();
            br.ReadB();
            _ = br.ReadBL();
            _ = br.ReadBS();
            br.ReadRC();
            if (br.BitPosition != dataEnd) return false;

            long hs = dataEnd;
            long hsEnd = LastMeaningfulHandleEnd(src, hs, (long)rec.BodyEnd * 8);
            if (hsEnd <= hs) return false;
            var hbr = new BitReader(src, 0);
            int refs = 0;
            for (long p = hs; p + 8 <= hsEnd;)
            {
                DwgObjectRecord.SeekTo(hbr, p);
                int code = hbr.ReadRC();
                int refBits = 8 + (code & 0xF) * 8;
                if (p + refBits > hsEnd) return false;
                refs++;
                p += refBits;
            }
            if (refs != (xdicMissing ? 1 : 2) + nr) return false;

            CopyBits(body, src, start, (int)(eedStart - start));
            CopyBits(body, src, eedStart, eedLen);
            body.WriteBL(nr);
            CopyBits(body, src, xdicBit + 1, (int)(cmcStart - xdicBit - 1));
            body.WriteBS(folded);
            CopyBits(body, src, cmcEnd, (int)(dataEnd - cmcEnd));

            var hw = new BitWriter(16);
            CopyBits(hw, src, hs, (int)(hsEnd - hs));
            if (xdicMissing) hw.WriteHandleRef(3, 0);
            handles = hw;

            if (DebugTrace)
                System.Console.Error.WriteLine(
                    $"SUNDN15 h={rec.Handle:X} t={rec.TypeCode} SUN AC1018→AC1015 nr={nr} " +
                    $"xdicMiss={xdicMissing} CMC 0x{trueColor:X8}→BS({folded}) 数据区 {dataEnd - start}→" +
                    $"{dataEnd - start - 1 - (cmcEnd - cmcStart) + BsWidthOf(folded)}位 " +
                    $"body={body.BitPosition} hs={hw.BitPosition}@{hs}");
            return true;
        }
        catch { return false; }
    }

    private static int BsWidthOf(short v)
        => v == 0 ? 2 : (v is >= 1 and <= 255 ? 10 : 18);
}
