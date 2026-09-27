using DwgParser.Core.IO;

namespace DwgParser.Core.Objects;

public static partial class DwgObjectStreamWriter
{

    internal readonly record struct ViewStylePreR2004Layout(
        List<long> CmcBits, List<(long Start, long End, string Val)> Tvs);

    internal static ViewStylePreR2004Layout? LocateViewStylePreR2004(
        byte[] src, long from, long dataEnd, bool section)
    {
        var br = new BitReader(src);
        DwgObjectRecord.SeekTo(br, from);
        var cmcs = new List<long>(7);
        var tvs = new List<(long, long, string)>(4);
        void Cmc() { cmcs.Add(br.BitPosition); _ = br.ReadBS(); }
        void BS() => _ = br.ReadBS();
        void BL() => _ = br.ReadBL();
        void BD() => _ = br.ReadBD();
        void B() => _ = br.ReadB();
        void RC() => _ = br.ReadRC();
        void TV()
        {
            long s = br.BitPosition;
            string v = br.ReadTV();
            tvs.Add((s, br.BitPosition, v));
        }

        BS(); TV();
        B(); BS(); BL();
        Cmc(); BD();
        if (section)
        {
            Cmc(); BD();
            TV();
            BD(); BL(); Cmc(); BL(); Cmc(); BD(); BD(); Cmc(); BD(); BL(); BD(); BL();
            TV();
            Cmc(); Cmc();
            TV();
            BD(); BL(); B(); B();
            BL(); BD(); BL(); BD();
            int nh = br.ReadBS();
            if (nh is < 0 or > 64) return null;
            for (int i = 0; i < nh; i++) BD();
        }
        else
        {
            TV();
            BD(); RC(); Cmc(); BD(); BL();
            Cmc(); Cmc();
            BD(); BL(); BD(); BL();
            TV();
            BL(); Cmc(); BL(); Cmc();
            RC();
        }
        return br.BitPosition == dataEnd ? new ViewStylePreR2004Layout(cmcs, tvs) : null;
    }

    internal static bool TryCarryViewStylePreR2004Centralized(
        BitWriter body, byte[] src, DwgObjectRecord rec, Target t, out BitWriter? handles)
    {
        handles = null;
        if (rec.LogicalType is not (TypeSectionViewStyle or TypeDetailViewStyle)) return false;
        if (rec.SourceVersion != DwgVersion.AC1015) return false;
        if (!(t.R2010Plus || t.Version == DwgVersion.AC1021)) return false;
        if (!TrySourceDataEnd(src, rec, out long dataEnd)) return false;
        long hs = rec.HandleStreamStartBit;
        if (hs <= 0 || hs != dataEnd) return false;
        var hbr = new BitReader(src);
        DwgObjectRecord.SeekTo(hbr, rec.CarryStartBit);
        _ = hbr.ReadRL();
        hbr.ReadHandle();
        if (!DwgObjectReader.TrySkipEed(hbr)) return false;
        int nr = hbr.ReadBL();
        if (nr is < 0 or > 8) return false;
        long from = hbr.BitPosition;
        bool section = rec.LogicalType == TypeSectionViewStyle;
        if (LocateViewStylePreR2004(src, from, dataEnd, section) is not { } lay) return false;
        int expectTv = section ? 4 : 3;
        if (lay.Tvs.Count != expectTv || lay.CmcBits.Count == 0) return false;
        long bodyStart = rec.CarryStartBit + 32;
        if (bodyStart >= from) return false;

        var idxs = new short[lay.CmcBits.Count];
        var widths = new int[lay.CmcBits.Count];
        long prev = from;
        for (int i = 0; i < idxs.Length; i++)
        {
            long p = lay.CmcBits[i];
            if (p < prev || p >= dataEnd) return false;
            short v = ReadBsAt(src, p, out int w);
            if (v is < 0 or > 257) return false;
            idxs[i] = v;
            widths[i] = w;
            prev = p + w;
        }
        var edits = new List<(long Start, long End, int Kind, int Ord)>(idxs.Length + lay.Tvs.Count);
        for (int i = 0; i < idxs.Length; i++)
            edits.Add((lay.CmcBits[i], lay.CmcBits[i] + widths[i], 0, i));
        foreach (var tv in lay.Tvs) edits.Add((tv.Start, tv.End, 1, 0));
        edits.Sort((a, b) => a.Start.CompareTo(b.Start));

        CopyBits(body, src, bodyStart, (int)(from - bodyStart));
        body.WriteB(false);
        long cur = from;
        foreach (var e in edits)
        {
            if (e.Start < cur || e.End > dataEnd) return false;
            CopyBits(body, src, cur, (int)(e.Start - cur));
            if (e.Kind == 0)
            {
                body.WriteBS(0);
                body.WriteBL(unchecked((int)Records.VisualStyleCarryAcR2000ToAc1021
                    .UpgradedRgb(idxs[e.Ord])));
                body.WriteRC(0);
            }
            cur = e.End;
        }
        CopyBits(body, src, cur, (int)(dataEnd - cur));

        long cs = body.BitPosition;
        foreach (var tv in lay.Tvs) WriteCentralTu(body, tv.Val ?? string.Empty);
        WriteStringStreamSize(body, (uint)(body.BitPosition - cs));
        body.WriteB(true);
        rec.WroteCentralStringStream = true;

        var hw = new BitWriter(16);
        long dataEndBit = (long)rec.BodyEnd * 8;
        long meaningful = LastMeaningfulHandleEnd(src, hs, dataEndBit);
        if (meaningful <= hs || meaningful > dataEndBit) meaningful = dataEndBit;
        CopyBits(hw, src, hs, (int)(meaningful - hs));
        handles = hw;

        if (DebugTrace)
            System.Console.Error.WriteLine(
                $"VSPRE04 h={rec.Handle:X} t={rec.TypeCode} {(section ? 510 : 511)} " +
                $"CMC={idxs.Length} TV={lay.Tvs.Count} data={dataEnd - from}b " +
                $"{rec.SourceVersion}→{t.Version}");
        return true;
    }
}
