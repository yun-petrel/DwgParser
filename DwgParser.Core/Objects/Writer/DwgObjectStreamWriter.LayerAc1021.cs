using DwgParser.Core.IO;
using DwgParser.Core.Objects.Records;

namespace DwgParser.Core.Objects;

public static partial class DwgObjectStreamWriter
{
    private const string DefaultMaterialName = "Global";

    private static void StampLayerDefaultMaterial(DwgObjectReader.WalkResult walk, DwgVersion target)
    {
        bool upgrade = target == DwgVersion.AC1021 || target.IsR2010Plus();
        bool downgrade = target == DwgVersion.AC1018 || target == DwgVersion.AC1015;
        if (!upgrade && !downgrade) return;
        bool Match(DwgObjectRecord r) => r.LogicalType == TypeLayer && r.Layer is { HasName: true }
            && (upgrade ? r.SourceVersion is DwgVersion.AC1015 or DwgVersion.AC1018
                        : r.SourceVersion >= DwgVersion.AC1021);
        bool need = false;
        foreach (var r in walk.Records)
            if (Match(r)) { need = true; break; }
        if (!need) return;
        ulong h = FindDefaultMaterialHandle(walk);
        if (h == 0) return;
        foreach (var r in walk.Records)
            if (Match(r))
                r.Layer!.DefaultMaterialHandle = h;
        if (DebugTrace) System.Console.Error.WriteLine(
            $"LDEFMAT 默认材质 h={h:X} → 盖入 LAYER material 槽（{(upgrade ? "升版补槽" : "降版剥槽")}）");
    }

    private static ulong FindDefaultMaterialHandle(DwgObjectReader.WalkResult walk)
    {
        foreach (var r in walk.Records)
            if (r.LogicalType == TypeMaterial && r.Handle != 0
                && r.Material is { Captured: true } mc && mc.Name == DefaultMaterialName)
                return r.Handle;
        return 0;
    }

    internal static bool LayerR2007MaterialSlot(DwgObjectRecord rec, byte[] src, Target t)
        => (t.Version == DwgVersion.AC1021 || t.R2010Plus) && rec.SourceVersion == DwgVersion.AC1018
            && rec.LogicalType == TypeLayer
            && rec.Layer is { HasName: true, DefaultMaterialHandle: > 0 }
            && LayerSlotInsertPoint(rec, src) >= 0;

    private static long LayerSlotInsertPoint(DwgObjectRecord rec, byte[] src)
    {
        long hs = rec.HandleStreamStartBit, end = (long)rec.BodyEnd * 8;
        if (hs <= 0 || hs < rec.CarryStartBit || hs >= end) return -1;
        if (FindAfterNumReactors(rec, src, PrefixHasRl(rec.SourceVersion), out _, out _) <= rec.CarryStartBit)
            return -1;
        var br = new BitReader(src);
        long p = hs, last = -1;
        int xdicRefs = 0;
        while (p + 8 <= end)
        {
            DwgObjectRecord.SeekTo(br, p);
            int cc = br.ReadRC();
            int bits = 8 + (cc & 0xF) * 8;
            if (p + bits > end) break;
            int code = cc >> 4;
            if (code != 0)
            {
                last = p;
                if (code == 3) xdicRefs++;
            }
            p += bits;
        }
        return (last > hs && xdicRefs <= 1) ? last : -1;
    }

    internal static bool TryRewriteLayerR2007Handles(BitWriter handles, byte[] src,
        DwgObjectRecord rec, long hsStartBit, long dataEndBit)
    {
        long insertAt = LayerSlotInsertPoint(rec, src);
        if (insertAt < 0 || hsStartBit != rec.HandleStreamStartBit || dataEndBit != (long)rec.BodyEnd * 8)
            return false;
        var br = new BitReader(src);
        for (long p = hsStartBit; p + 8 <= dataEndBit; )
        {
            DwgObjectRecord.SeekTo(br, p);
            int cc = br.ReadRC();
            int bits = 8 + (cc & 0xF) * 8;
            if (p + bits > dataEndBit) break;
            if ((cc >> 4) != 3)
            {
                if (p == insertAt) handles.WriteHandleRef(5, rec.Layer!.DefaultMaterialHandle);
                CopyBits(handles, src, p, bits);
            }
            p += bits;
        }
        return true;
    }
}
