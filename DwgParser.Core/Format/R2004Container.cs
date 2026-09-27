using System.Text;
using DwgParser.Core.Compression;
using DwgParser.Core.IO;

namespace DwgParser.Core.Format;

public sealed class R2004Page
{
    public int Number { get; init; }
    public uint CompressedSize { get; set; }
    public uint StartOffset { get; init; }
}

public sealed class R2004Section
{
    public required string Name { get; init; }
    public int TypeId { get; init; }
    public int CompressionFlag { get; init; }
    public long TotalDecompressedSize { get; init; }
    public int MaxDecompressedPageSize { get; init; }
    public List<R2004Page> Pages { get; } = new();
    public byte[]? Data { get; set; }

    public override string ToString() => Name + " (" + Pages.Count + " pages)";
}

public static class R2004Container
{
    private const uint SystemSectionType = 0x41630E3B;
    private const uint InfoSectionType = 0x4163003B;
    private const uint PageType = 0x4163043B;
    private const uint PageMaskSeed = 0x4164536B;

    public sealed class SecondHeader
    {
        public string IdString = "";
        public uint LastSectionId { get; init; }
        public ulong LastSectionAddress { get; init; }
        public ulong SecondHeaderAddress { get; init; }
        public uint NumSections { get; init; }
        public uint SectionMapId { get; init; }
        public ulong SectionMapAddress { get; init; }
        public uint SectionInfoId { get; init; }
        public uint SectionArraySize { get; init; }
        public bool Valid { get; init; }
    }

    public sealed record MapSection(int Number, uint Size, ulong Address);

    public static byte[] DecryptHeaderBlock(byte[] file)
    {
        const int blockSize = 0x6C;
        var dest = new byte[blockSize];
        uint seed = 1;
        for (int i = 0; i < blockSize; i++)
        {
            seed = seed * 0x343FDu + 0x269EC3u;
            dest[i] = (byte)(file[0x80 + i] ^ (seed >> 16));
        }
        return dest;
    }

    public static SecondHeader ParseSecondHeader(byte[] file)
    {
        var block = DecryptHeaderBlock(file);
        string id = Encoding.ASCII.GetString(block, 0, 11).TrimEnd('\0');
        return new SecondHeader
        {
            IdString = id,
            LastSectionId = BitConverter.ToUInt32(block, 0x28),
            LastSectionAddress = BitConverter.ToUInt64(block, 0x2C),
            SecondHeaderAddress = BitConverter.ToUInt64(block, 0x34),
            NumSections = BitConverter.ToUInt32(block, 0x40),
            SectionMapId = BitConverter.ToUInt32(block, 0x50),
            SectionMapAddress = BitConverter.ToUInt64(block, 0x54),
            SectionInfoId = BitConverter.ToUInt32(block, 0x5C),
            SectionArraySize = BitConverter.ToUInt32(block, 0x60),
            Valid = id.StartsWith("AcFssFcAJMB", StringComparison.Ordinal),
        };
    }

