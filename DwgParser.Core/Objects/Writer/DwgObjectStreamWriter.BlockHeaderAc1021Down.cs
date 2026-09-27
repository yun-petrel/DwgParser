using DwgParser.Core.IO;

namespace DwgParser.Core.Objects;

public static partial class DwgObjectStreamWriter
{
    private static bool CarriesBlockHeaderAc1021Down(DwgObjectRecord rec, Target t)
        => t.Version == DwgVersion.AC1018
           && rec.SourceVersion == DwgVersion.AC1021
           && rec.LogicalType == TypeBlockHeader
           && rec.StringStreamStartBit > 0
           && rec.HandleStreamStartBit > 0
           && !rec.HasHandleStream;

    private static bool CarryBlockHeaderAc1021Down(BitWriter body, DwgObjectRecord rec, byte[] src,
        Target t, out BitWriter? handles)
    {
        handles = null;
        long start = rec.CarryStartBit + 32;
        long ss = rec.StringStreamStartBit;
        long hs = rec.HandleStreamStartBit;
        if (start <= 0 || ss <= start || hs <= ss) return false;
        if (!TryReadCentralString(src, ss, out string name) || name.Length == 0) return false;
        if (!DictionaryCarry.TryAc1021HandlesTail(rec, src, hs, 1, out handles)) return false;
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
            if (br.BitPosition >= ss) return false;

            CopyBits(body, src, start, (int)(eedStart - start));
            if (!TryWriteEedPreR2007Range(body, src, eedStart, eedLen))
                CopyBits(body, src, eedStart, eedLen);
            body.WriteBL((int)numReactors);
            body.WriteB(xdic);
            body.WriteTV(name, false);

            var xbr = new BitReader(src);
            DwgObjectRecord.SeekTo(xbr, br.BitPosition);
            int resolved = xbr.ReadBS();
            body.WriteB(true);
            body.WriteBS((short)resolved);
            body.WriteB(false);
            long afterBase = SkipBlockHeaderToBasePt(src, xbr.BitPosition, hs);
            if (afterBase <= 0 || !TryCarryBlockHeaderTailAc1018(body, src, xbr.BitPosition,
                    afterBase, hs, ss, out _))
                return false;

            if (DebugTrace)
                System.Console.Error.WriteLine(
                    $"BH21DN h={rec.Handle:X} AC1021→AC1018 name=\"{name}\" resolved={resolved} " +
                    $"own={start} eed={eedStart}+{eedLen} ss={ss} hs={hs} out={body.BitPosition} " +
                    $"hsBits={handles!.BitPosition}");
            return true;
        }
        catch
        {
            handles = null;
            return false;
        }
    }
}
