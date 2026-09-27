using DwgParser.Core.IO;

namespace DwgParser.Core.Objects;

public static partial class DwgObjectStreamWriter
{
    private static bool CarriesDataLinkHandlesUp(DwgObjectRecord rec, Target t)
        => rec.LogicalType == TypeDataLink
           && rec.SourceVersion <= DwgVersion.AC1018
           && t.Version >= DwgVersion.AC1021;

    private static bool RebuildDataLinkHandlesUp(BitWriter handles, byte[] src,
        long hsStart, long hsEnd, DwgObjectRecord rec)
    {
        var refs = new List<(int Code, ulong Val, long Bits, bool NullXdic)>();
        var br = new BitReader(src);
        long hp = hsStart;
        bool headRelative = false;
        while (hp + 8 <= hsEnd)
        {
            DwgObjectRecord.SeekTo(br, hp);
            int cc = br.ReadRC();
            int code = cc >> 4, size = cc & 0xF;
            long bits = 8 + size * 8;
            if (hp + bits > hsEnd) break;
            ulong val = 0;
            for (int i = 0; i < size; i++) val = (val << 8) | br.ReadRC();
            if (code == 0 && size == 0) break;
            if (refs.Count == 0)
            {
                if (code != 12) return false;
                headRelative = true;
            }
            refs.Add((code, val, bits, code == 3 && val == 0));
            hp += bits;
        }
        if (!headRelative || refs.Count < 2) return false;

        ulong head = refs[0].Code == 12 ? rec.Handle - refs[0].Val : refs[0].Val;
        handles.WriteHandleRef(4, head);
        for (int i = 1; i < refs.Count; i++)
        {
            if (refs[i].NullXdic) continue;
            handles.WriteHandleRef(refs[i].Code, refs[i].Val);
        }
        handles.WriteHandleRef(3, 0);
        if (DebugTrace)
            System.Console.Error.WriteLine(
                $"DL565HS h={rec.Handle:X} src={rec.SourceVersion} 源 {refs.Count} 枚 → " +
                $"头枚改 (4,own) + 补 (3,0)，hs={handles.BitPosition}位");
        return true;
    }
}
