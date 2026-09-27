namespace DwgParser.Core.Format;

public class R2007File : R2004File
{
    private R2007Container.Model? _r2007;

    internal R2007File(string? path, byte[] raw) : base(path, raw) { }

    public R2007File(IDwgFile source) : base(source, DwgVersion.AC1021) { }

    protected R2007File(IDwgFile source, DwgVersion targetVersion) : base(source, targetVersion) { }

    internal override bool LoadContainer()
    {
        if (R2007Container.TryLoad(RawFile, out var model) && model != null)
        {
            _r2007 = model;
            foreach (var d in model.Diagnostics)
                _diagnostics.Add(d);
            _descriptors.Clear();
            foreach (var s in model.Sections)
            {
                _descriptors.Add(new SectionDescriptor
                {
                    Name = s.Name,
                    Size = s.DataSize,
                    NumPages = s.Pages.Count,
                    MaxDecompSize = 0x7400,
                    CompressedType = 2,
                    Type = 0,
                });
            }
            return true;
        }

        if (base.LoadContainer()) return true;
        _diagnostics.Add(
            "AC1021 (R2007) 容器（RS 交织头 + LZ77-AC21）解析失败，" +
            "R2004 风格容器也不匹配。");
        return false;
    }

    public override byte[]? DecompressSection(string name)
    {
        if (_r2007 != null)
        {
            var data = R2007Container.ReadSection(RawFile, _r2007, name);
            if (data != null) return data;
        }
        return base.DecompressSection(name);
    }

    public override void Save(string path)
    {
        if (Version == DwgVersion.AC1021)
        {
            bool sameVersion = R2007Container.TryLoad(RawFile, out var src7) && src7 is { Sections.Count: > 0 };
            if (sameVersion)
            {
                if (R2007Writer.TryBuildFromSource(RawFile, out var rebuilt, out var err))
                {
                    Emit(path, rebuilt, "经 R2007 真写出器重建");
                    return;
                }
                AddDiagnostic($"AC1021 Save：R2007 真写出失败（{err}），回退索引搬运。");
                if (R2007Writer.TryRebuildFromIndex(RawFile, out var carried, out var err2))
                {
                    Emit(path, carried, "经 R2007 容器索引重建写出");
                    return;
                }
                AddDiagnostic($"AC1021 Save：R2007 容器索引重建失败（{err2}），按原始字节写回。");
            }
            else if (R2007Writer.TryBuildCrossVersion(IntermediateSections(), out var xv, out var err3))
            {
                Emit(path, xv, "跨版本节内容已装入真 R2007 容器");
                return;
            }
            else
                AddDiagnostic($"AC1021 Save：跨版本容器装配失败（{err3}），按 R2004 家族字节写回。");
        }
        base.Save(path);
    }

    void Emit(string path, byte[] bytes, string what)
    {
        System.IO.File.WriteAllBytes(path, bytes);
        _objects = null;
        FilePath = path;
        AddDiagnostic($"AC1021 Save：{what}（{bytes.Length} 字节）。");
    }

    List<(string Name, byte[] Data)> IntermediateSections()
    {
        try
        {
            return ((R2004File)DwgFile.Load(FilePath ?? "in-memory.dwg", RawFile)).GetAllSectionData();
        }
        catch
        {
            return new List<(string, byte[])>();
        }
    }
}
