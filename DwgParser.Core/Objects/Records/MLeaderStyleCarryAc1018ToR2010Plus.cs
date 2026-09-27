using DwgParser.Core.IO;
using static DwgParser.Core.Objects.DwgObjectStreamWriter;

namespace DwgParser.Core.Objects.Records;

internal sealed class MLeaderStyleCarryAc1018ToR2010Plus : MLeaderStyleCarryPlan
{
    public override string Name => "AC1018→R2010+";

    public override bool IsCaptured(DwgObjectRecord rec) => rec.MLeader?.UpCaptured == true;

    internal override bool TryCarry(BitWriter body, byte[] src, long from, long to,
        DwgObjectRecord rec, Target t)
    {
        if (rec.MLeader is not { UpCaptured: true } mls) return false;
        if (!t.R2010Plus) return false;
        long ownStart = rec.CarryStartBit + 32;
        long headEnd = mls.UpHeadEndBit;
        long hs = rec.HandleStreamStartBit;
        if (hs <= 0 || from != ownStart || to != hs) return false;
        if (headEnd <= ownStart || headEnd > hs) return false;
        if (mls.UpDescStartBit <= headEnd || mls.UpDescEndBit < mls.UpDescStartBit
            || mls.UpTextDefStartBit < mls.UpDescEndBit
            || mls.UpTextDefEndBit < mls.UpTextDefStartBit || mls.UpTextDefEndBit > hs)
            return false;
        if (mls.UpEedStartBit <= ownStart || mls.UpEedEndBit < mls.UpEedStartBit
            || mls.UpEedEndBit > headEnd
            || mls.UpXdicBit < mls.UpEedEndBit || mls.UpXdicBit != headEnd - 1)
            return false;

        CopyBits(body, src, ownStart, (int)(mls.UpEedStartBit - ownStart));
        body.WriteBS(0);
        CopyBits(body, src, mls.UpEedEndBit, (int)(mls.UpXdicBit - mls.UpEedEndBit));
        body.WriteB(true);
        if (t.R2013Plus) body.WriteB(false);
        body.WriteBS(2);

        CopyBits(body, src, headEnd, (int)(mls.UpDescStartBit - headEnd));
        CopyBits(body, src, mls.UpDescEndBit, (int)(mls.UpTextDefStartBit - mls.UpDescEndBit));
        CopyBits(body, src, mls.UpTextDefEndBit, (int)(hs - mls.UpTextDefEndBit));

        body.WriteBS(0);
        body.WriteBS(9);
        body.WriteBS(9);
        if (t.R2013Plus)
            body.WriteB(false);

        long contentStart = body.BitPosition;
        WriteCentralTu(body, mls.Desc);
        WriteCentralTu(body, mls.TextDefault);
        int contentBits = (int)(body.BitPosition - contentStart);
        WriteStringStreamSize(body, (uint)contentBits);
        body.WriteB(true);

        rec.StringFields.Add(new DwgObjectRecord.StringField(ownStart, 0, mls.Desc));

        if (DebugTrace)
            System.Console.Error.WriteLine(
                $"MLSUP18R10 h={rec.Handle:X} {rec.SourceVersion}→{t.Version} body={body.BitPosition}位 " +
                $"cv10+attach22{(t.R2013Plus ? "+textExt1" : "")} 串流{contentBits}位 " +
                $"desc='{mls.Desc}' textdef='{mls.TextDefault}'");
        return true;
    }
}
