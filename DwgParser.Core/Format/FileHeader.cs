using DwgParser.Core.IO;

namespace DwgParser.Core.Format;

public sealed class FileHeader
{
    public const int HeaderSizeR2004Plus = 0x80;

    public byte[] Raw { get; }

    public string VersionString { get; }
    public DwgVersion Version { get; }
    public byte MaintenanceRelease { get; }

    public uint LocatorMarker { get; }

    public uint SystemAreaAddress { get; }

    public uint? PreviewAddress { get; }

    public ushort CodepageGuess { get; }

    public FileHeader(byte[] raw)
    {
        Raw = raw;
        VersionString = System.Text.Encoding.ASCII.GetString(raw, 0, Math.Min(6, raw.Length));
        Version = ParseVersion(VersionString);
        if (raw.Length > 6)
            MaintenanceRelease = raw[6];

        var r = new ByteReader(raw);
        if (Version.IsR2004Plus() && raw.Length >= 0x30)
        {
            LocatorMarker = r.ReadUInt32At(0x24);
            if (LocatorMarker == 0)
                LocatorMarker = r.ReadUInt32At(0x28);
            SystemAreaAddress = r.ReadUInt32At(0x2C);
            if (SystemAreaAddress == 0)
                SystemAreaAddress = r.ReadUInt32At(0x28);

            PreviewAddress = FindPreviewAddress(r);
        }
        if (raw.Length >= 0x16)
            CodepageGuess = GuessCodepage(raw);
    }

    private static DwgVersion ParseVersion(string s) => s switch
    {
        "AC1006" => DwgVersion.AC1006,
        "AC1009" => DwgVersion.AC1009,
        "AC1012" => DwgVersion.AC1012,
        "AC1014" => DwgVersion.AC1014,
        "AC1015" => DwgVersion.AC1015,
        "AC1018" => DwgVersion.AC1018,
        "AC1021" => DwgVersion.AC1021,
        "AC1024" => DwgVersion.AC1024,
        "AC1027" => DwgVersion.AC1027,
        "AC1032" => DwgVersion.AC1032,
        _ => DwgVersion.Unknown,
    };

    private uint? FindPreviewAddress(ByteReader r)
    {
        foreach (var off in new[] { 0x0D, 0x0C })
        {
            if (off + 4 > Raw.Length) continue;
            uint v = r.ReadUInt32At(off);
            if (v > 0 && v < Raw.Length && v > 0x80)
                return v;
        }
        return null;
    }

    private static ushort GuessCodepage(byte[] raw)
    {
        if (raw.Length < 0x16) return 0;
        var r = new ByteReader(raw);
        ushort a = r.ReadUInt16At(0x11);
        ushort b = r.ReadUInt16At(0x13);
        if (IsPlausibleCodepage(b)) return b;
        if (IsPlausibleCodepage(a)) return a;
        return 0;
    }

    public static System.Text.Encoding? LegacyEncodingOf(byte[] raw)
    {
        ushort cp = raw.Length < 0x16 ? (ushort)0 : GuessCodepage(raw);
        int win = cp switch
        {
            30 => 1252,
            31 => 936,
            32 => 950,
            38 => 932,
            39 => 936,
            40 => 949,
            41 => 950,
            28 => 1250,
            29 => 1251,
            44 => 1258,
            25 => 949,
            26 => 1361,
            27 => 866,
            _ => 0,
        };
        if (win == 0) return null;
        try
        {
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
            return System.Text.Encoding.GetEncoding(win);
        }
        catch
        {
            return null;
        }
    }

    private static bool IsPlausibleCodepage(ushort cp)
        => cp is 30 or 31 or 32 or 38 or 39 or 40 or 41
                or 28 or 29 or 44
                or 25 or 26 or 27;
}

public static class ByteReaderExtensions
{
    public static uint ReadUInt32At(this ByteReader r, int offset)
    {
        if (offset < 0 || offset + 4 > r.Length) return 0;
        int saved = r.Position;
        r.Seek(offset);
        uint v = r.ReadUInt32();
        r.Seek(saved);
        return v;
    }

    public static ushort ReadUInt16At(this ByteReader r, int offset)
    {
        if (offset < 0 || offset + 2 > r.Length) return 0;
        int saved = r.Position;
        r.Seek(offset);
        ushort v = r.ReadUInt16();
        r.Seek(saved);
        return v;
    }
}
