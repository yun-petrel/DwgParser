using DwgParser.Core.Objects;

namespace DwgParser.Core.Format;

public static class DwgFile
{
    public static BaseDwgFile Load(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException("DWG file not found.", path);
        return Load(path, File.ReadAllBytes(path));
    }

    public static BaseDwgFile Load(string path, byte[] bytes)
    {
        var file = CreateForVersion(path, bytes);
        file.DiscoverSections();
        return file;
    }

    private static BaseDwgFile CreateForVersion(string path, byte[] bytes)
    {
        var version = new FileHeader(bytes).Version;
        switch (version)
        {
            case DwgVersion.AC1015:
                return LoadR2000(path, bytes);

            case DwgVersion.AC1021:
                return LoadR2004(new R2007File(path, bytes));
            case DwgVersion.AC1024:
                return LoadR2004(new R2010File(path, bytes));
            case DwgVersion.AC1027:
                return LoadR2004(new R2013File(path, bytes));
            case DwgVersion.AC1032:
                return LoadR2004(new R2018File(path, bytes));

            case var v when v.IsR2004Plus():
                return LoadR2004(new R2004File(path, bytes));

            default:
                var scan = new ScannedDwgFile(path, bytes);
                scan.AddDiagnostic(version.IsSupportedVersion()
                    ? $"版本 {version} 暂不支持；仅做结构扫描。"
                    : $"版本 {version} 低于最低支持版本 "
                      + $"{DwgVersionExtensions.MinimumSupportedVersion}；仅做结构扫描。");
                return scan;
        }
    }

    private static BaseDwgFile LoadR2000(string path, byte[] bytes)
    {
        if (R2000File.TryLoad(bytes, out var r2) && r2 != null)
        {
            r2.SetPath(path);
            return r2;
        }
        var fallback = new ScannedDwgFile(path, bytes);
        fallback.AddDiagnostic("R2000 容器解析失败；仅做结构扫描。");
        return fallback;
    }

    private static BaseDwgFile LoadR2004(R2004File file)
    {
        if (!file.LoadContainer())
            file.AddDiagnostic($"{file.VersionName} 容器解析失败；回退到系统区页表/压缩块探测。");
        return file;
    }
}

internal sealed class ScannedDwgFile : BaseDwgFile
{
    internal ScannedDwgFile(string? path, byte[] raw) : base(path, raw) { }

    public override DwgObjectReader.WalkResult GetObjects()
    {
        if (_objects is not null) return _objects;
        return CacheObjects(DwgObjectReader.ReadObjects(RawFile, null, Version));
    }

    internal override void DiscoverSections() => StructuralScan();
}
