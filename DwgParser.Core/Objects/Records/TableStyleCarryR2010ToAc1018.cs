using DwgParser.Core.IO;
using static DwgParser.Core.Objects.DwgObjectStreamWriter;

namespace DwgParser.Core.Objects.Records;

internal sealed class TableStyleCarryR2010ToAc1018 : TableStyleCarryPlan
{
    public override string Name => "R2010+→AC1015/18";

    public override bool IsCaptured(DwgObjectRecord rec)
        => rec.TableStyle is { Captured: true } or { DownReady: true };

    internal override bool TryCarry(BitWriter body, byte[] src, long from, long to,
        DwgObjectRecord rec, Target t)
    {
        if (rec.TableStyle is not { Captured: true } and not { DownReady: true }) return false;
        if (rec.TableStyle is not { ValuesCommitted: true } ts0 && rec.SourceVersion.IsR2010Plus())
            return false;
        var ts = rec.TableStyle;
        if (string.IsNullOrEmpty(ts.Name)) return false;
        long hs = rec.HandleStreamStartBit;
        if (hs <= 0) return false;
        long headEnd = ts.HeadEndBit;
        bool rlInWindow = !rec.SourceVersion.IsR2010Plus();
        long ownStart = rec.CarryStartBit + (rlInWindow ? 32 : 0);
        if (headEnd <= ownStart || headEnd > hs) return false;
        long ssStart = rec.StringStreamStartBit;
        bool full = to == hs;
        bool truncated = ssStart > 0 && to == ssStart;
        if (!full && !truncated) return false;
        bool fromHead = from == rec.CarryStartBit && rlInWindow;
        if (!fromHead && from != ownStart && from != headEnd) return false;
        if (from != headEnd)
        {
            if (fromHead) CopyBits(body, src, from, 32);
            long e0 = rec.EedStartBit, eLen = rec.EedBitLen;
            if (e0 > ownStart && eLen > 0 && e0 + eLen <= headEnd)
            {
                CopyBits(body, src, ownStart, (int)(e0 - ownStart));
                if (!DwgObjectStreamWriter.TryWriteEedPreR2007Range(body, src, e0, (int)eLen))
                    CopyBits(body, src, e0, (int)eLen);
                CopyBits(body, src, e0 + eLen, (int)(headEnd - e0 - eLen));
            }
            else CopyBits(body, src, ownStart, (int)(headEnd - ownStart));
        }

        body.WriteTV(ts.Name);

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
        }

        if (DebugTrace)
            System.Console.Error.WriteLine(
                $"TSDN1018 h={rec.Handle:X} {rec.SourceVersion}→{t.Version} body={body.BitPosition}位 " +
                $"from={from} to={to} own={ownStart} headEnd={headEnd} hs={hs} " +
                $"name=\"{ts.Name}\" rows=[{ts.Rows[0].TextHeight}/{ts.Rows[0].Alignment}," +
                $"{ts.Rows[1].TextHeight}/{ts.Rows[1].Alignment}," +
                $"{ts.Rows[2].TextHeight}/{ts.Rows[2].Alignment}]");
        return true;
    }
}
