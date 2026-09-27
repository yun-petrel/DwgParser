using DwgParser.Core.IO;

namespace DwgParser.Core.Objects;

public static partial class DwgObjectReader
{
    internal static List<ulong>? TryReadOwnedVectorR2004(byte[] src, long hs, long end, int numOwned)
    {
        if (numOwned < 0 || numOwned > 4096) return null;
        if (src.Length == 0 || hs <= 0 || end <= hs || end > (long)src.Length * 8) return null;
        if (numOwned == 0) return new List<ulong>();
        try
        {
            var br = new BitReader(src, 0);
            int prevCode = -1;
            for (long hp = hs; hp + 8 <= end;)
            {
                DwgObjectRecord.SeekTo(br, hp);
                int cc = br.ReadRC();
                int n = cc & 0xF, code = cc >> 4;
                int bits = 8 + n * 8;
                if (hp + bits > end) break;
                if (code == 0 && n == 0) break;
                if (code == 3 && prevCode == 5)
                {
                    var vec = new List<ulong>(numOwned);
                    long q = hp + bits;
                    for (int i = 0; i < numOwned && q + 8 <= end; i++)
                    {
                        DwgObjectRecord.SeekTo(br, q);
                        int c2 = br.ReadRC();
                        int n2 = c2 & 0xF, code2 = c2 >> 4;
                        int b2 = 8 + n2 * 8;
                        if (q + b2 > end) return null;
                        if (code2 != 3) return null;
                        ulong v = 0;
                        for (int k = 0; k < n2; k++) v = (v << 8) | br.ReadRC();
                        vec.Add(v);
                        q += b2;
                    }
                    return vec.Count == numOwned ? vec : null;
                }
                prevCode = code;
                hp += bits;
            }
        }
        catch (Exception ex) when (ex is EndOfStreamException or IndexOutOfRangeException
                                      or ArgumentException or OverflowException)
        {
            return null;
        }
        return null;
    }
}
