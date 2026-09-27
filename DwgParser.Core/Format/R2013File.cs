namespace DwgParser.Core.Format;

public class R2013File : R2010File
{
    internal R2013File(string? path, byte[] raw) : base(path, raw) { }

    public R2013File(IDwgFile source) : base(source, DwgVersion.AC1027) { }

    protected R2013File(IDwgFile source, DwgVersion targetVersion) : base(source, targetVersion) { }
}
