using DwgParser.Core.IO;
using static DwgParser.Core.Objects.DwgObjectStreamWriter;

namespace DwgParser.Core.Objects.Records;

internal sealed class DimStyleCarryAc1021Up : DimStyleCarryPlan
{
    public override string Name => "AC1021→R2010+";

    public override bool IsCaptured(DwgObjectRecord rec) => rec.DimStyle?.Flag0EndBit > 0;

    internal override bool TryCarry(BitWriter body, byte[] src, long from, long to,
        DwgObjectRecord rec, Target t)
    {
        var cap = rec.DimStyle;
        if (cap == null) return false;
        if (!t.R2010Plus || rec.SourceVersion != DwgVersion.AC1021) return false;
        if (cap.FxlonEndBit <= 0 || cap.Flag0EndBit <= 0 || cap.Name == null) return false;
        long ownStart = rec.CarryStartBit + 32;
        if (from < ownStart || from > cap.FxlonEndBit || to < cap.Flag0EndBit) return false;
        if (rec.HandleStreamStartBit < cap.Flag0EndBit) return false;

        CopyBits(body, src, from, (int)(cap.FxlonEndBit - from));
        body.WriteB(false);
        body.WriteBD(100.0);
        body.WriteBD(100.0);
        CopyBits(body, src, cap.FxlonEndBit, (int)(cap.Flag0EndBit - cap.FxlonEndBit));

        long contentStart = body.BitPosition;
        WriteCentralTu(body, cap.Name);
        WriteCentralTu(body, cap.Dimpost);
        WriteCentralTu(body, cap.Dimapost);
        WriteCentralTu(body, "");
        WriteCentralTu(body, "");
        int contentBits = (int)(body.BitPosition - contentStart);
        WriteStringStreamSize(body, (uint)contentBits);
        body.WriteB(true);
        if (DebugTrace)
            System.Console.Error.WriteLine(
                $"DS21UP h={rec.Handle:X} {rec.SourceVersion}→{t.Version} R2010b133 串流{contentBits}位");
        return true;
    }
}
