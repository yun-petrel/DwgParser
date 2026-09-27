using DwgParser.Core.IO;
using static DwgParser.Core.Objects.DwgObjectStreamWriter;

namespace DwgParser.Core.Objects.Records;

internal sealed class DimStyleCarryR2010ToAc1021 : DimStyleCarryPlan
{
    public override string Name => "R2010+→AC1021";

    public override bool IsCaptured(DwgObjectRecord rec) => rec.DimStyle?.DownCaptured == true;

    internal override bool TryCarry(BitWriter body, byte[] src, long from, long to,
        DwgObjectRecord rec, Target t)
    {
        var cap = rec.DimStyle;
        if (cap == null || !cap.DownCaptured || cap.DownBlockBits <= 0) return false;
        if (cap.Name == null) return false;
        long ownStart = rec.CarryStartBit;
        long fxlonEnd = cap.FxlonEndBit;
        long flag0End = cap.Flag0EndBit;
        long blockEnd = fxlonEnd + cap.DownBlockBits;
        if (to != rec.HandleStreamStartBit) return false;
        if (from < ownStart || from > fxlonEnd) return false;
        if (fxlonEnd >= blockEnd || blockEnd >= flag0End || flag0End > to) return false;

        CopyBits(body, src, from, (int)(fxlonEnd - from));
        CopyBits(body, src, blockEnd, (int)(flag0End - blockEnd));

        long contentStart = body.BitPosition;
        WriteCentralTu(body, cap.Name);
        WriteCentralTu(body, cap.Dimpost ?? "");
        WriteCentralTu(body, cap.Dimapost ?? "");
        int contentBits = (int)(body.BitPosition - contentStart);
        WriteStringStreamSize(body, (uint)contentBits);
        body.WriteB(true);

        if (System.Environment.GetEnvironmentVariable("DWGDBG") == "1")
            System.Console.Error.WriteLine(
                $"DS21DN h={rec.Handle:X} {rec.SourceVersion}→{t.Version} 剥块{cap.DownBlockBits}位 " +
                $"数据区{body.BitPosition}位 串流{contentBits}位 " +
                $"from={from} to={to} own={ownStart} fxlon={fxlonEnd} flag0={flag0End} hs={rec.HandleStreamStartBit}");
        return true;
    }
}
