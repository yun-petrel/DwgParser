using DwgParser.Core.IO;
using static DwgParser.Core.Objects.DwgObjectStreamWriter;

namespace DwgParser.Core.Objects.Records;

internal sealed class VisualStyleCarryAc1018ToR2010Plus : VisualStyleCarryPlan
{
    public override string Name => "AC1018→R2010+";

    public override bool IsCaptured(DwgObjectRecord rec) => rec.VisualStyle?.Captured == true;

    private static readonly int[] R2010Seq =
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

    internal override bool TryCarry(BitWriter body, byte[] src, long from, long to,
        DwgObjectRecord rec, Target t)
    {
        var cap = rec.VisualStyle;
        if (cap == null || !cap.Captured) return false;
        if (rec.SourceVersion != DwgVersion.AC1018 || !t.R2010Plus) return false;
        long ownStart = rec.CarryStartBit + 32;
        long hs = rec.HandleStreamStartBit;
        if (hs <= 0 || from != ownStart || to != hs) return false;
        long headEnd = cap.BodyStartBit;
        long styleStart = cap.StreamStartBit;
        long eedEnd = rec.EedStartBit + rec.EedBitLen;
        if (headEnd <= ownStart || headEnd > hs) return false;
        if (styleStart <= headEnd || styleStart >= hs) return false;
        var sp = cap.FieldSpans;
        if (sp.Length != 60) return false;
        bool hasEed = rec.EedBitLen > 0;
        if (hasEed && (rec.EedStartBit <= ownStart || eedEnd > headEnd)) return false;

        string? desc = VisualStyleCarryAc1018ToAc1021.ReadDescFromCapture(src, headEnd, styleStart);
        if (desc == null) return false;

        if (t.Version >= DwgVersion.AC1027)
        {
            var probe = new BitWriter(64);
            WriteCentralTu(probe, desc);
            WriteStrokesTu(probe);
            if (probe.BitPosition > 0x7FFF) return false;
        }

        if (hasEed)
        {
            CopyBits(body, src, ownStart, (int)(rec.EedStartBit - ownStart));
            body.WriteBS(0);
            CopyBits(body, src, eedEnd, (int)(headEnd - eedEnd));
        }
        else
        {
            CopyBits(body, src, ownStart, (int)(headEnd - ownStart));
        }
        if (t.R2013Plus) body.WriteB(false);

        void CopySpan(int i) => CopyBits(body, src, sp[i * 2], (int)(sp[i * 2 + 1] - sp[i * 2]));
        CopySpan(0);
        body.WriteBS(2);
        body.WriteB(cap.InternalOnly != 0);
        foreach (int i in R2010Seq)
        {
            if (i == 22) body.WriteBS((short)cap.HaloGap);
            else if (i == 28) body.WriteBD(cap.Brightness);
            else CopySpan(i);
            body.WriteBS(1);
        }

        long contentStart;
        if (t.Version >= DwgVersion.AC1027)
        {
            WriteVisualStyleR2013bBlock(body, desc);
            contentStart = body.BitPosition;
            WriteCentralTu(body, desc);
            WriteStrokesTu(body);
        }
        else
        {
            contentStart = body.BitPosition;
            WriteCentralTu(body, desc);
        }
        int contentBits = (int)(body.BitPosition - contentStart);
        WriteStringStreamSize(body, (uint)contentBits);
        body.WriteB(true);

        rec.StringFields.Add(new DwgObjectRecord.StringField(ownStart, 0, desc));

        if (DebugTrace)
            System.Console.Error.WriteLine(
                $"VSUP18R10 h={rec.Handle:X} {rec.SourceVersion}→{t.Version} body={body.BitPosition}位 " +
                $"EED丢弃 desc='{desc}' 串流{contentBits}位");
        return true;
    }
}
