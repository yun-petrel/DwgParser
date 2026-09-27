using DwgParser.Core.IO;

namespace DwgParser.Core.Objects;

internal static class SyntheticDrawingBuilder
{
    public const double RefRectWidth = 13.090303862899901;
    public const double RefTextHeight = 2.5;
    public const double RefExtentsHeight = 3.2960893854748625;
    public const double RefExtentsWidth = 13.090303862899901;
    public const string RefMText = "大家好！";

    public static readonly double[] RefRectX =
        { 9.137087793335148, -0.23353104941371505, -0.23353104941371505, 9.137087793335148 };
    public static readonly double[] RefRectY =
        { 6.0460515508320665, 6.0460515508320665, -0.17242636972395786, -0.17242636972395786 };

    public readonly record struct Result(ulong MTextHandle, ulong PolylineHandle, ulong HostBlockHeader);

    public static Result Append(DwgObjectReader.WalkResult walk,
        bool withMText = true, bool withPolyline = true,
        System.Text.Encoding? legacyEncoding = null)
    {
        if (walk.Records.Count == 0) throw new InvalidOperationException("空遍历结果，无法定位宿主块头。");
        ulong ms = ResolveModelSpace(walk);
        var (layer, style, colorPoly, colorText) = PickStyleAndLayer(walk);
        ulong next = walk.Records.Max(r => r.Handle) + 1;
        ulong hMText = 0, hRect = 0;
        var added = new List<ulong>(2);

        if (withMText)
        {
            hMText = next++;
            walk.Records.Add(NewMTextRecord(hMText, ms, layer, style, colorText,
                walk.Records[0].SourceVersion, 0, 0, RefMText, RefTextHeight, legacyEncoding));
            added.Add(hMText);
        }

        if (withPolyline)
        {
            hRect = next;
            walk.Records.Add(new DwgObjectRecord
            {
                Handle = hRect, TypeCode = 0x4D, LogicalType = 0x4D,
                Kind = DwgObjectRecordKind.Entity, SourceVersion = walk.Records[0].SourceVersion,
                OwnerHandle = ms, LayerHandle = layer, EntMode = 2, ColorIndex = colorPoly,
                HasSyntheticFrame = true,
                Synthetic = new SyntheticFrame
                {
                    Kind = SynthEntityKind.LwPolyline,
                    Polyline = new SyntheticPolyline
                    {
                        Closed = true,
                        X = (double[])RefRectX.Clone(),
                        Y = (double[])RefRectY.Clone(),
                    },
                },
            });
            added.Add(hRect);
        }

        BlockHeaderOwnedSync.AttachToDecodedVectors(walk, added);
        WriteSideEdits.MarkObjectStreamEdited();
        WriteSideEdits.SetEditedWalk(walk);

        return new Result(hMText, hRect, ms);
    }

    public static ulong AppendMText(DwgObjectReader.WalkResult walk, double x, double y,
        string text, double height, System.Text.Encoding? legacyEncoding = null,
        DwgVersion? target = null)
    {
        if (walk.Records.Count == 0) throw new InvalidOperationException("空遍历结果，无法定位宿主块头。");
        ulong ms = ResolveModelSpace(walk);
        var (layer, style, _, colorText) = PickStyleAndLayer(walk);
        ulong h = walk.Records.Max(r => r.Handle) + 1;
        if (target is { } tg && tg.IsR2000())
        {
            var src = walk.SourceData ?? Array.Empty<byte>();
            var btr = walk.Records.FirstOrDefault(r => r.Handle == ms);
            if (btr is null) return 0;
            int headWidth = BlockHeaderOwnedSync.ChainHeadRefWidth(src, btr);
            if (headWidth > 0 && h >> (headWidth * 8) != 0) h = HandleFittingWidth(walk, headWidth);
        }
        var rec = NewMTextRecord(h, ms, layer, style, colorText,
            walk.Records[0].SourceVersion, x, y, text, height, legacyEncoding, target);
        if (target is { } tg2 && tg2.IsR2000())
        {
            var btr = walk.Records.FirstOrDefault(r => r.Handle == ms);
            if (btr is null || h == 0) return 0;
            ulong? oldFirst = BlockHeaderOwnedSync.TryRetargetChainHead(
                walk.SourceData ?? Array.Empty<byte>(), btr, h);
            if (oldFirst is null) return 0;
            btr.DataRegionRewritten = true;
            rec.NoLinks = false;
            rec.PrevRef = new DwgHandleReference(4, 0);
            rec.NextRef = new DwgHandleReference(4, oldFirst.Value);
        }
        walk.Records.Add(rec);
        if (target is { } tg3 && !tg3.IsR2000()
            && walk.Records.FirstOrDefault(r => r.Handle == ms)?.BlockOwnedHandles is null)
            walk.Entities.Add(new DwgEntity { Handle = h, Kind = DwgEntityType.MText,
                OwnerHandle = ms, TextValue = text });
        BlockHeaderOwnedSync.AttachToDecodedVectors(walk, new[] { h });
        WriteSideEdits.MarkObjectStreamEdited();
        WriteSideEdits.SetEditedWalk(walk);
        return h;
    }

