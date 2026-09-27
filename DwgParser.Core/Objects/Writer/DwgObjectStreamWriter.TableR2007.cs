using System.Text;
using DwgParser.Core.IO;

namespace DwgParser.Core.Objects;

public static partial class DwgObjectStreamWriter
{
    private const int TableR2007ValueFormatFlags = 4;
    private const int TableR2007ValueDataTypeString = 4;
    private const int TableR2007ValueDataTypeUnknown = 0;

    private static bool CarriesTableR2007Up(DwgObjectRecord rec, Target t)
        => rec.LogicalType == TypeTable
           && !rec.SourceVersion.IsR2007Plus()
           && t.R2007Plus && !t.R2010Plus
           && rec.Table is { Captured: true }
           && rec.TableCellsStartBit > 0
           && rec.TableCellsEndBit > rec.TableCellsStartBit;

    private static bool CarryTableR2007Up(BitWriter w, DwgObjectRecord rec, byte[] src,
        long start, int len)
    {
        var cap = rec.Table!;
        long cellsStart = rec.TableCellsStartBit, cellsEnd = rec.TableCellsEndBit;
        long end = start + len;
        if (cellsStart < start || cellsEnd < cellsStart || cellsEnd > end) return false;
        foreach (var c in cap.Cells)
            if (c.Type is not (0 or 1)) return false;
        if (cap.Cells.Count == 0) return false;

        CopyBits(w, src, start, (int)(cellsStart - start));

        var central = new List<string>(cap.Cells.Count * 2);
        long pos = cellsStart;
        foreach (var c in cap.Cells)
        {
            long ts = c.TextStartBit, te = c.TextEndBit;
            if (ts >= pos && te > ts && te <= c.CellEndBit)
            {
                CopyBits(w, src, pos, (int)(ts - pos));
                pos = te;
            }
            long ce = c.CellEndBit > pos ? c.CellEndBit : pos;
            if (ce > cellsEnd) ce = cellsEnd;
            CopyBits(w, src, pos, (int)(ce - pos));
            pos = ce;
            WriteTableR2007ValueUnion(w, central, c);
        }
        if (pos < cellsEnd) CopyBits(w, src, pos, (int)(cellsEnd - pos));

        CopyBits(w, src, cellsEnd, (int)(end - cellsEnd));

        long contentStart = w.BitPosition;
        foreach (var s in central)
            WriteCentralTu(w, s);
        WriteStringStreamSize(w, (uint)(w.BitPosition - contentStart));
        w.WriteB(true);
        rec.WroteCentralStringStream = true;
        return true;
    }

    private static void WriteTableR2007ValueUnion(BitWriter w, List<string> central,
        TableEntityCell c)
    {
        w.WriteBL(0);
        w.WriteBL(TableR2007ValueFormatFlags);
        bool text = c.Type == 1;
        w.WriteBL(text ? TableR2007ValueDataTypeString
                       : TableR2007ValueDataTypeUnknown);
        if (text)
        {
            var bytes = Encoding.Unicode.GetBytes(c.Text ?? string.Empty);
            w.WriteBS(unchecked((short)(bytes.Length + 2)));
            foreach (var b in bytes) w.WriteRC(b);
            w.WriteRC(0); w.WriteRC(0);
        }
        else
        {
            w.WriteBL(0);
        }
        w.WriteBL(0);
        central.Add(string.Empty);
        central.Add(c.Text ?? string.Empty);
    }
}
