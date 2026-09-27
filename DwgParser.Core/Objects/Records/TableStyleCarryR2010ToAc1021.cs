using DwgParser.Core.IO;
using static DwgParser.Core.Objects.DwgObjectStreamWriter;

namespace DwgParser.Core.Objects.Records;

internal sealed class TableStyleCarryR2010ToAc1021 : TableStyleCarryPlan
{
    public override string Name => "R2010+→AC1021";

    public override bool IsCaptured(DwgObjectRecord rec)
        => rec.TableStyle is { Captured: true } or { DownReady: true };

    internal override bool TryCarry(BitWriter body, byte[] src, long from, long to,
        DwgObjectRecord rec, Target t)
    {
        if (rec.TableStyle is not { ValuesCommitted: true } ts
            || rec.TableStyle is not { Captured: true } and not { DownReady: true }) return false;
        long hs = rec.HandleStreamStartBit;
        if (hs <= 0 || to != hs) return false;
        long headEnd = ts.HeadEndBit;
        long ownStart = rec.CarryStartBit;
        if (headEnd <= ownStart || headEnd > hs) return false;

        if (from == ownStart)
            CopyBits(body, src, ownStart, (int)(headEnd - ownStart));
        else if (from != headEnd)
            return false;

        body.WriteBS((short)ts.FlowDirection);
        body.WriteBS((short)ts.Flags);
        body.WriteBD(ts.HorizMargin);
        body.WriteBD(ts.VertMargin);
        body.WriteB(ts.TitleSuppressed != 0);
        body.WriteB(ts.HeaderSuppressed != 0);
        for (int r = 0; r < 3; r++)
        {
            var row = ts.Rows[r];
            body.WriteBD(row.TextHeight);
            body.WriteBS((short)row.Alignment);
            WriteCmc(body, row.TextColor);
            WriteCmc(body, row.FillColor);
            body.WriteB(row.HasBg);
            for (int k = 0; k < 6; k++)
            {
                body.WriteBS((short)row.BorderLw[k]);
                body.WriteB(row.BorderVis[k]);
                WriteCmc(body, row.BorderCol[k]);
            }
            body.WriteBL(row.DataType);
            body.WriteBL(row.UnitType);
        }

        long contentStart = body.BitPosition;
        WriteCentralTu(body, ts.Name);
        WriteCentralTu(body, "");
        WriteCentralTu(body, "");
        WriteCentralTu(body, "");
        int contentBits = (int)(body.BitPosition - contentStart);
        WriteStringStreamSize(body, (uint)contentBits);
        body.WriteB(true);

        if (DebugTrace)
            System.Console.Error.WriteLine(
                $"TSUP1021 h={rec.Handle:X} {rec.SourceVersion}→{t.Version} body={body.BitPosition}位 " +
                $"串流{contentBits}位 from={from} to={to} own={ownStart} headEnd={headEnd} hs={hs}");
        return true;
    }
}
