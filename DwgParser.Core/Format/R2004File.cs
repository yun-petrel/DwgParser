using DwgParser.Core.Compression;
using DwgParser.Core.IO;
using DwgParser.Core.Objects;

namespace DwgParser.Core.Format;

public partial class R2004File : BaseDwgFile
{
    private readonly byte[] _raw;
    private readonly List<SectionRecord> _sectionRecords = new();
    protected readonly List<SectionDescriptor> _descriptors = new();

    public ulong SectionMapAddress { get; private set; }
    public uint SectionMapId { get; private set; }
    public uint SectionInfoId { get; private set; }
    public uint SectionArraySize { get; private set; }
    public uint NumSections { get; private set; }
    public uint NumGaps { get; private set; }
    public ulong LastSectionAddress { get; private set; }

    internal R2004File(string? path, byte[] raw) : base(path, raw)
    {
        _raw = raw;
    }

    public R2004File(IDwgFile source) : this(source, DwgVersion.AC1018) { }

    protected R2004File(IDwgFile source, DwgVersion targetVersion)
        : base(null, BuildFromSource(source, targetVersion))
    {
        _raw = RawFile;
    }


    private List<R2000Class>? _classes;

    public IReadOnlyList<R2000Class> Classes => _classes ??= ParseClasses();

    private List<R2000Class> ParseClasses()
    {
        var data = _sections.FirstOrDefault(s => s.Name == "Classes")?.Data;
        if (data is null) return [];
        var parsed = R2000ClassTable.TryParseR2004Classes(data, Header.Version);
        if (parsed is null)
        {
            _diagnostics.Add("CLASSES: R2004+ 类节解析失败，变体类将显示为 Unknown。");
            return [];
        }
        return parsed;
    }

    public string? ResolveTypeName(int type)
    {
        int idx = type - 500;
        var cs = Classes;
        return idx >= 0 && idx < cs.Count ? cs[idx].DxfName : null;
    }


    private static byte[] BuildFromSource(IDwgFile source, DwgVersion targetVersion)
    {
        if (source is R2000File r2000) return BuildFromR2000(r2000, targetVersion);
        if (source is R2004File r2004) return BuildFromR2004(r2004, targetVersion);
        throw new NotSupportedException(
            $"不支持从 {source.Header.Version.GetVersionName()} 转换：需要 R13–R2000 或 R2004+ 源文件。");
    }

    private static byte[] BuildFromR2000(R2000File source, DwgVersion targetVersion)
    {
        var sections = new List<R2004Writer.SectionEntry>();
        foreach (var rec in source.Records)
        {
            var data = source.RawFile.AsSpan((int)rec.Address, (int)rec.Size).ToArray();
            if (rec.Name == "Handles")
                continue;
            sections.Add(new R2004Writer.SectionEntry("AcDb:" + rec.Name, data));
        }

        R2004SectionReencoder.RebuildObjectStream(source, targetVersion, sections);

        R2004SectionReencoder.ReencodeHeaderSection(sections, source.Header.Version, targetVersion, s => source.AddDiagnostic(s), source);
        R2004SectionReencoder.ReencodeAuxHeader(sections, source.Header.Version, targetVersion, s => source.AddDiagnostic(s));
        R2004SectionReencoder.ReencodeObjFreeSpace(sections, source.Header.Version, targetVersion, s => source.AddDiagnostic(s));
        R2004SectionReencoder.ReencodeClasses(sections, source.Header.Version, targetVersion, s => source.AddDiagnostic(s));
        R2004SectionReencoder.ReencodeSummaryInfo(sections, source.Header.Version, targetVersion, s => source.AddDiagnostic(s));
        R2004SectionReencoder.ReencodeAppInfo(sections, source.Header.Version, targetVersion, s => source.AddDiagnostic(s));
        R2004SectionReencoder.ApplyOracleSectionsBypass(sections, source, targetVersion, s => source.AddDiagnostic(s));

        byte[] built0 = R2004Writer.Build(targetVersion, R2004SectionReencoder.CreateMinimalR2004Template(targetVersion), sections);
        R2004SectionReencoder.ApplyOracleHeaderPrefixBypass(built0, targetVersion, s => source.AddDiagnostic(s));
        return built0;
    }


