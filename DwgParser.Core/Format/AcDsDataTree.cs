using System.Buffers.Binary;
using DwgParser.Core.Objects;

namespace DwgParser.Core.Format;

public sealed class AcDsDataTree
{
    private const int SegmentHeaderSize = 48;
    private const ushort SegmentSignature = 0xD5AC;

    private static readonly byte[] AcisStart = "ACIS "u8.ToArray();

    private static readonly byte[] AcisEnd = "End-of-ACIS"u8.ToArray();

    private readonly byte[] _data;
    private readonly List<Segment> _segments = new();

    public uint FileHeaderSize { get; private set; }
    public uint DsVersion { get; private set; }
    public uint SegidxOffset { get; private set; }
    public uint NumSegidx { get; private set; }
    public uint SchidxSegidx { get; private set; }
    public uint DatidxSegidx { get; private set; }
    public uint SearchSegidx { get; private set; }
    public uint PrvsavSegidx { get; private set; }
    public uint FileSize { get; private set; }

    public IReadOnlyList<Segment> Segments => _segments;

    private AcDsDataTree(byte[] data) => _data = data;

    public enum SegmentKind
    {
        Unknown, Segidx, Datidx, Data, Schidx, Schdat, Search, Blob01, Prvsav, Freesp
    }

    public sealed class Segment
    {
        public string Name = "";
        public SegmentKind Kind;
        public uint Offset;
        public uint SegSize;
        public uint DataAlgnOffset;
        public uint ObjDataAlgnOffset;
    }

    public static bool TryParse(byte[]? acdsBytes, out AcDsDataTree? tree)
    {
        tree = null;
        var d = acdsBytes;
        if (d is null || d.Length < 56) return false;

        uint u32(int o) => BinaryPrimitives.ReadUInt32LittleEndian(d.AsSpan(o));
        ulong u64(int o) => BinaryPrimitives.ReadUInt64LittleEndian(d.AsSpan(o));

        var t = new AcDsDataTree(d)
        {
            FileHeaderSize = u32(4),
            DsVersion      = u32(5 * 4),
            SegidxOffset   = u32(6 * 4),
            NumSegidx      = u32(8 * 4),
            SchidxSegidx   = u32(9 * 4),
            DatidxSegidx   = u32(10 * 4),
            SearchSegidx   = u32(11 * 4),
            PrvsavSegidx   = u32(12 * 4),
            FileSize       = u32(13 * 4),
        };

        if (t.SegidxOffset + SegmentHeaderSize > (uint)d.Length) return false;
        if (t.NumSegidx == 0 || t.NumSegidx > 4096) return false;
        if (t.FileHeaderSize > t.SegidxOffset) return false;

        int idxBase = (int)t.SegidxOffset + SegmentHeaderSize;
        long need = idxBase + (long)t.NumSegidx * 12;
        if (need > d.Length) return false;

        for (int i = 0; i < t.NumSegidx; i++)
        {
            int slot = idxBase + i * 12;
            uint off = (uint)u64(slot);
            if (off == 0) continue;
            var seg = ReadSegmentHeader(d, off);
            if (seg is not null) t._segments.Add(seg);
        }

        tree = t;
        return true;
    }

    private static Segment? ReadSegmentHeader(byte[] d, uint off)
    {
        if (off + SegmentHeaderSize > d.Length) return null;
        int o = (int)off;
        ushort sig = BinaryPrimitives.ReadUInt16LittleEndian(d.AsSpan(o));
        if (sig != SegmentSignature) return null;
        string name = System.Text.Encoding.ASCII.GetString(d, o + 2, 6).TrimEnd('\0');
        var seg = new Segment
        {
            Offset = off,
            Name = name,
            Kind = name switch
            {
                "segidx"  => SegmentKind.Segidx,
                "datidx"  => SegmentKind.Datidx,
                "_data_"  => SegmentKind.Data,
                "schidx"  => SegmentKind.Schidx,
                "schdat"  => SegmentKind.Schdat,
                "search"  => SegmentKind.Search,
                "blob01"  => SegmentKind.Blob01,
                "prvsav"  => SegmentKind.Prvsav,
                "freesp"  => SegmentKind.Freesp,
                _         => SegmentKind.Unknown,
            },
            SegSize           = BinaryPrimitives.ReadUInt32LittleEndian(d.AsSpan(o + 8 + 2 * 4)),
            DataAlgnOffset    = BinaryPrimitives.ReadUInt32LittleEndian(d.AsSpan(o + 8 + 6 * 4)),
            ObjDataAlgnOffset = BinaryPrimitives.ReadUInt32LittleEndian(d.AsSpan(o + 8 + 7 * 4)),
        };
        return seg;
    }

    public bool TryGetAcisBlob(ulong objHandle, out byte[] blob)
    {
        blob = System.Array.Empty<byte>();
        if (objHandle == 0) return false;
        var d = _data;

        for (int pos = 0; pos + 8 <= d.Length; pos++)
        {
            if (BinaryPrimitives.ReadUInt64LittleEndian(d.AsSpan(pos)) != objHandle) continue;

            int winFrom = pos + 8;
            int winTo = System.Math.Min(winFrom + 128, d.Length);
            int s = IndexOf(d, AcisStart, winFrom, winTo);
            if (s < 0) continue;
            if (s - 4 < winFrom) continue;
            uint len = BinaryPrimitives.ReadUInt32LittleEndian(d.AsSpan(s - 4));
            if (len < AcisEnd.Length + 8 || s + len > d.Length) continue;

            int end = s + (int)len;
            if (IndexOf(d, AcisEnd, s, end) < 0) continue;

            blob = new byte[len];
            System.Array.Copy(d, s, blob, 0, (int)len);
            return true;
        }
        return false;
    }

    private static int IndexOf(byte[] hay, byte[] needle, int from, int to)
    {
        if (needle.Length == 0 || to - from < needle.Length) return -1;
        for (int i = from; i + needle.Length <= to; i++)
        {
            int j = 0;
            while (j < needle.Length && hay[i + j] == needle[j]) j++;
            if (j == needle.Length) return i;
        }
        return -1;
    }

    public static int AttachGeometry(DwgObjectReader.WalkResult? walk, byte[]? acdsBytes)
    {
        if (walk is null) return 0;
        try
        {
            if (!TryParse(acdsBytes, out var tree) || tree is null) return 0;
            int n = 0;
            foreach (var e in walk.Entities)
            {
                if (e is null || e.AcisData is not null) continue;
                if (e.Kind is not (DwgEntityType.Region or DwgEntityType.Solid3d or DwgEntityType.Body))
                    continue;
                if (tree.TryGetAcisBlob(e.Handle, out var blob))
                {
                    e.AcisData = blob;
                    if (AcisSab.TryReadVertices(blob, out var verts))
                        e.AcisVertices = verts;
                    n++;
                }
            }
            return n;
        }
        catch (System.Exception ex) when (ex is not System.OperationCanceledException)
        {
            return 0;
        }
    }
}
