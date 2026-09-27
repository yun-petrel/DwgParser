using DwgParser.Core.IO;
using static DwgParser.Core.Objects.DwgObjectStreamWriter;

namespace DwgParser.Core.Objects.Records;

internal sealed class TableStyleCarryAc1018ToAc1021 : TableStyleCarryPlan
{
    public override string Name => "AC1015/18→AC1021";

    public override bool IsCaptured(DwgObjectRecord rec) => rec.TableStyle?.Captured == true;

    internal override bool TryCarry(BitWriter body, byte[] src, long from, long to,
        DwgObjectRecord rec, Target t)
    {
        if (rec.TableStyle is not { Captured: true } ts) return false;
        long ownStart = rec.CarryStartBit + 32;
        long headEnd = ts.HeadEndBit;
        long hs = rec.HandleStreamStartBit;
        if (hs <= 0 || to > hs) return false;
        if (from != rec.CarryStartBit) return false;
        if (headEnd <= ownStart || headEnd > hs) return false;

        int typeBits = (int)(rec.CarryStartBit - (long)rec.BodyStart * 8);

        body.WriteRL(0);

        long eedStart = rec.EedStartBit, eedEnd = rec.EedStartBit + rec.EedBitLen;
        bool eedOk = t.UnicodeStrings && rec.EedBitLen > 2
            && eedStart >= ownStart && eedEnd <= headEnd;
        if (eedOk && TryCarryEedR2007Plus(new BitWriter(16), src, eedStart, eedEnd, rec))
        {
            CopyBits(body, src, ownStart, (int)(eedStart - ownStart));
            TryCarryEedR2007Plus(body, src, eedStart, eedEnd, rec);
            CopyBits(body, src, eedEnd, (int)(headEnd - eedEnd));
        }
        else
        {
            CopyBits(body, src, ownStart, (int)(headEnd - ownStart));
        }

        if (rec.SourceVersion < DwgVersion.AC1018)
            body.WriteB(SourceXdicRefIsNull(src, rec.HandleStreamStartBit, (long)rec.BodyEnd * 8));

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
            body.WriteBL(512);
            body.WriteBL(0);
        }

        long contentStart = body.BitPosition;
        WriteCentralTu(body, ts.Name);
        WriteCentralTu(body, "");
        WriteCentralTu(body, "");
        WriteCentralTu(body, "");
        int contentBits = (int)(body.BitPosition - contentStart);
        WriteStringStreamSize(body, (uint)contentBits);
        body.WriteB(true);

        rec.StringFields.Add(new DwgObjectRecord.StringField(ownStart, 0, ts.Name));

        int dataBits = (int)body.BitPosition - 32;
        body.PatchRL(0, (uint)(typeBits + 32 + dataBits));

        if (DebugTrace)
            System.Console.Error.WriteLine(
                $"TSUP1821 h={rec.Handle:X} {rec.SourceVersion}→{t.Version} body={body.BitPosition}位 " +
                $"串流{contentBits}位 EED={(eedOk ? "TU" : "verbatim")} RL={(uint)(typeBits + 32 + dataBits)}");
        return true;
    }
}