    internal byte[]? TryGetRawSectionData(string name)
    {
        foreach (var desc in _descriptors)
        {
            if (!string.Equals(desc.Name, name, StringComparison.OrdinalIgnoreCase)) continue;
            var data = DecompressSection(name);
            if (data == null || data.Length == 0) return null;
            return data;
        }
        return null;
    }

    private static byte[] BuildFromR2004(R2004File source, DwgVersion targetVersion)
    {
        var sections = new List<R2004Writer.SectionEntry>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        int failed = 0;
        foreach (var desc in source._descriptors)
        {
            if (!seen.Add(desc.Name))
                continue;
            var data = source.DecompressSection(desc.Name);
            if (data != null && data.Length > 0)
                sections.Add(new R2004Writer.SectionEntry(desc.Name, data, (int)desc.Type));
            else
            {
                var raw = source.TryGetRawSectionData(desc.Name);
                if (raw != null && raw.Length > 0)
                    sections.Add(new R2004Writer.SectionEntry(desc.Name, raw, (int)desc.Type));
                else
                    failed++;
            }
        }
        if (failed > 0)
            source.AddDiagnostic($"版本转换：{failed} 个节解压失败，未包含在输出中。");

        bool sameFamily = source.Header.Version != targetVersion
                           && DwgObjectStreamWriter.SameObjectFrameFormat(source.Header.Version, targetVersion);

        source.AddDiagnostic($"BuildFromR2004: collected {sections.Count} sections before rebuild");
        if (sameFamily)
        {
            source.AddDiagnostic(
                $"BuildFromR2004: 源/目标帧格式等价（{source.Header.Version.GetVersionName()}↔{targetVersion.GetVersionName()}），"
                + "对象流/句柄映射/Header 均按原样搬运，仅重建容器。");
        }
        else if (source.Header.Version != targetVersion || WriteSideEdits.HasPendingChanges)
        {
            R2004SectionReencoder.RebuildObjectStream(source, targetVersion, sections);
        }
        source.AddDiagnostic($"BuildFromR2004: {sections.Count} sections after rebuild");

        R2004SectionReencoder.ReencodeHeaderSection(sections, source.Header.Version, targetVersion, s => source.AddDiagnostic(s));
        R2004SectionReencoder.ReencodeAuxHeader(sections, source.Header.Version, targetVersion, s => source.AddDiagnostic(s));
        R2004SectionReencoder.ReencodeObjFreeSpace(sections, source.Header.Version, targetVersion, s => source.AddDiagnostic(s));
        R2004SectionReencoder.ReencodeClasses(sections, source.Header.Version, targetVersion, s => source.AddDiagnostic(s));
        R2004SectionReencoder.ReencodeSummaryInfo(sections, source.Header.Version, targetVersion, s => source.AddDiagnostic(s));
        R2004SectionReencoder.ReencodeAppInfo(sections, source.Header.Version, targetVersion, s => source.AddDiagnostic(s));
        R2004SectionReencoder.ApplyOracleSectionsBypass(sections, source, targetVersion, s => source.AddDiagnostic(s));

        byte[] template = (source is R2007File || targetVersion >= DwgVersion.AC1024)
            ? R2004SectionReencoder.CreateMinimalR2004Template(targetVersion)
            : source.RawFile;
        byte[] built = R2004Writer.Build(targetVersion, template, sections);
        R2004SectionReencoder.ApplyOracleHeaderPrefixBypass(built, targetVersion, s => source.AddDiagnostic(s));
        return built;
    }



    internal virtual bool LoadContainer()
    {
        if (!DecryptHeader()) return false;
        if (!ReadSectionMap()) return false;
        if (!ReadSectionInfo()) return false;
        return true;
    }

