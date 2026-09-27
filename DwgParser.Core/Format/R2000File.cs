using System.Text;
using DwgParser.Core.IO;
using DwgParser.Core.Objects;

namespace DwgParser.Core.Format;

public sealed record R2000SectionRecord(int Number, uint Address, uint Size, string Name)
{
    public override string ToString() => $"#{Number} {Name}: addr=0x{Address:X} size={Size}";
}

public sealed record R2000Class(
    int Number,
    int ProxyFlag,
    string AppName,
    string CppName,
    string DxfName,
    bool IsZombie,
    int ItemClassId,
    int NumInstances = 0,
    int DwgVer = 0,
    int MaintVer = 0,
    int Unknown1 = 0,
    int Unknown2 = 0)
{
    public override string ToString() => $"{Number} {DxfName} ({CppName})";
}

public class R2000File : BaseDwgFile
{
    public byte[] Raw => RawFile;

    public IReadOnlyList<R2000SectionRecord> Records => _records;
    private readonly List<R2000SectionRecord> _records = new();

    public IReadOnlyList<R2000Class> Classes => _classes;
    private readonly List<R2000Class> _classes = new();

    public IReadOnlyList<ulong> Handles => _handles;
    public IReadOnlyList<int> ObjectOffsets => _offsets;
    private readonly List<ulong> _handles = new();
    private readonly List<int> _offsets = new();

    internal R2000File(string? path, byte[] raw) : base(path, raw) { }

    public R2000File(IDwgFile source)
        : this(source, source.Header.Version.IsR2000() ? source.Header.Version : DwgVersion.AC1015) { }

    public R2000File(IDwgFile source, DwgVersion targetVersion)
        : base(null, R2000DowngradeBuilder.BuildFromSource(source, targetVersion)) { }

    public static bool TryLoad(byte[] raw, out R2000File? file)
    {
        file = null;
        if (raw.Length < 0x60)
            return false;
        var version = ParseVersion(Encoding.ASCII.GetString(raw, 0, 6));
        if (!version.IsR2000())
            return false;

        var f = new R2000File(null, raw);
        if (!f.TryParseLocatorRecords())
            return false;
        f.TryReadClasses();
        f.TryReadObjectMap();
        file = f;
        return true;
    }

    internal override void DiscoverSections()
    {
        foreach (var rec in _records)
        {
            _sections.Add(new DwgSection
            {
                Name = rec.Name,
                DataOffset = (int)rec.Address,
                CompressedSize = (int)rec.Size,
                DecompressedSize = (int)rec.Size,
                IsCompressed = false,
                Data = RawFile.AsSpan((int)rec.Address,
                    (int)Math.Min(rec.Size, (uint)(RawFile.Length - (int)rec.Address))).ToArray(),
            });
        }
    }

    public override DwgObjectReader.WalkResult GetObjects()
    {
        if (_objects is not null) return _objects;
        var walk = DwgObjectReader.ReadObjectsR2000(RawFile, Handles, ObjectOffsets,
            Version, ResolveTypeName, FileHeader.LegacyEncodingOf(RawFile), Classes);
        walk.Classes ??= Classes;
        return CacheObjects(walk);
    }

    private static DwgVersion ParseVersion(string s) => s switch
    {
        "AC1015" => DwgVersion.AC1015,
        _ => DwgVersion.Unknown,
    };


    private static readonly string[] SectionNames =
    {
        "Header", "Classes", "Handles", "ObjFreeSpace", "Template", "AuxHeader",
        "Objects", "AuxData",
    };

    private bool TryParseLocatorRecords()
    {
        var r = new ByteReader(Raw);
        ushort count = r.ReadUInt16At(0x15);
        if (count is < 3 or > 8)
        {
            _diagnostics.Add($"Implausible section count {count} at 0x15.");
            return false;
        }

        for (int i = 0; i < count; i++)
        {
            int pos = 0x19 + i * 9;
            if (pos + 9 > Raw.Length)
                break;
            int number = Raw[pos];
            uint address = BitConverter.ToUInt32(Raw, pos + 1);
            uint size = BitConverter.ToUInt32(Raw, pos + 5);
            string name = (uint)number < SectionNames.Length ? SectionNames[number] : "?";
            if ((long)address + size > Raw.Length)
            {
                _diagnostics.Add($"Section #{number} ({name}) out of bounds: 0x{address:X}+{size}.");
                continue;
            }
            _records.Add(new R2000SectionRecord(number, address, size, name));
        }

        return _records.Count >= 3;
    }

