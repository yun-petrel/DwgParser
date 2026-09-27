using DwgParser.Core.IO;

namespace DwgParser.Core.Objects;

public static partial class DwgObjectStreamWriter
{
    private static bool CarriesLayerAc1018ToAc1015(DwgObjectRecord rec, Target t)
        => t.Version == DwgVersion.AC1015
           && rec.SourceVersion == DwgVersion.AC1018
           && rec.LogicalType == TypeLayer
           && rec.Layer is { ColorFieldStartBit: > 0, ColorFieldEndBit: > 0 }
           && rec.HasHandleStream && rec.HandleStreamStartBit > 0;

    private static bool CarryLayerAc1018ToAc1015(BitWriter body, DwgObjectRecord rec, byte[] src,
        out BitWriter? handles)
    {
        handles = null;
        var cap = rec.Layer!;
        long start = rec.CarryStartBit + 32;
        long cs = cap.ColorFieldStartBit, ce = cap.ColorFieldEndBit, hs = rec.HandleStreamStartBit;
        if (start <= 0 || cs <= start || ce <= cs || hs < ce) return false;
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
            if (headEnd > cs) return false;

            CopyBits(body, src, start, (int)(eedStart - start));
            if (!TryWriteEedPreR2007Range(body, src, eedStart, eedLen))
                CopyBits(body, src, eedStart, eedLen);
            body.WriteBL(nr);
            CopyBits(body, src, headEnd, (int)(cs - headEnd));
            var cb = new BitReader(src, 0);
            DwgObjectRecord.SeekTo(cb, cs);
            DwgObjectReader.ReadCmc(cb, out uint trueColor, out _, out _);
            if (cb.BitPosition != ce) return false;
            if ((trueColor >> 24) is < 0xC0 or > 0xC8) return false;
            body.WriteBS(FoldCmcToPreR2004Index(trueColor));
            CopyBits(body, src, ce, (int)(hs - ce));

            long hsEnd = LastMeaningfulHandleEnd(src, hs, (long)rec.BodyEnd * 8);
            if (hsEnd <= hs) return false;
            var hw = new BitWriter(16);
            CopyBits(hw, src, hs, (int)(hsEnd - hs));
            if (!HandleStreamHasXdicSlot(hw.ToArray(), hw.BitPosition)) hw.WriteRC(0x30);
            handles = hw;
            if (DebugTrace)
                System.Console.Error.WriteLine(
                    $"LAYDN1015 h={rec.Handle:X} AC1018→AC1015 折色 CMC 0x{trueColor:X8} " +
                    $"body={body.BitPosition} hs={hw.BitPosition}位");
            return true;
        }
        catch { return false; }
    }
}
