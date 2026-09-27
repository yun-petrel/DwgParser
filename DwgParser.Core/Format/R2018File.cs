namespace DwgParser.Core.Format;

public class R2018File : R2013File
{
    internal R2018File(string? path, byte[] raw) : base(path, raw) { }

    public R2018File(IDwgFile source) : base(source, DwgVersion.AC1032) { }
}