    public R2000SectionRecord? FindSection(string name)
    {
        foreach (var rec in _records)
            if (string.Equals(rec.Name, name, StringComparison.OrdinalIgnoreCase))
                return rec;
        return null;
    }


    private void TryReadClasses()
    {        var rec = FindSection("Classes");
        if (rec is not { Size: >= 30 })
            return;

        int headerLen = -1;
        uint contentLen = 0;
        if (rec.Address + 20 <= Raw.Length)
        {
            uint v = BitConverter.ToUInt32(Raw, (int)rec.Address + 16);
            if (v == rec.Size - 38 && v < rec.Size)
            {
                headerLen = 20;
                contentLen = v;
            }
        }
        if (headerLen < 0 && rec.Address + 4 <= Raw.Length)
        {
            uint v = BitConverter.ToUInt32(Raw, (int)rec.Address);
            if (v == rec.Size - 6 && v < rec.Size)
            {
                headerLen = 4;
                contentLen = v;
            }
        }
        if (headerLen < 0)
        {
            _diagnostics.Add("CLASSES: no valid size anchor found.");
            return;
        }

        int contentOff = (int)rec.Address + headerLen;
        int endPos = Math.Min(contentOff + (int)contentLen, Raw.Length);
        var br = new BitReader(Raw, contentOff);
        try
        {
            while (br.BytePosition < endPos - 1)
            {
                int number = br.ReadBS();
                int proxyflag = br.ReadBS();
                string appname = br.ReadTV();
                string cppname = br.ReadTV();
                string dxfname = br.ReadTV();
                bool zombie = br.ReadB();
                int itemClassId = br.ReadBS();
                if (number == 0 && dxfname.Length == 0)
                    break;
                _classes.Add(new R2000Class(number, proxyflag, appname, cppname, dxfname, zombie, itemClassId));
            }
        }
        catch (Exception ex) when (ex is EndOfStreamException or IndexOutOfRangeException)
        {
            _diagnostics.Add($"CLASSES: stopped after {_classes.Count} records ({ex.Message}).");
        }

        if (_classes.Count == 0)
            _diagnostics.Add("CLASSES: decoded zero classes.");
    }

    public string? ResolveTypeName(int type)
    {
        if (type < 500)
            return R2000TypeNames.FixedTypeNames.TryGetValue(type, out var n) ? n : null;
        int idx = type - 500;
        return idx >= 0 && idx < _classes.Count ? _classes[idx].DxfName : null;
    }


    private void TryReadObjectMap()
    {
        var rec = FindSection("Handles");
        if (rec is null || rec.Size < 4)
        {
            _diagnostics.Add("HANDLES section missing.");
            return;
        }

        int pos = (int)rec.Address;
        int end = Math.Min((int)(rec.Address + rec.Size), Raw.Length);
        int guard = 0;

        while (pos + 2 <= end && guard++ < 4096)
        {
            int pageSize = (Raw[pos] << 8) | Raw[pos + 1];
            pos += 2;
            if (pageSize == 0 || pageSize > 2040 || pos + pageSize > end)
                break;
            ulong lastHandle = 0;
            long lastOffset = 0;
            int pageEnd = pos + pageSize - 2;
            while (pos < pageEnd)
            {
                ulong handleOff = R2000ObjectMap.ReadUmc(Raw, ref pos);
                if (handleOff == 0)
                    break;
                long off = R2000ObjectMap.ReadMc(Raw, ref pos);
                lastHandle += handleOff;
                lastOffset += off;
                if (lastOffset < 0 || lastOffset >= Raw.Length)
                    break;
                _handles.Add(lastHandle);
                _offsets.Add((int)lastOffset);
            }
            pos = pageEnd + 2;
        }

        if (_offsets.Count == 0)
            _diagnostics.Add("HANDLES: object map decoded empty.");
    }

public sealed class Ltype
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string PatternType { get; set; } = "";
    public double[] Pattern { get; set; } = Array.Empty<double>();
    public double Scale { get; set; } = 1.0;
}
}
