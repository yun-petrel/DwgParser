namespace DwgParser.Core.Format;

public class R2010File : R2004File
{
    internal R2010File(string? path, byte[] raw) : base(path, raw) { }

    public R2010File(IDwgFile source) : base(source, DwgVersion.AC1024) { }

    protected R2010File(IDwgFile source, DwgVersion targetVersion) : base(source, targetVersion) { }
}
