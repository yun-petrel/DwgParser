using DwgParser.Core.Objects;
using DwgParser.Core.Objects.Records;
using DwgParser.Core.Strings;

namespace DwgParser.Core.Format;

public sealed class DwgDocument
{
    private readonly BaseDwgFile _file;
    private bool _acdsAttached;
    private DwgStyleCatalog? _styles;
    private Dictionary<ulong, DwgObjectRecord>? _recordsByHandle;
    private Dictionary<ulong, IEntity>? _entitiesByHandle;

    public string FilePath => _file.FilePath ?? string.Empty;
    public long FileSize => _file.FileSize;
    public byte[] RawFile => _file.RawFile;
    public FileHeader Header => _file.Header;
    public DwgVersion Version => _file.Version;
    public string VersionName => _file.VersionName;

    public IReadOnlyList<DwgSection> Sections => _file.Sections;

    public IReadOnlyList<string> Diagnostics => _file.Diagnostics;

    public IDwgFile File => _file;

    public R2000File? R2000 => _file as R2000File;

    public R2004File? R2004 => _file as R2004File;

    public DwgObjectReader.WalkResult? Objects { get; private set; }

    public IReadOnlyList<IEntity> Entities => GetObjects().EntitiesGeneric;

    public LayerTable Layers => GetObjects().Layers;

    public IReadOnlyList<DwgObjectRecord> ObjectRecords => GetObjects().Records;

    public BlockTable Blocks => GetObjects().Blocks;

    public DwgStyleCatalog Styles => _styles ??= DwgStyleCatalog.From(GetObjects());

    public DwgObjectRecord? FindRecord(ulong handle)
    {
        _recordsByHandle ??= BuildRecordIndex(GetObjects().Records);
        return _recordsByHandle.TryGetValue(handle, out var r) ? r : null;
    }

    public IEntity? FindEntity(ulong handle)
    {
        _entitiesByHandle ??= BuildEntityIndex(GetObjects().Entities);
        return _entitiesByHandle.TryGetValue(handle, out var e) ? e : null;
    }

    public IReadOnlyList<IEntity> UnmappedEntities => _unmapped ??= ComputeUnmapped();

    private IReadOnlyList<IEntity> ComputeUnmapped()
    {
        var walk = GetObjects();
        if (walk.Records.Count == 0) return Array.Empty<IEntity>();
        var mapped = new HashSet<ulong>(walk.Records.Count);
        foreach (var r in walk.Records)
            if (r.Handle != 0) mapped.Add(r.Handle);
        var list = new List<IEntity>();
        foreach (var e in walk.Entities)
            if (!mapped.Contains(e.Handle)) list.Add(e);
        return list;
    }

    private IReadOnlyList<IEntity>? _unmapped;

    private static Dictionary<ulong, DwgObjectRecord> BuildRecordIndex(List<DwgObjectRecord> records)
    {
        var map = new Dictionary<ulong, DwgObjectRecord>(records.Count);
        foreach (var r in records)
            if (r.Handle != 0) map.TryAdd(r.Handle, r);
        return map;
    }

    private static Dictionary<ulong, IEntity> BuildEntityIndex(List<DwgEntity> entities)
    {
        var map = new Dictionary<ulong, IEntity>(entities.Count);
        foreach (var e in entities)
            if (e.Handle != 0) map.TryAdd(e.Handle, e);
        return map;
    }

    public DwgObjectReader.WalkResult GetObjects()
    {
        Objects ??= _file.GetObjects();
        AttachAcDsGeometry(Objects);
        return Objects;
    }

    private void AttachAcDsGeometry(DwgObjectReader.WalkResult walk)
    {
        if (_acdsAttached) return;
        _acdsAttached = true;
        if (Version < DwgVersion.AC1027) return;
        if (_file is not R2004File rf) return;
        AcDsDataTree.AttachGeometry(walk, rf.DecompressSection("AcDb:AcDsPrototype_1b"));
    }

    private DwgDocument(BaseDwgFile file) => _file = file;

    public static DwgDocument Load(string path)
        => new(DwgFile.Load(path));

    public static DwgDocument Load(string path, byte[] bytes)
        => new(DwgFile.Load(path, bytes));


    public List<ExtractedString> ExtractStrings(int maxLength = 200)
        => _file.ExtractStrings(maxLength);

    public List<ExtractedString> ExtractStrings(List<DwgSection>? sources, int maxLength)
        => _file.ExtractStrings(sources, maxLength);

    public List<ClassOccurrence> ScanObjectClasses()
        => _file.ScanObjectClasses();

    public bool HasPreviewBitmap()
        => _file.HasPreviewBitmap();

    public byte[]? TryExtractPreview()
        => _file.TryExtractPreview();


    public void Save(string path)
    {
        _file.Save(path);
        InvalidateViews();
    }

    public void Save(string path, DwgVersion targetVersion)
    {
        DwgConverter.Save(_file, targetVersion, path);
        InvalidateViews();
    }

    public void SaveAs(DwgVersion targetVersion, string path)
        => Save(path, targetVersion);

    private void InvalidateViews()
    {
        Objects = null;
        _styles = null;
        _recordsByHandle = null;
        _entitiesByHandle = null;
        _unmapped = null;
        _acdsAttached = false;
    }
}
