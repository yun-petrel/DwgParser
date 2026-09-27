using System.Text;

namespace DwgParser.Core.Compression;

public sealed record LocatedChunk(int Offset, byte[] Decoded, int MatchedNames);

public static class ChunkLocator
{
    private static readonly string[] Markers =
    {
        "AcDb:Header", "AcDb:Classes", "AcDb:Handles", "AcDb:Objects",
        "AcDb:AcDbObjects", "AcDb:ObjFreeSpace", "AcDb:Template",
        "AcDb:AuxHeader", "AcDb:AppInfo", "AcDb:Preview", "AcDb:FileDepList",
        "AcDb:RevHistory", "AcDb:SummaryInfo",
    };

    private const int ProbeMaxLength = 256 * 1024;
    private const int MinDecodeLength = 32;

    public static List<LocatedChunk> FindSystemChunks(byte[] file)
    {
        var results = new List<LocatedChunk>();
        int limit = Math.Min(file.Length, 8 * 1024 * 1024);

        for (int off = 0x40; off + MinDecodeLength < limit; off++)
        {
            int first = file[off];
            if (first >= 0x20)
                continue;

            var decoded = RsDecoder.DecodeLenient(file, off, ProbeMaxLength);
            if (decoded.Length < MinDecodeLength)
                continue;

            int matches = CountMarkers(decoded);
            if (matches > 0)
                results.Add(new LocatedChunk(off, decoded, matches));

            if (decoded.Length > 64)
                off += Math.Max(16, decoded.Length / 8);
        }

        return results.OrderByDescending(c => c.MatchedNames).Take(24).ToList();
    }

    public static int CountMarkers(byte[] data)
    {
        string text = Encoding.ASCII.GetString(data.AsSpan(0, Math.Min(data.Length, 64 * 1024)));
        int count = 0;
        foreach (var m in Markers)
            if (text.Contains(m, StringComparison.Ordinal))
                count++;
        return count;
    }
}
