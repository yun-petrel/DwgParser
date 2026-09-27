namespace DwgParser.Core.Format;

public sealed class DwgSection
{
    public string Name { get; init; } = "";
    public long DataOffset { get; set; }
    public int CompressedSize { get; init; }
    public int DecompressedSize { get; init; }
    public bool IsCompressed { get; init; }
    public byte[]? Data { get; set; }

    public override string ToString()
        => $"{Name}: off=0x{DataOffset:X} comp={CompressedSize} decomp={DecompressedSize}{(IsCompressed ? " [lz]" : "")}";
}
