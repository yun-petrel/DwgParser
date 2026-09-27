using DwgParser.Core.IO;
using static DwgParser.Core.Objects.DwgObjectStreamWriter;

namespace DwgParser.Core.Objects.Records;

internal abstract class DimStyleCarryAcR2000Up : DimStyleCarryAc1018Up
{
    protected sealed override bool SourcePreR2004 => true;

    internal bool TryCarryFrameStart(BitWriter body, byte[] src, long from, long to,
        DwgObjectRecord rec, Target t, out BitWriter? handles)
    {
        handles = null;
        if (rec.LogicalType != TypeDimStyle
            || (t.Version != DwgVersion.AC1021 && !t.R2010Plus)
            || rec.SourceVersion != DwgVersion.AC1015) return false;
        if (rec.DimStyle is not { } cap || cap.PreR2004CmcIndices is not { Length: 3 }
            || cap.DeferredStringFields is not { Count: 3 }) return false;
        var hs = BuildHandleStream(src, rec, cap.HsBit);
        var probe = new BitWriter((int)(to - from) + 512);
        if (!TryCarry(probe, src, from, to, rec, t)) return false;
        Append(body, probe);
        handles = hs;
        return true;
    }

    private static BitWriter BuildHandleStream(byte[] src, DwgObjectRecord rec, long hsStart)
    {
        var w = new BitWriter(16);
        var br = new BitReader(src);
        long hp = hsStart, end = (long)rec.BodyEnd * 8;
        while (hp + 8 <= end)
        {
            DwgObjectRecord.SeekTo(br, hp);
            int cc = br.ReadRC();
            int bits = 8 + (cc & 0xF) * 8;
            if (hp + bits > end) break;
            bool strip = (cc >> 4) == 3;
            if (strip)
                for (int i = 8; i < bits && strip; i++)
                    if (ReadSrcBit(src, hp + i)) strip = false;
            if (!strip) CopyBits(w, src, hp, bits);
            hp += bits;
        }
        w.WriteHandleRef(5, 0);
        w.WriteHandleRef(5, 0);
        w.WriteHandleRef(5, 0);
        if (DebugTrace)
            System.Console.Error.WriteLine(
                $"DIMSLOT15 h={rec.Handle:X} hs={w.BitPosition}位 (+3×[5]0 R_2007a 槽)");
        return w;
    }
}

internal sealed class DimStyleCarryAcR2000ToAc1021 : DimStyleCarryAcR2000Up
{
    public override string Name => "AC1015→AC1021";

    protected override bool TargetIsR2010 => false;
}

internal sealed class DimStyleCarryAcR2000ToR2010 : DimStyleCarryAcR2000Up
{
    public override string Name => "AC1015→R2010+";

    protected override bool TargetIsR2010 => true;
}
