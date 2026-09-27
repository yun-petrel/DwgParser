using DwgParser.Core.Compression;
using DwgParser.Core.IO;
using DwgParser.Core.Objects;
using DwgParser.Core.Strings;

namespace DwgParser.Core.Format;

public abstract class BaseDwgFile : IDwgFile
{
    public string? FilePath { get; protected set; }
    public long FileSize { get; protected set; }
    public byte[] RawFile { get; protected set; }
    public FileHeader Header { get; protected set; }
    public DwgVersion Version => Header.Version;
    public string VersionName => Version.GetVersionName();

    public IReadOnlyList<DwgSection> Sections => _sections;
    protected readonly List<DwgSection> _sections = new();

    public IReadOnlyList<string> Diagnostics => _diagnostics;
    protected readonly List<string> _diagnostics = new();

    protected DwgObjectReader.WalkResult? _objects;

    protected DwgObjectReader.WalkResult CacheObjects(DwgObjectReader.WalkResult walk)
    {
        _objects = walk;
        DwgObjectReader.FillLinetypeScales(walk, _sections, Version);
        return walk;
    }

    internal void AddDiagnostic(string message) => _diagnostics.Add(message);

    internal void SetPath(string path) => FilePath = path;

    protected BaseDwgFile(string? path, byte[] raw)
    {
        if (raw.Length < 6)
            throw new InvalidDataException("File too small to be a DWG.");
        FilePath = path;
        RawFile = raw;
        FileSize = raw.Length;
        Header = new FileHeader(raw);
    }


    public abstract DwgObjectReader.WalkResult GetObjects();

    public virtual void Save(string path)
    {
        File.WriteAllBytes(path, RawFile);
        _objects = null;
        FilePath = path;
    }

    internal virtual void DiscoverSections()
    {
        _diagnostics.Add("Pre-R13 format: structural scan only.");
        StructuralScan();
    }


    protected static readonly string[] KnownSectionNames =
    {
        "AcDb:Header", "AcDb:Classes", "AcDb:Handles", "AcDb:ObjFreeSpace",
        "AcDb:Template", "AcDb:AuxHeader", "AcDb:AppInfo", "AcDb:Preview",
        "AcDb:SummaryInfo", "AcDb:FileDepList", "AcDb:RevHistory",
        "AcDb:AcDbObjects", "AcDb:Objects", "AcDb:Security",
    };

    protected void TryParseSectionMap()
    {
        uint addr = Header.SystemAreaAddress;
        if (addr == 0 || addr >= RawFile.Length)
        {
            _diagnostics.Add($"System-area pointer invalid (0x{addr:X}).");
            return;
        }

        try
        {
            var r = new ByteReader(RawFile);
            r.Seek((int)addr);

            uint compSize = r.ReadUInt32();
            uint decompSize = r.ReadUInt32();
            uint compMarker = r.ReadUInt32();
            r.Seek(r.Position - 4);
            bool isCompressed = compMarker == 0x41100000u;

            if (compSize == 0 || compSize > RawFile.Length - addr || decompSize == 0
                || decompSize > RsDecoder.MaxReasonableSize)
            {
                _diagnostics.Add($"System-map sizes implausible at 0x{addr:X} (comp={compSize}, decomp={decompSize}).");
                return;
            }

            int headerSkip = isCompressed ? 0x14 : 0x08;
            int payloadOffset = (int)addr + headerSkip;
            int payloadLen = Math.Min((int)compSize, (int)(RawFile.Length - payloadOffset));

            byte[] mapData;
            if (isCompressed)
            {
                if (!RsDecoder.TryDecompress(RawFile, payloadOffset, payloadLen,
                        (int)Math.Min(decompSize, int.MaxValue), out var decoded))
                {
                    _diagnostics.Add("System-map decompression failed validation; falling back to scan.");
                    return;
                }
                mapData = decoded;
            }
            else
            {
                mapData = new byte[payloadLen];
                Array.Copy(RawFile, payloadOffset, mapData, 0, payloadLen);
            }

            ParseSectionMapPayload(mapData);
        }
        catch (Exception ex)
        {
            _diagnostics.Add("Section-map parse error: " + ex.Message);
        }
    }

