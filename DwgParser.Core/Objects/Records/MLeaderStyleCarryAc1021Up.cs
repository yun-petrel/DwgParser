using DwgParser.Core.IO;
using static DwgParser.Core.Objects.DwgObjectStreamWriter;

namespace DwgParser.Core.Objects.Records;

internal sealed class MLeaderStyleCarryAc1021Up : MLeaderStyleCarryPlan
{
    public override string Name => "AC1021→R2010+";

    public override bool IsCaptured(DwgObjectRecord rec) => rec.MLeader?.Captured == true;

    internal override bool TryCarry(BitWriter body, byte[] src, long from, long to,
        DwgObjectRecord rec, Target t)
    {
        if (rec.MLeader is not { Captured: true } ml) return false;
        if (from != ml.BodyStartBit || to != rec.HandleStreamStartBit) return false;
        if (ml.StreamStartBit <= from || ml.StreamStartBit >= to) return false;
        CopyBits(body, src, from, (int)(ml.StreamStartBit - from));
        body.WriteBS(0);
        body.WriteBS(9);
        body.WriteBS(9);
        if (t.R2013Plus)
            body.WriteB(false);
        CopyBits(body, src, ml.StreamStartBit, (int)(to - ml.StreamStartBit));
        if (DebugTrace)
            System.Console.Error.WriteLine(
                $"MLS21UP h={rec.Handle:X} {rec.SourceVersion}→{t.Version} " +
                $"数值{ml.StreamStartBit - from}位+attach22{(t.R2013Plus ? "+text_ext1" : "")} " +
                $"串流{to - ml.StreamStartBit}位");
        return true;
    }
}