    private static ulong HandleFittingWidth(DwgObjectReader.WalkResult walk, int widthBytes)
    {
        var used = new HashSet<ulong>(walk.Records.Select(r => r.Handle));
        ulong cap = widthBytes >= 8 ? ulong.MaxValue : (1UL << (widthBytes * 8)) - 1;
        for (ulong h = 2; h <= cap; h++)
            if (!used.Contains(h)) return h;
        return 0;
    }

    private static DwgObjectRecord NewMTextRecord(ulong handle, ulong owner, ushort layer,
        ulong style, short color, DwgVersion srcVer, double x, double y, string text,
        double height, System.Text.Encoding? legacyEncoding, DwgVersion? target = null)
        => new()
        {
            Handle = handle, TypeCode = 0x2C, LogicalType = 0x2C,
            Kind = DwgObjectRecordKind.Entity, SourceVersion = srcVer,
            OwnerHandle = owner, LayerHandle = layer,
            EntMode = 2, NoLinks = !(target is { } tg && tg.IsR2000()), ColorIndex = color,
            HasStyleSlot = true, StyleRef = new DwgHandleReference(5, style),
            HasSyntheticFrame = true,
            Synthetic = new SyntheticFrame
            {
                Kind = SynthEntityKind.MText,
                MText = new SyntheticMText
                {
                    InsX = x, InsY = y, InsZ = 0,
                    ExtrusionX = 0, ExtrusionY = 0, ExtrusionZ = 1,
                    XAxisX = 1, XAxisY = 0, XAxisZ = 0,
                    RectWidth = RefRectWidth,
                    TextHeight = height,
                    Attachment = 1,
                    FlowDir = 0,
                    ExtentsHeight = RefExtentsHeight,
                    ExtentsWidth = RefExtentsWidth,
                    Text = text,
                    LegacyEncoding = legacyEncoding,
                },
            },
        };

    private static ulong ResolveModelSpace(DwgObjectReader.WalkResult walk)
    {
        if (walk.ModelSpaceHandle != 0) return walk.ModelSpaceHandle;
        return walk.Entities.Where(e => e.OwnerHandle != 0)
            .GroupBy(e => e.OwnerHandle).OrderByDescending(g => g.Count())
            .FirstOrDefault()?.Key ?? 0;
    }

    private static (ushort Layer, ulong Style, short ColorPoly, short ColorText) PickStyleAndLayer(DwgObjectReader.WalkResult walk)
    {
        var donor = walk.Entities.FirstOrDefault(e => e.TextStyleHandle != 0)
            ?? walk.Entities.FirstOrDefault(e => e.LayerHandle != 0)
            ?? walk.Entities.FirstOrDefault();
        ushort layer = donor is { LayerHandle: not 0 } ? (ushort)donor.LayerHandle : (ushort)0x10;
        ulong style = donor is { TextStyleHandle: not 0 } ? donor.TextStyleHandle : 0x11;
        short Follow(Func<DwgEntity, bool> pred)
        {
            var d = walk.Entities.FirstOrDefault(pred) ?? donor;
            return d is not null && d.ColorIndex is > 0 and < 256 ? d.ColorIndex : (short)256;
        }
        short colorPoly = Follow(e => e.Kind == DwgEntityType.LwPolyline);
        short colorText = Follow(e => e.Kind is DwgEntityType.Text or DwgEntityType.MText);
        return (layer, style, colorPoly, colorText);
    }
}
