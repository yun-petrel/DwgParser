using DwgParser.Core.IO;
using static DwgParser.Core.Objects.DwgObjectStreamWriter;

namespace DwgParser.Core.Objects.Records;

internal sealed class VisualStyleCarryAc1021Up : VisualStyleCarryPlan
{
    public override string Name => "AC1021→R2010+";

    public override bool IsCaptured(DwgObjectRecord rec) => rec.VisualStyle?.Captured == true;

    internal override bool TryCarry(BitWriter body, byte[] src, long from, long to,
        DwgObjectRecord rec, Target t)
    {
        var cap = rec.VisualStyle;
        if (cap == null || !cap.Captured) return false;
        if (!t.R2010Plus || rec.SourceVersion != DwgVersion.AC1021) return false;
        if (from != cap.BodyStartBit || to != rec.HandleStreamStartBit) return false;
        var sp = cap.FieldSpans;
        if (sp.Length != 60) return false;
        long ss = cap.StreamStartBit;
        if (ss <= from || ss >= to) return false;
        void CopySpan(int i) => CopyBits(body, src, sp[i * 2], (int)(sp[i * 2 + 1] - sp[i * 2]));
        CopySpan(0);
        body.WriteBS(2);
        body.WriteB(cap.InternalOnly != 0);
        int[] seq =
        {
            1, 2, 3,
            7,
            4, 5,
            6,
            8, 9,
            10, 11,
            12,
            26,
            13,
            14,
            15, 16,
            17, 18, 19,
            20,
            21,
            22,
            23, 24,
            27,
            28,
            29,
        };
        foreach (int i in seq)
        {
            if (i == 22) body.WriteBS((short)cap.HaloGap);
            else if (i == 28) body.WriteBD(cap.Brightness);
            else CopySpan(i);
            body.WriteBS(1);
        }
        if (t.Version >= DwgVersion.AC1027)
        {
            long hs = to;
            long streamBits = hs - 17 - ss;
            if (streamBits < 0 || streamBits + VisualStyleStrokesTuBits > 0x7FFF)
                return false;
            WriteVisualStyleR2013bBlock(body, ReadStreamFirstTu(src, ss));
            CopyBits(body, src, ss, (int)(hs - 17 - ss));
            WriteStrokesTu(body);
            body.WriteRS((ushort)(streamBits + VisualStyleStrokesTuBits));
            body.WriteB(ReadSrcBit(src, hs - 1));
        }
        else
        {
            CopyBits(body, src, ss, (int)(to - ss));
        }
        if (DebugTrace)
            System.Console.Error.WriteLine(
                $"VS21UP h={rec.Handle:X} {rec.SourceVersion}→{t.Version} " +
                $"头{sp[1] - from}位+28对 串流{to - ss}位");
        return true;
    }
}
