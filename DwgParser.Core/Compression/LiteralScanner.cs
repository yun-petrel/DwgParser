using System.Text;

namespace DwgParser.Core.Compression;

public sealed record LiteralHit(int Offset, string Text);

public static class LiteralScanner
{
    public static List<LiteralHit> Scan(byte[] data, int minLength = 3)
    {
        var hits = new List<LiteralHit>();
        var sb = new StringBuilder();
        int start = -1;

        int i = 0;
        while (i < data.Length)
        {
            int ctrl = data[i];
            if (ctrl < 0x20 && i + 1 + ctrl + 3 <= data.Length)
            {
                int litStart = i + 1;
                int litLen = ctrl + 3;

                int ascii = 0;
                for (int k = 0; k < litLen; k++)
                {
                    byte b = data[litStart + k];
                    if (b >= 0x20 && b <= 0x7E) ascii++;
                }

                bool usable = litLen >= minLength && ascii == litLen;
                if (usable)
                {
                    if (start < 0) start = i;
                    sb.Append(Encoding.ASCII.GetString(data, litStart, litLen));
                    i = litStart + litLen;
                    continue;
                }
            }

            Flush(hits, sb, ref start);
            i++;
        }
        Flush(hits, sb, ref start);
        return hits;
    }

    private static void Flush(List<LiteralHit> hits, StringBuilder sb, ref int start)
    {
        if (sb.Length > 0 && start >= 0)
            hits.Add(new LiteralHit(start, sb.ToString()));
        sb.Clear();
        start = -1;
    }
}
