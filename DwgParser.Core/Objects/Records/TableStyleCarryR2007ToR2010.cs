using DwgParser.Core.IO;
using static DwgParser.Core.Objects.DwgObjectStreamWriter;

namespace DwgParser.Core.Objects.Records;

internal sealed class TableStyleCarryR2007ToR2010 : TableStyleCarryPlan
{
    public override string Name => "AC1015-AC1021→R2010+";

    public override bool IsCaptured(DwgObjectRecord rec)
        => rec.TableStyle is { Captured: true };

    internal override bool TryCarry(BitWriter body, byte[] src, long from, long to,
        DwgObjectRecord rec, Target t)
    {
        if (rec.TableStyle is not { Captured: true } ts) return false;
        long hs = rec.HandleStreamStartBit;
        if (hs <= 0 || to != hs) return false;
        long ownStart = rec.CarryStartBit + 32;
        long headEnd = ts.HeadEndBit;
        if (headEnd <= ownStart || headEnd > hs) return false;
        if (from == ownStart)
            CopyBits(body, src, ownStart, (int)(headEnd - ownStart));
        else if (from != headEnd)
            return false;

        body.WriteRC(0);
        body.WriteBL(ts.Flags);
        body.WriteBL(101);

        var data = ts.Rows[0];
        bool baseMargins = ts.HorizMargin != 0.0 || ts.VertMargin != 0.0;
        WriteTableCellStyle(body, data, th: 0.18, align: 1, vdt: 4, vut: 0, merge: 0,
            m0: 0.06, m1: 0.06, margins: baseMargins);
        body.WriteBL(4);
        body.WriteBL(2);

        body.WriteBL(3);
        WriteTableStyleOverride(body, ts.Rows[1], bl3: 1, id: 1, ctype: 1,
            merge: 0x8000, m0: ts.VertMargin, m1: ts.HorizMargin);
        WriteTableStyleOverride(body, ts.Rows[2], bl3: 2, id: 2, ctype: 1,
            merge: 0, m0: ts.VertMargin, m1: ts.HorizMargin);
        WriteTableStyleOverride(body, data, bl3: 3, id: 3, ctype: 2,
            merge: 0, m0: ts.VertMargin, m1: ts.HorizMargin);

        long contentStart = body.BitPosition;
        WriteCentralTu(body, ts.Name);
        WriteCentralTu(body, "");
        WriteCentralTu(body, "Table");
        WriteCentralTu(body, "");
        WriteCentralTu(body, "_TITLE");
        WriteCentralTu(body, "");
        WriteCentralTu(body, "_HEADER");
        WriteCentralTu(body, "");
        WriteCentralTu(body, "_DATA");
        int contentBits = (int)(body.BitPosition - contentStart);
        WriteStringStreamSize(body, (uint)contentBits);
        body.WriteB(true);
        rec.StringFields.Add(new DwgObjectRecord.StringField(ownStart, 0, ts.Name));
        if (DebugTrace)
            System.Console.Error.WriteLine(
                $"TSUP h={rec.Handle:X} {rec.SourceVersion}→{t.Version} 串流{contentBits}位 " +
                $"body={body.BitPosition}位 from={from} to={to} own={ownStart} headEnd={headEnd} hs={hs}");
        return true;
    }
}