    private void ParseSectionMapPayload(byte[] mapData)
    {
        var r = new ByteReader(mapData);
        if (mapData.Length < 16) return;

        uint pageCount = r.ReadUInt32();
        uint maxPages = r.ReadUInt32();
        uint lastPageId = r.ReadUInt32();
        uint unknown = r.ReadUInt32();
        if (pageCount == 0 || pageCount > 100_000)
        {
            _diagnostics.Add($"Implausible page count ({pageCount}).");
            return;
        }

        var pages = new List<(uint Id, uint Size, uint Uncomp, long Offset)>();
        for (int i = 0; i < pageCount && r.Remaining >= 20; i++)
        {
            uint id = r.ReadUInt32();
            uint size = r.ReadUInt32();
            uint uncomp = r.ReadUInt32();
            ulong off64 = r.ReadUInt64();
            if (r.Remaining >= 8) r.Skip(8);
            pages.Add((id, size, uncomp, unchecked((long)off64)));
        }

        while (r.Remaining >= 24)
        {
            int recStart = r.Position;
            uint size = r.ReadUInt32();
            uint decomp = r.ReadUInt32();
            uint count1 = r.ReadUInt32();
            uint count2 = r.ReadUInt32();
            bool compressed = count1 != 2;
            if (size == 0xFFFFFFFF || decomp == 0xFFFFFFFF) break;

            string? name = ReadNameSmart(r);
            if (name == null)
            {
                r.Seek(recStart + 4);
                continue;
            }

            var known = KnownSectionNames.FirstOrDefault(k =>
                k.EndsWith(name, StringComparison.OrdinalIgnoreCase));
            var section = new DwgSection
            {
                Name = known ?? name,
                DataOffset = 0,
                CompressedSize = (int)size,
                DecompressedSize = (int)decomp,
                IsCompressed = compressed,
            };
            _sections.Add(section);
        }

        AttachPageData(pages);
    }

    private static string? ReadNameSmart(ByteReader r)
    {
        if (r.Remaining < 2) return null;
        ushort len = r.ReadUInt16();
        if (len == 0 || len > 64 || len > r.Remaining) return null;
        var bytes = r.ReadBytes(len);
        bool printable = bytes.All(b => b is >= 0x20 and <= 0x7E);
        if (!printable) return null;
        return System.Text.Encoding.ASCII.GetString(bytes);
    }

    private void AttachPageData(List<(uint Id, uint Size, uint Uncomp, long Offset)> pages)
    {
        int pageIdx = 0;
        foreach (var section in _sections)
        {
            int remainingComp = section.CompressedSize;
            long firstOff = -1;
            int gathered = 0;
            var buffer = new byte[Math.Max(section.DecompressedSize, 0)];

            while (remainingComp > 0 && pageIdx < pages.Count)
            {
                var p = pages[pageIdx++];
                if (firstOff < 0) firstOff = p.Offset;
                int take = Math.Min((int)p.Size, remainingComp);
                if (p.Offset < 0 || p.Offset + take > RawFile.Length) break;

                byte[] chunkSrc = RawFile;
                int srcOff = (int)p.Offset;
                int srcLen = take;

                if (section.IsCompressed)
                {
                    srcOff += 0x10;
                    srcLen -= 0x10;
                    if (!RsDecoder.TryDecompress(RawFile, srcOff, Math.Max(srcLen, 0),
                            (int)Math.Min(p.Uncomp, int.MaxValue), out var dec))
                    {
                        _diagnostics.Add($"Page {p.Id}: decompression rejected.");
                        break;
                    }
                    if (gathered + dec.Length <= buffer.Length)
                    {
                        Buffer.BlockCopy(dec, 0, buffer, gathered, dec.Length);
                        gathered += dec.Length;
                    }
                }
                else
                {
                    int cp = Math.Min(srcLen, buffer.Length - gathered);
                    if (cp > 0)
                    {
                        Buffer.BlockCopy(RawFile, srcOff, buffer, gathered, cp);
                        gathered += cp;
                    }
                }
                remainingComp -= take;
                if (!section.IsCompressed && gathered >= section.DecompressedSize) break;
            }

            if (firstOff >= 0 && gathered > 0)
            {
                section.DataOffset = firstOff;
                section.Data = buffer.AsSpan(0, gathered).ToArray();
            }
        }
    }

    protected void StructuralScan()
    {
        _sections.Add(new DwgSection
        {
            Name = "(scan)",
            DataOffset = 0,
            CompressedSize = (int)FileSize,
            DecompressedSize = (int)FileSize,
            IsCompressed = false,
            Data = RawFile,
        });
    }

    protected void ProbeSystemChunks()
    {
        try
        {
            var chunks = ChunkLocator.FindSystemChunks(RawFile);
            int idx = 1;
            foreach (var chunk in chunks)
            {
                string name = NameChunkFromContent(chunk.Decoded) ?? ("chunk" + idx++);
                _sections.Add(new DwgSection
                {
                    Name = name + " (探测)",
                    DataOffset = chunk.Offset,
                    CompressedSize = 0,
                    DecompressedSize = chunk.Decoded.Length,
                    IsCompressed = true,
                    Data = chunk.Decoded,
                });
                _diagnostics.Add($"探测到压缩块 @0x{chunk.Offset:X} 匹配标记数={chunk.MatchedNames} 解码={chunk.Decoded.Length}B");
            }
        }
        catch (Exception ex)
        {
            _diagnostics.Add("Chunk probe error: " + ex.Message);
        }

        if (_sections.Count == 0)
            StructuralScan();
    }

