using DwgParser.Core.IO;

namespace DwgParser.Core.Objects;

public static partial class DwgObjectStreamWriter
{
    public static bool DebugTrace =
        System.Environment.GetEnvironmentVariable("DWGDBG") == "1";

    public sealed class Result
    {
        public byte[] Data = Array.Empty<byte>();
        public List<(ulong Handle, long Offset)> Entries = new();
        public int ReencodedEntities;
        public int CarriedObjects;
        public int SkippedNoHandle;
        public int SkippedDuplicateHandle;
        public int BypassedObjects;
        public int DroppedObjects;
    }

    internal readonly struct Target
    {
        public Target(DwgVersion v)
        {
            Version = v;
            R2000 = v.IsR2000();
            R2007Plus = v >= DwgVersion.AC1021;
            R2010Plus = v.IsR2010Plus();
            R2013Plus = v >= DwgVersion.AC1027;
            UnicodeStrings = v >= DwgVersion.AC1021;
        }
        public readonly DwgVersion Version;
        public readonly bool R2000;
        public readonly bool R2007Plus;
        public readonly bool R2010Plus;
        public readonly bool R2013Plus;
        public readonly bool UnicodeStrings;
    }

    private const short TypeInsert = 0x07;
    private const short TypeMInsert = 0x08;
    private const short TypeText = 0x01;
    private const short TypeAttrib = 0x02;
    private const short TypeAttDef = 0x03;
    private const short TypeMText = 0x2C;
    private const short TypeDimOrdinate = 0x14;
    private const short TypeDimDiameter = 0x1A;
    private const short TypeHatch = 0x4E;
    private const short TypeViewport = 0x22;
    private const short TypeTolerance = 0x2E;
    private const short TypeMline = 0x2F;
    private const short TypeLine = 0x13;
    private const short TypeBlockControl = 0x30;
    private const short TypeDimStyleControl = 0x44;
    private const short TypeLtypeControl = 0x38;
    private const short TypeMLineStyle = 0x49;
    private const short TypeLayout = 0x52;
    private const short TypeMLeaderStyle = 507;
    private const short TypeMLeader = 534;
    private const short TypeMaterial = 508;
    private const short TypeVisualStyle = 509;
    private const short TypeSectionViewStyle = 510;
    private const short TypeDetailViewStyle = 511;
    private const short TypeBlockEntity = 0x04;
    private const short TypeBlockHeader = 0x31;
    private const short TypeLType = 0x39;
    internal const short TypeDimStyle = 0x45;
    private const short TypeStyle = 0x35;
    private const short TypeAppId = 0x43;
    private const short TypeTableStyle = 0x1F8;
    private const short TypeCellStyleMap = 0x200;
    private const short TypeScale = 0x1FA;
    private const short TypeTable = 0x212;
    private const short TypeDataLink = 565;
    private const short TypeTableData = 0x215;
    internal static readonly byte[] R2010bConst =
    {
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x0B, 0x28, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x02, 0xCA, 0x01,
    };
    private const short TypeLayer = 0x33;

    public static Dictionary<ulong, long>? FrameOffsets;

    public static Result Build(DwgObjectReader.WalkResult walk, DwgVersion target)
    {
        var res = new Result();
        byte[] src = walk.SourceData ?? Array.Empty<byte>();
        var t = new Target(target);

        MLeaderCtxDroppedHandles.Clear();
        if (!t.R2007Plus)
            foreach (var r in walk.Records)
                if (DropsMLeaderContextDataOnDowngrade(r, t)) MLeaderCtxDroppedHandles.Add(r.Handle);

        var ltypeTable = CaptureLtypeEntries(walk, src);
        StampVportDefaultVisualStyle(walk, src, target);
        StampLayerDefaultMaterial(walk, target);
        StampProxyClassNames(walk);
        StampBlockHeaderOwnedVectors(walk, src, target);
        SyncOwnedVectorsForDrops(walk, src);
        StampLayoutViewportVectors(walk, src, target);

        var ordered = new List<DwgObjectRecord>(walk.Records.Count);
        var seenHandles = new HashSet<ulong>();
        foreach (var r in walk.Records.OrderBy(r => t.R2000 && r.BodyStart == 0 ? long.MaxValue : r.BodyStart))
        {
            if (r.Handle == 0) { res.SkippedNoHandle++; continue; }
            if (!seenHandles.Add(r.Handle)) { res.SkippedDuplicateHandle++; continue; }
            ordered.Add(r);
        }

        var main = new BitWriter(Math.Max(src.Length, 4096));
        if (t.R2000)
        {
            main.WriteRC(0);
        }
        else
        {
            main.WriteRL(0x0DCA);
        }

        foreach (var rec in ordered)
        {
            if (DropsDataTableOnUpgrade(rec, t))
            {
                res.DroppedObjects++;
                continue;
            }
            if (DropsTableContentOnUpgrade(rec, t))
            {
                res.DroppedObjects++;
                continue;
            }
            if (DropsMLeaderContextDataOnDowngrade(rec, t))
            {
                res.DroppedObjects++;
                continue;
            }
            if (WriteSideEdits.IsDropped(rec.Handle))
            {
                res.DroppedObjects++;
                continue;
            }
            main.AlignByte();
            long entryOffset = main.BytePosition;
            if (FrameOffsets != null) FrameOffsets[rec.Handle] = main.BitPosition;
            if (OracleFrameBypass.TryGetBypassFrame(target, rec.Handle, out byte[] oracleFrame))
            {
                main.WriteBitsFrom(new BitReader(oracleFrame, 0), oracleFrame.Length * 8);
                res.Entries.Add((rec.Handle, entryOffset));
                res.BypassedObjects++;
                OracleFrameBypass.LastBypassed.Add(rec.Handle);
                continue;
            }
            if (!WriteObject(main, rec, src, t, walk.ClassTypeMap, ltypeTable, walk))
                continue;
            res.Entries.Add((rec.Handle, entryOffset));
            if (CanReencode(rec, target)) res.ReencodedEntities++;
            else res.CarriedObjects++;
        }

        res.Data = main.ToArray();
        return res;
    }

