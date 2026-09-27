using DwgParser.Core.IO;

namespace DwgParser.Core.Objects;

public static partial class DwgObjectStreamWriter
{
    internal static bool LayoutR2007ShadeplotSlot(DwgObjectRecord rec, Target t)
        => t.Version >= DwgVersion.AC1021 && rec.SourceVersion == DwgVersion.AC1018
            && rec.LogicalType == TypeLayout && rec.Layout is { Captured: true };

    internal static bool TryInsertLayoutR2007Shadeplot(BitWriter handles, byte[] src,
        long hsStartBit, long copyLen)
    {
        if (copyLen < 40) return false;
        long bound = hsStartBit + copyLen;
        var br = new BitReader(src);
        long p = hsStartBit, insertAt = -1;

        bool Next(out int code, out long at)
        {
            code = -1; at = p;
            if (p + 8 > bound) return false;
            DwgObjectRecord.SeekTo(br, p);
            int cc = br.ReadRC();
            int bits = 8 + (cc & 0xF) * 8;
            if (p + bits > bound) return false;
            code = cc >> 4;
            p += bits;
            return true;
        }

        if (!Next(out int c0, out _) || (c0 != 4 && c0 != 12)) return false;
        if (!Next(out int c1, out _) || c1 != 4) return false;
        if (!Next(out int c2, out _)) return false;
        if (c2 == 3)
        {
            if (!Next(out int c3, out _)) return false;
            c2 = c3;
        }
        if (c2 != 5) return false;
        long afterPlotView = p;
        if (!Next(out int c4, out _) || c4 != 4) return false;
        insertAt = afterPlotView;
        if (insertAt <= hsStartBit) return false;
        CopyBits(handles, src, hsStartBit, (int)(insertAt - hsStartBit));
        handles.WriteHandleRef(4, 0);
        CopyBits(handles, src, insertAt, (int)(bound - insertAt));
        return true;
    }

    internal static bool LayoutR2007ShadeplotSlotDowngrade(DwgObjectRecord rec, Target t)
        => t.Version == DwgVersion.AC1018 && rec.SourceVersion >= DwgVersion.AC1021
            && rec.LogicalType == TypeLayout;

    internal static bool TryRemoveLayoutR2007Shadeplot(BitWriter handles, byte[] src,
        long hsStartBit, long copyLen)
    {
        if (copyLen < 40) return false;
        long bound = hsStartBit + copyLen;
        var br = new BitReader(src);
        long p = hsStartBit;

        bool Next(out int code, out int size)
        {
            code = -1; size = -1;
            if (p + 8 > bound) return false;
            DwgObjectRecord.SeekTo(br, p);
            int cc = br.ReadRC();
            int bits = 8 + (cc & 0xF) * 8;
            if (p + bits > bound) return false;
            code = cc >> 4;
            size = cc & 0xF;
            p += bits;
            return true;
        }

        if (!Next(out int c0, out _) || (c0 != 4 && c0 != 12)) return false;
        if (!Next(out int c1, out _) || c1 != 4) return false;
        if (!Next(out int c2, out _)) return false;
        if (c2 == 3)
        {
            if (!Next(out int c3, out _)) return false;
            c2 = c3;
        }
        if (c2 != 5) return false;
        long shadeStart = p;
        if (!Next(out int c4, out int n4) || c4 != 4 || n4 != 0) return false;
        long shadeEnd = p;
        if (!Next(out int c5, out _) || c5 != 4) return false;
        if (shadeStart <= hsStartBit || shadeEnd >= bound) return false;

        CopyBits(handles, src, hsStartBit, (int)(shadeStart - hsStartBit));
        CopyBits(handles, src, shadeEnd, (int)(bound - shadeEnd));
        return true;
    }
}
