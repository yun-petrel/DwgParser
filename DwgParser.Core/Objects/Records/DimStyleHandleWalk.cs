using DwgParser.Core.IO;

namespace DwgParser.Core.Objects.Records;

internal static class DimStyleHandleWalk
{
    private static readonly string[] TrailingLabels =
    {
        "DIMTXSTY", "DIMLDRBLK", "DIMBLK", "DIMBLK1", "DIMBLK2",
        "DIMLTYPE", "DIMLTEX1", "DIMLTEX2",
    };

    public static void Capture(DimStyleCapture cap, byte[]? src, long hs, long bodyEndBit)
    {
        Fail(cap, "");
        if (src is null) { Fail(cap, "无源缓冲"); return; }
        long len = bodyEndBit - hs;
        if (hs <= 0 || len < 8 || len > 8 * 8192) { Fail(cap, "句柄流区间非法"); return; }
        try
        {
            var br = new BitReader(src);
            var list = new System.Collections.Generic.List<DimStyleCapture.DimStyleHandleRef>();
            long p = hs;
            while (p + 8 <= bodyEndBit)
            {
                DwgObjectRecord.SeekTo(br, p);
                byte rc = br.ReadRC();
                int code = rc >> 4, size = rc & 0xF;
                int bits = 8 + size * 8;
                if (p + bits > bodyEndBit) break;
                ulong val = 0;
                for (int i = 0; i < size; i++) val = (val << 8) | br.ReadRC();
                ulong resolved = code switch
                {
                    6 => cap.OwnHandle + 1,
                    8 => cap.OwnHandle - 1,
                    10 => cap.OwnHandle + val,
                    12 => cap.OwnHandle - val,
                    14 => cap.OwnHandle,
                    _ => val,
                };
                list.Add(new DimStyleCapture.DimStyleHandleRef(p, bits, code, val, resolved, ""));
                p += bits;
                if (list.Count > 8192) { Fail(cap, "引用枚数越界"); return; }
            }
            if (list.Count == 0) { Fail(cap, "零引用"); return; }
            if (bodyEndBit - p >= 8) { Fail(cap, "末枚引用后有完整字节残留"); return; }

            int tail = System.Math.Min(TrailingLabels.Length, list.Count - 1);
            for (int i = list.Count - tail; i < list.Count; i++)
            {
                var r = list[i];
                list[i] = r with { Slot = TrailingLabels[TrailingLabels.Length - (list.Count - i)] };
            }
            var first = list[0];
            list[0] = first with { Slot = "owner" };
            for (int i = 1; i < list.Count - tail; i++)
            {
                var r = list[i];
                list[i] = r with { Slot = $"dimstyle_ent_{i - 1:D2}" };
            }
            cap.HandleRefs = list;
            cap.HandleRefsCaptured = true;
            cap.HandleRefsFailReason = "";
        }
        catch (System.Exception ex) { Fail(cap, ex.GetType().Name); }
    }

    private static void Fail(DimStyleCapture cap, string why)
    {
        cap.HandleRefs = new System.Collections.Generic.List<DimStyleCapture.DimStyleHandleRef>();
        cap.HandleRefsCaptured = false;
        cap.HandleRefsFailReason = why;
    }
}