    private static void SyncOwnedVectorsForDrops(DwgObjectReader.WalkResult walk, byte[] src)
    {
        if (OracleFrameBypass.DropHandles.Count == 0 && WriteSideEdits.DroppedHandles.Count == 0)
            return;
        _ = src;
        foreach (ulong h in WriteSideEdits.AllDropped())
        {
            var (headers, removed) = BlockHeaderOwnedSync.RemoveFromDecodedVectors(walk, h);
            if (DebugTrace)
                System.Console.Error.WriteLine(
                    $"OWNSYNC h={h:X} 已解码向量: {headers} 头/{removed} 枚");
        }
    }

    private static bool CanReencode(DwgObjectRecord rec, DwgVersion target)
        => rec.CanReencodeEntity && rec.SourceVersion != target
           && rec.SourceVersion.IsSupportedVersion() && target.IsSupportedVersion()
           && rec.LogicalType is not (TypeBlockControl or TypeLtypeControl or TypeDimStyleControl);

    internal static bool SameObjectFrameFormat(DwgVersion a, DwgVersion b)
    {
        if (a == b) return true;
        bool famA = a.IsR2000(), famB = b.IsR2000();
        if (famA != famB) return false;
        return (a >= DwgVersion.AC1021) == (b >= DwgVersion.AC1021)
            && a.IsR2010Plus() == b.IsR2010Plus()
            && (a >= DwgVersion.AC1027) == (b >= DwgVersion.AC1027)
            && (a >= DwgVersion.AC1032) == (b >= DwgVersion.AC1032);
    }

    private const short TypeDataTableCode = 533;

    private const string MLeaderObjectContextDataClass = "ACDB_MLEADEROBJECTCONTEXTDATA_CLASS";

    internal static readonly System.Collections.Generic.HashSet<ulong> MLeaderCtxDroppedHandles = new();

    private static bool DropsMLeaderContextDataOnDowngrade(DwgObjectRecord rec, Target t)
        => (t.Version == DwgVersion.AC1021 || t.Version == DwgVersion.AC1018)
           && rec.SourceVersion.IsR2010Plus()
           && string.Equals(rec.ClassName, MLeaderObjectContextDataClass,
                            System.StringComparison.OrdinalIgnoreCase);

    private static bool DropsTableContentOnUpgrade(DwgObjectRecord rec, Target t)
        => (t.R2010Plus && !rec.SourceVersion.IsR2010Plus()
            || t.Version == DwgVersion.AC1015 && rec.SourceVersion.IsR2010Plus())
           && (rec.LogicalType == TypeTableContentCode || rec.LogicalType == TypeTableGeometryCode
               || string.Equals(rec.ClassName, "FIELD", System.StringComparison.OrdinalIgnoreCase)
               || string.Equals(rec.ClassName, "ACAD_FIELD", System.StringComparison.OrdinalIgnoreCase)
               || string.Equals(rec.ClassName, "FIELDLIST", System.StringComparison.OrdinalIgnoreCase));

    private const short TypeTableGeometryCode = 0x214;

    private static bool DropsDataTableOnUpgrade(DwgObjectRecord rec, Target t)
    {
        bool hit = rec.LogicalType == TypeDataTableCode
                   && !rec.SourceVersion.IsR2007Plus()
                   && t.R2007Plus;
        if (System.Environment.GetEnvironmentVariable("DWGDBG") == "1"
            && rec.LogicalType == TypeDataTableCode)
            System.Console.Error.WriteLine(
                $"DTCHK h={rec.Handle:X} tc={rec.TypeCode} lt={rec.LogicalType} cn={rec.ClassName} " +
                $"sv={rec.SourceVersion} dst={t.Version} hit={hit}");
        return hit;
    }
}
