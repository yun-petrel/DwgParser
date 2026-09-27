using DwgParser.Core.Format;
using DwgParser.Core.IO;
using DwgParser.Core.Objects;

namespace DwgParser.Cli;

public static partial class Program
{
    private const string ColumnInfoMarker = "ACAD_MTEXT_COLUMN_INFO";
    private const short WireMText = 0x2C;

    internal sealed class EedAuditResult
    {
        public int Sliced;
        public int Chunks;
        public int Unregistered;
        public int Abandon;
        public int ColMText;
        public int ColChunks;
        public int Bb01;
        public int Bb00;
        public int BbSignedTrap;
        public readonly List<string> ColHandles = new();
        public readonly List<string> AbandonLines = new();
    }

    private static void EedAuditReport(string path, DwgDocument doc)
    {
        var objs = doc.GetObjects();
        var t = EedAuditOf(objs);
        foreach (var line in t.AbandonLines) Console.WriteLine(line);
        Console.WriteLine($"EEDAUDIT file={Path.GetFileName(path)} ver={doc.Version} " +
                          $"seen={objs.ObjectsSeen} decoded={objs.ObjectsDecoded} errors={objs.Errors} " +
                          $"records={objs.Records.Count}");
        Console.WriteLine($"eed sliced={t.Sliced} chunks={t.Chunks} unregistered={t.Unregistered} " +
                          $"abandon={t.Abandon} colInfoMText={t.ColMText} colChunks={t.ColChunks} " +
                          $"bb01={t.Bb01} bb00={t.Bb00} bbSignedTrap={t.BbSignedTrap}");
        if (t.ColHandles.Count > 0)
            Console.WriteLine("eed-col-handles " + string.Join(",", t.ColHandles.Take(12)) +
                              (t.ColHandles.Count > 12 ? $"…(+{t.ColHandles.Count - 12})" : ""));
    }

    internal static EedAuditResult EedAuditOf(DwgObjectReader.WalkResult objs)
    {
        var data = objs.SourceData ?? Array.Empty<byte>();
        var t = new EedAuditResult();
        foreach (var rec in objs.Records.OrderBy(r => r.Handle))
        {
            if (rec.EedStartBit <= 0) continue;
            if (rec.EedBitLen > 0)
            {
                t.Sliced++;
                t.Chunks += rec.EedFields.Count;
                bool recHasCol = false;
                foreach (var fld in rec.EedFields)
                {
                    if (!HasColumnInfoMarker(fld.Payload)) continue;
                    t.ColChunks++;
                    recHasCol = true;
                    int bb = PrefixAt(data, fld.StartBit);
                    if (bb == 1) t.Bb01++;
                    else if (bb == 0) t.Bb00++;
                    if (fld.PayloadSize is >= 128 and <= 255) t.BbSignedTrap++;
                }
                if (recHasCol && rec.LogicalType == WireMText)
                {
                    t.ColMText++;
                    t.ColHandles.Add(rec.Handle.ToString("X"));
                }
                continue;
            }
            string? fail = TryWalkEed(data, rec);
            if (fail is null) { t.Unregistered++; continue; }
            t.Abandon++;
            t.AbandonLines.Add($"eed-abandon h={rec.Handle:X} t={rec.TypeCode} lt=0x{rec.LogicalType:X} " +
                               $"v={rec.SourceVersion} kind={rec.Kind} eedStart={rec.EedStartBit} reason={fail}");
        }
        return t;
    }

    private static bool HasColumnInfoMarker(byte[] payload)
    {
        if (payload.Length == 0) return false;
        return IndexOf(payload, System.Text.Encoding.ASCII.GetBytes(ColumnInfoMarker)) >= 0
            || IndexOf(payload, System.Text.Encoding.Unicode.GetBytes(ColumnInfoMarker)) >= 0;
    }

    private static int PrefixAt(byte[] data, long bit)
    {
        int byteIndex = (int)(bit >> 3);
        int shift = 6 - (int)(bit & 7);
        if (byteIndex < 0 || byteIndex >= data.Length || shift < 0 || shift > 6) return -1;
        return (data[byteIndex] >> shift) & 3;
    }

    private static string? TryWalkEed(byte[] data, DwgObjectRecord rec)
    {
        long frameEndBit = (long)(rec.FrameEnd > 0 ? rec.FrameEnd : rec.BodyEnd) * 8;
        var br = new BitReader(data, 0);
        br.SetPosition((int)(rec.EedStartBit / 8), (int)(rec.EedStartBit % 8));
        try
        {
            for (int guard = 0; guard < 4096; guard++)
            {
                long at = br.BitPosition;
                int size = br.ReadBS();
                if (size < 0)
                    return $"size<0 @{at}（BB=00 的裸短 ≥0x8000，或有符号读 BB=01 的 8 位长度）";
                if (size == 0) return null;
                br.ReadHandle();
                if (br.BitPosition + (long)size * 8 > frameEndBit)
                    return $"size={size} @{at} 越帧尾 {frameEndBit}";
                if (br.BitPosition + (long)size * 8 > (long)data.Length * 8)
                    return $"size={size} @{at} 越缓冲区尾 {data.Length * 8}";
                for (int i = 0; i < size && !br.EndOfStream; i++) br.ReadRC();
                if (br.EndOfStream) return $"载荷越流尾 @{at} size={size}";
            }
            return "4096 段未见终止符";
        }
        catch (EndOfStreamException) { return $"越缓冲区尾 @{br.BitPosition}"; }
    }

    private static int IndexOf(byte[] hay, byte[] needle)
    {
        for (int i = 0; i + needle.Length <= hay.Length; i++)
        {
            int j = 0;
            while (j < needle.Length && hay[i + j] == needle[j]) j++;
            if (j == needle.Length) return i;
        }
        return -1;
    }
}
