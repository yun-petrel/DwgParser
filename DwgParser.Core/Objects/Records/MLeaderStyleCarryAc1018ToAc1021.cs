using DwgParser.Core.IO;
using static DwgParser.Core.Objects.DwgObjectStreamWriter;

namespace DwgParser.Core.Objects.Records;

internal sealed class MLeaderStyleCarryAc1018ToAc1021 : MLeaderStyleCarryPlan
{
    public override string Name => "AC1018→AC1021";

    public override bool IsCaptured(DwgObjectRecord rec) => rec.MLeader?.UpCaptured == true;

    internal override bool TryCarry(BitWriter body, byte[] src, long from, long to,
        DwgObjectRecord rec, Target t)
    {
        if (rec.MLeader is not { UpCaptured: true } mls) return false;
        long ownStart = rec.CarryStartBit + 32;
        long headEnd = mls.UpHeadEndBit;
        long hs = rec.HandleStreamStartBit;
        if (hs <= 0 || to > hs) return false;
        if (from != rec.CarryStartBit) return false;
        if (headEnd <= ownStart || headEnd > hs) return false;
        if (mls.UpDescStartBit <= headEnd || mls.UpDescEndBit < mls.UpDescStartBit
            || mls.UpTextDefStartBit < mls.UpDescEndBit
            || mls.UpTextDefEndBit < mls.UpTextDefStartBit || mls.UpTextDefEndBit > hs)
            return false;

        int typeBits = (int)(rec.CarryStartBit - (long)rec.BodyStart * 8);

        body.WriteRL(0);

        CopyBits(body, src, ownStart, (int)(mls.UpDescStartBit - ownStart));
        CopyBits(body, src, mls.UpDescEndBit, (int)(mls.UpTextDefStartBit - mls.UpDescEndBit));
        CopyBits(body, src, mls.UpTextDefEndBit, (int)(hs - mls.UpTextDefEndBit));

        long contentStart = body.BitPosition;
        WriteCentralTu(body, mls.Desc);
        WriteCentralTu(body, mls.TextDefault);
        int contentBits = (int)(body.BitPosition - contentStart);
        WriteStringStreamSize(body, (uint)contentBits);
        body.WriteB(true);

        rec.StringFields.Add(new DwgObjectRecord.StringField(ownStart, 0, mls.Desc));

        int dataBits = (int)body.BitPosition - 32;
        body.PatchRL(0, (uint)(typeBits + 32 + dataBits));

        if (DebugTrace)
            System.Console.Error.WriteLine(
                $"MLSUP1821 h={rec.Handle:X} {rec.SourceVersion}→{t.Version} body={body.BitPosition}位 " +
                $"串流{contentBits}位 RL={(uint)(typeBits + 32 + dataBits)} desc='{mls.Desc}' textdef='{mls.TextDefault}'");
        return true;
    }
}
