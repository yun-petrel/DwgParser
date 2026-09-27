namespace DwgParser.Core.Format;

internal static class R2007Template
{
    public static readonly byte[] PlainHeader =
    {
        0x41, 0x43, 0x31, 0x30, 0x32, 0x31, 0x00, 0x00, 0x00, 0x00, 0x00, 0x19, 0x03, 0xE0, 0x0C, 0x00,
        0x00, 0x21, 0x37, 0x27, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x80, 0x0C, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x80, 0x00, 0x00, 0x00, 0x80, 0x45, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
    };

    readonly record struct Attrs(long MaxSize, long Encrypted, long Hashcode, long Unknown, long Encoded);

    static readonly (string Name, Attrs A)[] Order =
    {
        ("AcDb:FileDepList",  new Attrs(256,   2, 0x6C4205CA, 0, 1)),
        ("AcDb:AppInfo",      new Attrs(768,   0, 0x3FA0043E, 0, 1)),
        ("AcDb:Preview",      new Attrs(0,     0, 0x40AA0473, 0, 1)),
        ("AcDb:SummaryInfo",  new Attrs(128,   0, 0x717A060F, 0, 1)),
        ("AcDb:RevHistory",   new Attrs(4096,  0, 0x60A205B3, 0, 4)),
        ("AcDb:AcDbObjects",  new Attrs(63488, 0, 0x674C05A9, 0, 4)),
        ("AcDb:ObjFreeSpace", new Attrs(63488, 0, 0x77E2061F, 0, 4)),
        ("AcDb:Template",     new Attrs(1024,  0, 0x4A1404CE, 0, 4)),
        ("AcDb:Handles",      new Attrs(63488, 0, 0x3F6E0450, 0, 4)),
        ("AcDb:Classes",      new Attrs(63488, 0, 0x3F54045F, 0, 4)),
        ("AcDb:AuxHeader",    new Attrs(2048,  0, 0x54F0050A, 0, 4)),
        ("AcDb:Header",       new Attrs(2048,  0, 0x32B803D9, 0, 4)),
    };

    public static List<R2007Writer.SectionInput> BuildInputs(
        IList<(string Name, byte[] Data)> sections)
    {
        var byName = new Dictionary<string, (string Name, byte[] Data)>(StringComparer.OrdinalIgnoreCase);
        foreach (var sd in sections)
            if (!string.IsNullOrEmpty(sd.Name) && sd.Data.Length > 0) byName[sd.Name] = sd;

        var list = new List<R2007Writer.SectionInput>();
        foreach (var (name, a) in Order)
        {
            if (!byName.TryGetValue(name, out var sd)) continue;
            byName.Remove(name);
            list.Add(new R2007Writer.SectionInput
            {
                Name = sd.Name,
                Content = sd.Data,
                MaxSize = a.MaxSize > 0 ? a.MaxSize : Align64(sd.Data.Length),
                Encrypted = a.Encrypted,
                Hashcode = a.Hashcode,
                Unknown = a.Unknown,
                Encoded = a.Encoded,
            });
        }
        foreach (var sd in byName.Values)
            list.Add(new R2007Writer.SectionInput
            {
                Name = sd.Name,
                Content = sd.Data,
                MaxSize = Align64(sd.Data.Length),
                Encoded = 4,
            });
        return list;
    }

    static long Align64(long v) => Math.Max(128, (v + 63) & ~63L);
}
