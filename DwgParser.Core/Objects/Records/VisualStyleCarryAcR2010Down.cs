using DwgParser.Core.IO;
using static DwgParser.Core.Objects.DwgObjectStreamWriter;

namespace DwgParser.Core.Objects.Records;

internal sealed class VisualStyleCarryAcR2010Down : VisualStyleCarryPlan
{
    public override string Name => "R2010+→AC1021";

    public override bool IsCaptured(DwgObjectRecord rec) => rec.VisualStyle is { DownCaptured: true };

    internal override bool TryCarry(BitWriter body, byte[] src, long from, long to,
        DwgObjectRecord rec, Target t)
    {
        var cap = rec.VisualStyle;
        if (cap == null || !cap.DownCaptured) return false;
        if (!rec.SourceVersion.IsR2010Plus() || t.Version != DwgVersion.AC1021) return false;
        if (from != cap.DownBodyStartBit || to != rec.HandleStreamStartBit) return false;
        var sp = cap.DownFieldSpans;
        var sc = cap.DownScalars;
        if (sp.Length != 18 || sc.Length != 20) return false;
        long ss = cap.DownStreamStartBit;
        if (ss <= from || ss >= to) return false;
        WriteVisualStylePreFields(body, src, cap, withBd2007: true);
        if (rec.SourceVersion >= DwgVersion.AC1027)
        {
            long tuEnd = to - 17;
            long tuStart = LastTuStart(src, ss, tuEnd, out _);
            if (tuStart > ss && tuStart < tuEnd)
            {
                CopyBits(body, src, ss, (int)(tuStart - ss));
                body.WriteRS((ushort)(tuStart - ss));
                body.WriteB(ReadSrcBit(src, to - 1));
            }
            else
            {
                CopyBits(body, src, ss, (int)(to - ss));
            }
        }
        else
        {
            CopyBits(body, src, ss, (int)(to - ss));
        }
        if (DebugTrace)
            System.Console.Error.WriteLine(
                $"VSDOWN h={rec.Handle:X} {rec.SourceVersion}→{t.Version} " +
                $"PRE重排 串流{to - ss}位");
        return true;
    }
}
