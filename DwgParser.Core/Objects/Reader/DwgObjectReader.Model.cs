using System.Text;
using DwgParser.Core.IO;
using DwgParser.Core.Objects.Records;
using DwgParser.Core.Render;

namespace DwgParser.Core.Objects;

public static partial class DwgObjectReader
{

    public readonly record struct DimFieldEntry(string Name, long AbsBitAfter, long RelBitAfter, string Value);

    [ThreadStatic] private static Dictionary<ulong, IReadOnlyList<DimFieldEntry>>? _dimTraces;

    public static bool TraceDimensions { get; set; }

    public static IReadOnlyDictionary<ulong, IReadOnlyList<DimFieldEntry>>? DimensionTraces => _dimTraces;

    public sealed partial class WalkResult
    {
        public List<DwgEntity> Entities { get; } = new();

        public List<DwgObjectRecord> Records { get; } = new();

        public byte[]? SourceData { get; internal set; }

        public IReadOnlyList<DwgParser.Core.Format.R2000Class>? Classes { get; set; }

        public Dictionary<DwgObjectRecord, int> FrameEnds { get; } = new();

        public IReadOnlyList<IEntity> EntitiesGeneric
            => Entities;

        public List<BlockRegion> BlockRegions { get; } = new();

        public Dictionary<ulong, BlockRegion> BlockHeaderByHandle { get; } = new();

        public List<BlockEntity> BlockBegins { get; } = new();
        public int ObjectsSeen { get; set; }
        public int ObjectsDecoded { get; set; }
        public int Errors { get; set; }

        public long RecordsBaselineFingerprint { get; internal set; }

        public long RecordsFingerprint()
        {
            ulong h = 14695981039346656037UL;
            foreach (var r in Records)
            {
                h ^= r.Handle;
                h *= 1099511628211UL;
            }
            return (long)h;
        }

        public ulong ModelSpaceHandle { get; set; }
        public ulong PaperSpaceHandle { get; set; }

        public HashSet<ulong> LiveBlockHeaders { get; } = new();

        public List<ulong> BlockControlEntries { get; } = new();

        public Dictionary<ulong, short> LayerColors { get; } = new();

        public Dictionary<ulong, string> LayerNames { get; } = new();

        public Dictionary<ulong, string> LayerLinetypes { get; } = new();

        public LayerTable Layers => _layers ??= new LayerTable(
            Records.OrderBy(r => r.Handle)
                   .Where(r => r.LogicalType == LayerTable.LayerTypeCode
                               && r.Layer is { HasName: true })
                   .Select(r => (ILayer)r.Layer!));

        private LayerTable? _layers;

        public Dictionary<ulong, string> StyleNames { get; } = new();

        public Dictionary<ulong, TextStyleInfo> StyleFonts { get; } = new();

        public Dictionary<ulong, DimStyleInfo> DimStyles { get; } = new();

        public Dictionary<ulong, LinetypeInfo> LtypeStyles { get; } = new();

        public Dictionary<short, int> TypeCounts { get; } = new();

        public Dictionary<int, short> ClassTypeMap { get; } = new();

        public static string TypeName(short type) => type switch
        {
            0x01 => "Text", 0x02 => "Attrib", 0x03 => "AttDef",
            0x05 => "EndBlk", 0x06 => "SeqEnd", 0x07 => "Insert", 0x08 => "MInsert",
            0x0A => "Vertex2D", 0x0F => "Polyline2D",
            0x11 => "Arc", 0x12 => "Circle", 0x13 => "Line", 0x1B => "Point",
            0x1C => "3dFace", 0x1F => "Solid", 0x20 => "Trace",
            0x2C => "MText", 0x4D => "LwPolyline", 0x4E => "Hatch",
            0x14 => "DimOrdinate", 0x15 => "DimLinear", 0x16 => "DimAligned",
            0x17 => "DimAng3pt", 0x18 => "DimAng2ln", 0x19 => "DimRadius",
            0x1A => "DimDiameter",
            0x22 => "Viewport", 0x23 => "Ellipse", 0x24 => "Spline",
            0x25 => "Region", 0x26 => "Solid3d", 0x27 => "Body",
            0x28 => "Ray", 0x29 => "XLine", 0x2D => "Leader",
            0x2E => "Tolerance", 0x2F => "Mline",
            0x212 => "Table", 0x216 => "MLeader",
            0x30 => "BlockControl", 0x31 => "BlockHeader",
            0x32 => "LayerControl", 0x33 => "Layer", 0x34 => "StyleControl", 0x35 => "Style",
            _ => $"Unknown(0x{type:X2})",
        };

        internal void CountType(short type)
        {
            TypeCounts.TryGetValue(type, out int c);
            TypeCounts[type] = c + 1;
        }
    }
}
