using System.Text;

namespace DwgParser.Core.Strings;

public enum StringKind
{
    Text,
    FontRef,
    Path,
    ClassName,
    SymbolTable,
}

public sealed record ExtractedString(
    long Offset,
    string Value,
    StringKind Kind,
    bool FromDecompressed);

public static class StringExtractor
{
    public const int MinLength = 3;

    public static List<ExtractedString> Extract(byte[] data, long baseOffset = 0, bool fromDecompressed = false)
    {
        var results = new List<ExtractedString>();
        results.AddRange(ScanAscii(data, baseOffset, fromDecompressed));
        results.AddRange(ScanUtf16(data, baseOffset, fromDecompressed));
        return results;
    }

    private static IEnumerable<ExtractedString> ScanAscii(byte[] data, long baseOffset, bool dec)
    {
        var results = new List<ExtractedString>();
        var sb = new StringBuilder();
        int start = 0;
        for (int i = 0; i <= data.Length; i++)
        {
            bool printable = i < data.Length && IsPrintable(data[i]);
            if (printable)
            {
                if (sb.Length == 0) start = i;
                sb.Append((char)data[i]);
            }
            else
            {
                bool hasHigh = false;
                foreach (var ch in sb.ToString())
                    if (ch > 0x7E) { hasHigh = true; break; }
                int threshold = hasHigh ? MinLength + 3 : MinLength;

                if (sb.Length >= threshold)
                    results.Add(Classify(baseOffset + start, sb.ToString(), dec));
                sb.Clear();
            }
        }
        return results;
    }

    private static IEnumerable<ExtractedString> ScanUtf16(byte[] data, long baseOffset, bool dec)
    {
        var results = new List<ExtractedString>();
        int i = 0;
        while (i + 1 < data.Length)
        {
            int start = i;
            var sb = new StringBuilder();
            while (i + 1 < data.Length)
            {
                ushort c = (ushort)(data[i] | (data[i + 1] << 8));
                if (c == 0 || c < 0x20 || c == 0xFFFF || (c >= 0x7F && c < 0xA0))
                    break;
                sb.Append((char)c);
                i += 2;
            }
            if (sb.Length >= MinLength && LooksLikeUtf16Run(data, start, sb.Length * 2))
                results.Add(Classify(baseOffset + start, sb.ToString(), dec));
            i = Math.Max(i + 2, start + 2);
        }
        return results;
    }

    private static bool LooksLikeUtf16Run(byte[] data, int start, int len)
    {
        for (int k = start + 1; k < Math.Min(start + len, data.Length); k += 2)
            if (data[k] != 0)
                return false;
        return true;
    }

    public static ExtractedString Classify(long off, string value, bool dec)
        => new(off, value, ClassifyValue(value), dec);

    public static StringKind ClassifyValue(string v)
    {
        if (v.StartsWith("AcDb", StringComparison.Ordinal) ||
            v.StartsWith("Acad", StringComparison.Ordinal)) return StringKind.ClassName;
        if (v.EndsWith(".shx", StringComparison.OrdinalIgnoreCase) ||
            v.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase)) return StringKind.FontRef;
        if (v.Contains(':') || v.Contains('/')) return StringKind.Path;
        if (v.StartsWith("*Model_Space", StringComparison.OrdinalIgnoreCase) ||
            v.StartsWith("*Paper_Space", StringComparison.OrdinalIgnoreCase)) return StringKind.SymbolTable;
        return StringKind.Text;
    }

    private static bool IsPrintable(byte b)
        => b >= 0x20 && b <= 0x7E || b >= 0xA0;
}
