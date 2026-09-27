using DwgParser.Core.IO;

namespace DwgParser.Core.Objects;

public static partial class DwgObjectStreamWriter
{
    private readonly record struct HandleRef(long Start, long End, int Code, ulong Val);

    private static void StampLayoutViewportVectors(DwgObjectReader.WalkResult walk, byte[] src, DwgVersion target)
    {
        if (target != DwgVersion.AC1018) return;
        foreach (var rec in walk.Records)
        {
            if (rec.LogicalType != TypeLayout || rec.Layout is not { Captured: true } cap) continue;
            if (rec.SourceVersion < DwgVersion.AC1015 || rec.SourceVersion >= DwgVersion.AC1018) continue;
            if (!TrySourceDataEnd(src, rec, out long hs)) continue;
            int nr = ReadSourceNumReactors(src, rec);
            if (nr is < 0 or > 32) continue;
            var refs = ReadHandleRefs(src, hs, (long)rec.BodyEnd * 8, nr + 4);
            int x = 1 + nr;
            if (refs.Count != x + 3 || refs[x].Code != 3 || refs[x + 1].Code != 4 || refs[x + 2].Code != 4)
                continue;
            ulong bh = refs[x + 1].Val;
            if (walk.Records.FirstOrDefault(r => r.LogicalType == TypeBlockHeader && r.Handle == bh)
                    ?.BlockOwnedHandles is not { } owned)
                continue;
            var vps = new List<ulong>();
            foreach (var h in owned)
                if (walk.Entities.FirstOrDefault(e => e.Handle == h)?.Kind == DwgEntityType.Viewport)
                    vps.Add(h);
            if (vps.Count > 64) continue;
            cap.XdicEndBit = refs[x].End;
            cap.LastRefEndBit = refs[x + 2].End;
            cap.LayoutBlockHandle = bh;
            cap.ViewportHandles = vps;
            if (DebugTrace)
                System.Console.Error.WriteLine(
                    $"LAYSTAMP h={rec.Handle:X} nr={nr} xdicEnd={refs[x].End} 末枚止={refs[x + 2].End}"
                    + $" 块头={bh:X} active={refs[x + 2].Val:X} 视口=[{string.Join(",", vps.Select(v => v.ToString("X")))}]");
        }
    }

    private static void CopyLayoutR2004HandleRegion(BitWriter body, byte[] src, long hs, Records.LayoutCapture cap)
    {
        CopyBits(body, src, hs, (int)(cap.XdicEndBit - hs));
        body.WriteHandleRef(5, 0);
        CopyBits(body, src, cap.XdicEndBit, (int)(cap.LastRefEndBit - cap.XdicEndBit));
        body.WriteHandleRef(5, 0);
        body.WriteHandleRef(5, 0);
        foreach (var vp in cap.ViewportHandles!)
            body.WriteHandleRef(4, vp);
    }

    private static int ReadSourceNumReactors(byte[] src, DwgObjectRecord rec)
    {
        if (rec.CarryStartBit <= 0) return -1;
        var br = new BitReader(src);
        DwgObjectRecord.SeekTo(br, rec.CarryStartBit);
        _ = br.ReadRL();
        br.ReadHandle();
        if (!DwgObjectReader.TrySkipEed(br)) return -1;
        int nr = br.ReadBL();
        return nr;
    }

    private static List<HandleRef> ReadHandleRefs(byte[] src, long hs, long end, int max)
    {
        var list = new List<HandleRef>();
        var br = new BitReader(src);
        long p = hs;
        while (p + 8 <= end && list.Count < max)
        {
            DwgObjectRecord.SeekTo(br, p);
            int cc = br.ReadRC();
            int size = cc & 0xF;
            long next = p + 8 + size * 8L;
            if (cc >> 4 == 0 || next > end) break;
            ulong v = 0;
            for (int i = 0; i < size; i++) v = (v << 8) | br.ReadRC();
            list.Add(new HandleRef(p, next, cc >> 4, v));
            p = next;
        }
        return list;
    }
}
