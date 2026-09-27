using DwgParser.Core.Objects;

namespace DwgParser.Core.Format;

public static class DwgConverter
{
    public static IDwgFile Load(string path)
        => DwgFile.Load(path);

    public static IDwgFile Load(string path, byte[] bytes)
        => DwgFile.Load(path, bytes);

    public static void Save(IDwgFile file, DwgVersion targetVersion, string outputPath)
    {
        ArgumentNullException.ThrowIfNull(file);
        if (targetVersion == DwgVersion.Unknown)
            throw new ArgumentException("目标版本无效。", nameof(targetVersion));
        if (!targetVersion.IsSupportedVersion())
            throw new ArgumentException(
                $"目标版本 {targetVersion} 低于最低支持版本 "
                + $"{DwgVersionExtensions.MinimumSupportedVersion}。", nameof(targetVersion));
        if (!file.Header.Version.IsSupportedVersion())
            throw new ArgumentException(
                $"源文件版本 {file.Header.Version} 低于最低支持版本 "
                + $"{DwgVersionExtensions.MinimumSupportedVersion}，对象流不解析、无法转换。", nameof(file));
        if (string.IsNullOrWhiteSpace(outputPath))
            throw new ArgumentException("输出路径不能为空。", nameof(outputPath));

        var dir = Path.GetDirectoryName(Path.GetFullPath(outputPath));
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        if (file.Header.Version == targetVersion && !HasPendingWriteSideChanges(file))
        {
            file.Save(outputPath);
            return;
        }

        BaseDwgFile target = targetVersion switch
        {
            DwgVersion.AC1015 => new R2000File(file, targetVersion),
            DwgVersion.AC1018 => new R2004File(file),
            DwgVersion.AC1021 => new R2007File(file),
            DwgVersion.AC1024 => new R2010File(file),
            DwgVersion.AC1027 => new R2013File(file),
            DwgVersion.AC1032 => new R2018File(file),
            _ => throw new NotSupportedException($"不支持的目标版本 {targetVersion.GetVersionName()}"),
        };
        target.Save(outputPath);
    }

    private static bool HasPendingWriteSideChanges(IDwgFile file)
    {
        if (WriteSideEdits.HasPendingChanges) return true;
        DwgObjectReader.WalkResult walk;
        try { walk = file.GetObjects(); }
        catch (Exception ex) when (ex is IOException or InvalidDataException or NotSupportedException)
        {
            return true;
        }
        if (walk is null || walk.RecordsBaselineFingerprint == 0) return false;
        return walk.RecordsFingerprint() != walk.RecordsBaselineFingerprint;
    }
}
