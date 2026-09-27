using DwgParser.Core.IO;

namespace DwgParser.Core.Format;

public static class HandleBitsReader
{
    public static ulong ReadHandleBits(this ByteReader br, int bits)
    {
        ulong result = 0;
        int bitsRemaining = bits;
        while (bitsRemaining > 0)
        {
            if (br.Position >= br.Length) break;
            byte b = br.ReadByte();
            int take = Math.Min(bitsRemaining, 8);
            result <<= take;
            result |= (byte)(b >> (8 - take));
            bitsRemaining -= take;
        }
        return result;
    }
}