    private static string? NameChunkFromContent(byte[] data)
    {
        string text = System.Text.Encoding.ASCII.GetString(
            data.AsSpan(0, Math.Min(data.Length, 8192)));
        if (text.Contains("AcDb:Header")) return "Header";
        if (text.Contains("AcDb:Classes")) return "Classes";
        if (text.Contains("AcDb:Handles")) return "Handles";
        if (text.Contains("AcDb:Objects") || text.Contains("AcDb:AcDbObjects")) return "Objects";
        return null;
    }


    public List<ExtractedString> ExtractStrings(int maxLength = 200)
        => ExtractStrings(_sections.Count > 0
            ? _sections.Where(s => s.Data != null).Select(s => s).ToList()
            : null, maxLength);

    public List<ExtractedString> ExtractStrings(List<DwgSection>? sources, int maxLength)
    {
        var results = new List<ExtractedString>();
        bool decodedAvailable = sources != null && sources.Any(s => s.Data != null && !ReferenceEquals(s.Data, RawFile));
        results.AddRange(StringExtractor.Extract(RawFile, 0, false));
        if (decodedAvailable)
        {
            foreach (var s in sources!)
            {
                if (s.Data == null) continue;
                if (ReferenceEquals(s.Data, RawFile)) continue;
                results.AddRange(StringExtractor.Extract(s.Data, s.DataOffset, s.IsCompressed));
            }
        }

        foreach (var hit in LiteralScanner.Scan(RawFile))
            results.Add(new ExtractedString(hit.Offset, hit.Text,
                StringExtractor.ClassifyValue(hit.Text), true));

        return results
            .Where(x => x.Value.Length <= maxLength)
            .Select(x => x with { Value = x.Value.Trim().TrimEnd('\0') })
            .Where(x => x.Value.Length > 0)
            .Where(x => IsMeaningful(x.Value))
            .GroupBy(x => x.Value, StringComparer.Ordinal)
            .Select(g => g.OrderBy(x => x.Offset).First())
            .OrderBy(x => x.Offset)
            .ToList();
    }

    private static bool IsMeaningful(string v)
    {
        foreach (var ch in v)
            if ((ch >= 'a' && ch <= 'z') || (ch >= 'A' && ch <= 'Z'))
                return true;
        return v.Length >= 4 && v.All(char.IsDigit);
    }

    public List<ClassOccurrence> ScanObjectClasses()
    {
        var sources = _sections.Where(s => s.Data != null).ToList();
        var merged = new Dictionary<string, int>(StringComparer.Ordinal);
        if (sources.Count == 0)
        {
            foreach (var occ in ObjectScanner.ScanClasses(RawFile))
                merged[occ.ClassName] = merged.GetValueOrDefault(occ.ClassName) + occ.Count;
        }
        else
        {
            foreach (var s in sources)
                foreach (var occ in ObjectScanner.ScanClasses(s.Data!))
                    merged[occ.ClassName] = merged.GetValueOrDefault(occ.ClassName) + occ.Count;
        }

        foreach (var hit in LiteralScanner.Scan(RawFile))
        {
            string v = hit.Text.Trim().TrimEnd('\0');
            bool classLike = v.StartsWith("AcDb", StringComparison.Ordinal)
                          || v.StartsWith("Acad", StringComparison.Ordinal);
            if (!classLike) continue;
            int cut = 0;
            while (cut < v.Length && (char.IsLetterOrDigit(v[cut]) || v[cut] == '_' || v[cut] == ':'))
                cut++;
            if (cut < 5) continue;
            string name = v[..cut];
            merged[name] = merged.GetValueOrDefault(name) + 1;
        }

        return merged.OrderByDescending(kv => kv.Value)
            .Select(kv => new ClassOccurrence(kv.Key, kv.Value))
            .ToList();
    }

    public bool HasPreviewBitmap()
        => Header.PreviewAddress is uint a && a + 2 <= RawFile.Length &&
           RawFile[a] == (byte)'B' && RawFile[a + 1] == (byte)'M';

    public byte[]? TryExtractPreview()
    {
        if (Header.PreviewAddress is not uint addr || addr + 8 > RawFile.Length)
            return null;

        if (RawFile[addr] == (byte)'B' && RawFile[addr + 1] == (byte)'M' && addr + 6 <= RawFile.Length)
        {
            uint bmpSize = BitConverter.ToUInt32(RawFile, (int)addr + 2);
            if (bmpSize >= 14 && bmpSize <= RawFile.Length - addr)
                return RawFile.AsSpan((int)addr, (int)bmpSize).ToArray();
            return RawFile.AsSpan((int)addr, (int)Math.Min(bmpSize | (uint)RawFile.Length, (uint)(RawFile.Length - addr))).ToArray();
        }
        return null;
    }
}