    public static bool TryLoad(byte[] file, out List<R2004Section> sections, out SecondHeader header, out List<MapSection> mapEntries)
    {
        sections = new List<R2004Section>();
        mapEntries = new List<MapSection>();
        header = ParseSecondHeader(file);
        if (!header.Valid || (long)header.SectionMapAddress + 0x120 >= (long)file.Length)
            return false;

        long pageMapPos = (long)header.SectionMapAddress + 0x100;
        uint pmType = BitConverter.ToUInt32(file, (int)pageMapPos);
        if (pmType != SystemSectionType)
            return false;
        uint pmDecompSize = BitConverter.ToUInt32(file, (int)pageMapPos + 4);
        uint pmCompSize = BitConverter.ToUInt32(file, (int)pageMapPos + 8);
        if (pmCompSize == 0 || pageMapPos + 20 + (long)pmCompSize > (long)file.Length)
            return false;
        if (pmDecompSize == 0 || pmDecompSize > RsDecoder.MaxReasonableSize)
            return false;
        if (!RsDecoder.TryDecompress(file, (int)pageMapPos + 20, (int)pmCompSize,
                (int)pmDecompSize, out var mapData))
            return false;

        var entries = new List<MapSection>();
        ulong accumulated = 0x100;
        int pos = 0;
        while (pos + 8 <= mapData.Length)
        {
            int number = BitConverter.ToInt32(mapData, pos);
            uint size = BitConverter.ToUInt32(mapData, pos + 4);
            pos += 8;
            bool counts = number <= (int)header.SectionArraySize;
            if (number < 0)
            {
                if (counts)
                    accumulated += size;
                pos += 16;
                continue;
            }
            if (!counts)
                continue;
            entries.Add(new MapSection(number, size, accumulated));
            accumulated += size;
        }

        MapSection? infoEntry = null;
        foreach (var e in entries)
        {
            if (e.Number == (int)header.SectionInfoId)
            {
                infoEntry = e;
                break;
            }
        }
        if (infoEntry is null || (long)infoEntry.Address + 24 >= (long)file.Length)
            return false;

        long infoPos = (long)infoEntry.Address;
        if (BitConverter.ToUInt32(file, (int)infoPos) != InfoSectionType)
            return false;
        uint infoDecompSize = BitConverter.ToUInt32(file, (int)infoPos + 4);
        uint infoCompSize = BitConverter.ToUInt32(file, (int)infoPos + 8);
        if (infoCompSize == 0 || infoPos + 20 + (long)infoCompSize > (long)file.Length)
            return false;
        if (infoDecompSize == 0 || infoDecompSize > RsDecoder.MaxReasonableSize)
            return false;
        if (!RsDecoder.TryDecompress(file, (int)infoPos + 20, (int)infoCompSize,
                (int)infoDecompSize, out var infoData))
            return false;

        if (infoData.Length < 20)
            return false;
        uint numDescriptions = BitConverter.ToUInt32(infoData, 0);
        if (numDescriptions == 0 || numDescriptions > 4096)
            return false;

        int recPos = 20;
        for (uint i = 0; i < numDescriptions && recPos + 96 <= infoData.Length; i++)
        {
            long totalSize = BitConverter.ToInt64(infoData, recPos);
            uint numPages = BitConverter.ToUInt32(infoData, recPos + 8);
            uint maxDecomp = BitConverter.ToUInt32(infoData, recPos + 12);
            uint compression = BitConverter.ToUInt32(infoData, recPos + 20);
            int typeId = BitConverter.ToInt32(infoData, recPos + 24);
            string name = Encoding.ASCII.GetString(infoData, recPos + 32, 64).TrimEnd('\0');
            recPos += 96;

            if (numPages > 1_000_000)
                continue;

            var section = new R2004Section
            {
                Name = name,
                TypeId = typeId,
                CompressionFlag = (int)compression,
                TotalDecompressedSize = totalSize,
                MaxDecompressedPageSize = (int)maxDecomp,
            };

            for (uint p = 0; p < numPages && recPos + 16 <= infoData.Length; p++)
            {
                int pageNumber = BitConverter.ToInt32(infoData, recPos);
                uint pageSize = BitConverter.ToUInt32(infoData, recPos + 4);
                uint startOffset = BitConverter.ToUInt32(infoData, recPos + 8);
                recPos += 16;
                if (pageNumber < 0)
                    continue;
                section.Pages.Add(new R2004Page
                {
                    Number = pageNumber,
                    CompressedSize = pageSize,
                    StartOffset = startOffset,
                });
            }

            sections.Add(section);
        }

        mapEntries = entries;
        return true;
    }

    public static bool TryGetPageAddress(List<MapSection> mapEntries, int pageNumber, out long address)
    {
        foreach (var e in mapEntries)
        {
            if (e.Number == pageNumber)
            {
                address = (long)e.Address;
                return true;
            }
        }
        address = 0;
        return false;
    }

    public static bool TryReadSection(byte[] file, R2004Section section, List<MapSection>? mapEntries = null)
    {
        if (section.Pages.Count == 0 || section.MaxDecompressedPageSize <= 0)
            return false;
        long capacity = Math.Max(section.TotalDecompressedSize,
            (long)section.Pages.Count * section.MaxDecompressedPageSize);
        if (capacity <= 0 || capacity > RsDecoder.MaxReasonableSize)
            return false;

        var buffer = new byte[capacity];
        foreach (var page in section.Pages)
        {
            long addr;
            if (mapEntries != null)
            {
                if (!TryGetPageAddress(mapEntries, page.Number, out addr))
                    continue;
            }
            else
            {
                addr = 0;
            }
            if (addr + 32 > (long)file.Length)
                continue;

            uint mask = PageMaskSeed ^ (uint)addr;
            uint pageType = BitConverter.ToUInt32(file, (int)addr) ^ mask;
            uint dataSize = BitConverter.ToUInt32(file, (int)addr + 8) ^ mask;
            uint pageSize = BitConverter.ToUInt32(file, (int)addr + 12) ^ mask;
            uint startOffset = BitConverter.ToUInt32(file, (int)addr + 16) ^ mask;

            if (startOffset > capacity || startOffset + pageSize > capacity)
                continue;

            long payload = addr + 32;
            if (section.CompressionFlag == 2)
            {
                int count = (int)Math.Min((long)dataSize, file.Length - payload);
                if (count <= 0)
                    continue;
                var decoded = RsDecoder.DecodeLenient(file, (int)payload, count);
                int take = (int)Math.Min((long)decoded.Length, pageSize);
                if (take > 0)
                    Buffer.BlockCopy(decoded, 0, buffer, (int)startOffset, take);
            }
            else
            {
                int count = (int)Math.Min(Math.Min((long)dataSize, pageSize), file.Length - payload);
                if (count > 0)
                    Buffer.BlockCopy(file, (int)payload, buffer, (int)startOffset, count);
            }
        }

        long finalLength = section.TotalDecompressedSize > 0 &&
                           section.TotalDecompressedSize <= capacity
            ? section.TotalDecompressedSize
            : capacity;
        section.Data = finalLength == capacity
            ? buffer
            : buffer.AsSpan(0, (int)finalLength).ToArray();
        return true;
    }
}