    public static bool TryLoad(byte[] raw, out R2004File? result)
    {
        result = null;
        var f = new R2004File(null, raw);
        if (!f.LoadContainer()) return false;
        f.DiscoverSections();
        result = f;
        return true;
    }

    internal override void DiscoverSections()
    {
        if (_descriptors.Count == 0)
        {
            _diagnostics.Add("R2004+ 容器未加载；尝试系统区页表/压缩块探测。");
            TryParseSectionMap();
            if (_sections.Count == 0) ProbeSystemChunks();
            return;
        }
        _diagnostics.Add($"R2004+ container loaded: {SectionArraySize} sections");

        var headerData = DecompressSection("AcDb:Header");
        if (headerData != null)
        {
            _sections.Add(new DwgSection
            {
                Name = "Header",
                Data = headerData,
                DataOffset = 0,
                CompressedSize = 0,
                DecompressedSize = headerData.Length,
                IsCompressed = false,
            });
            _diagnostics.Add($"Header section: {headerData.Length} bytes");
        }
        var classesData = DecompressSection("AcDb:Classes");
        if (classesData != null)
        {
            _sections.Add(new DwgSection
            {
                Name = "Classes",
                Data = classesData,
                DataOffset = 0,
                CompressedSize = 0,
                DecompressedSize = classesData.Length,
                IsCompressed = false,
            });
        }
        var handlesData = DecompressSection("AcDb:Handles");
        if (handlesData != null)
        {
            _sections.Add(new DwgSection
            {
                Name = "Handles",
                Data = handlesData,
                DataOffset = 0,
                CompressedSize = 0,
                DecompressedSize = handlesData.Length,
                IsCompressed = false,
            });
        }
        var objectsData = DecompressSection("AcDb:AcDbObjects");
        if (objectsData != null)
        {
            _sections.Add(new DwgSection
            {
                Name = "Objects",
                Data = objectsData,
                DataOffset = 0,
                CompressedSize = 0,
                DecompressedSize = objectsData.Length,
                IsCompressed = false,
            });
        }

        foreach (var desc in _descriptors)
        {
            if (string.IsNullOrEmpty(desc.Name)) continue;
            string shortName = desc.Name.StartsWith("AcDb:") ? desc.Name[5..] : desc.Name;
            if (shortName is "Header" or "Classes" or "Handles" or "Objects" or "AcDbObjects") continue;
            if (_sections.Any(s => s.Name == shortName)) continue;
            _sections.Add(new DwgSection
            {
                Name = shortName,
                Data = null,
                DataOffset = 0,
                CompressedSize = 0,
                DecompressedSize = (int)Math.Min(desc.Size > 0 ? desc.Size : 0L, int.MaxValue),
                IsCompressed = desc.CompressedType == 2 || desc.CompressedType == 4,
            });
        }
    }

    public override DwgObjectReader.WalkResult GetObjects()
    {
        if (_objects != null) return _objects;

        var handles = DecompressSection("AcDb:Handles");
        var objects = DecompressSection("AcDb:AcDbObjects");
        DwgObjectReader.WalkResult walk;
        if (handles == null || objects == null)
        {
            var handlesSec = _sections.FirstOrDefault(s =>
                s.Name.EndsWith("Handles", StringComparison.OrdinalIgnoreCase));
            var objectsSec = _sections.FirstOrDefault(s =>
                s.Name.EndsWith("Objects", StringComparison.OrdinalIgnoreCase) ||
                s.Name.EndsWith("AcDbObjects", StringComparison.OrdinalIgnoreCase));
            walk = DwgObjectReader.ReadObjects(objectsSec?.Data ?? RawFile, handlesSec?.Data, Version,
                ResolveTypeName, classes: Classes);
        }
        else
        {
            walk = DwgObjectReader.ReadObjects(objects, handles, Version, ResolveTypeName,
                FileHeader.LegacyEncodingOf(RawFile), Classes);
        }
        walk.Classes ??= Classes;
        return CacheObjects(walk);
    }

}

