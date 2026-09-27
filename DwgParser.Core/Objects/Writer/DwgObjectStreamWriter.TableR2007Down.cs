using DwgParser.Core.IO;

namespace DwgParser.Core.Objects;

public static partial class DwgObjectStreamWriter
{
    private static bool CarriesTableR2007Down(DwgObjectRecord rec, Target t)
        => rec.LogicalType == TypeTable
           && rec.SourceVersion == DwgVersion.AC1021
           && (t.Version is DwgVersion.AC1015 or DwgVersion.AC1018)
           && rec.Table is { Captured: true }
           && rec.TableCellsStartBit > 0
           && rec.TableCellsEndBit > rec.TableCellsStartBit;

    private static bool CarryTableR2007Down(BitWriter w, DwgObjectRecord rec, byte[] src,
        long start, int len)
    {
        var cap = rec.Table!;
        long cellsStart = rec.TableCellsStartBit, cellsEnd = rec.TableCellsEndBit;
        long end = start + len;
        long ss = rec.StringStreamStartBit;
        if (ss > start && ss < end) end = ss;
        if (cellsStart < start || cellsEnd <= cellsStart || cellsEnd > end) return false;
        if (cap.Cells.Count == 0) return false;
        foreach (var c in cap.Cells)
            if (c.Type is not (0 or 1)) return false;
        long probe = cellsStart;
        foreach (var c in cap.Cells)
        {
            if (c.CellStartBit != probe) return false;
            if (c.FlatEndBit < c.CellStartBit || c.ValueUnionStartBit < c.FlatEndBit
                || c.CellEndBit <= c.ValueUnionStartBit || c.CellEndBit > cellsEnd)
                return false;
            probe = c.CellEndBit;
        }
        if (probe > cellsEnd) return false;

        CopyBits(w, src, start, (int)(cellsStart - start));
        long pos = cellsStart;
        foreach (var c in cap.Cells)
        {
            CopyBits(w, src, pos, (int)(c.FlatEndBit - pos));
            if (c.Type == 1) WriteThinCellText(w, c.Text);
            CopyBits(w, src, c.FlatEndBit,
                (int)(c.ValueUnionStartBit - c.FlatEndBit));
            pos = c.CellEndBit;
        }
        CopyBits(w, src, pos, (int)(cellsEnd - pos));
        CopyBits(w, src, cellsEnd, (int)(end - cellsEnd));
        return true;
    }

    private static void WriteThinCellText(BitWriter w, string? text)
    {
        if (string.IsNullOrEmpty(text)) { w.WriteBS(0); return; }
        byte[] bytes = BitWriter.EncodeText(text);
        w.WriteBS(unchecked((short)(bytes.Length + 1)));
        foreach (var b in bytes) w.WriteRC(b);
        w.WriteRC(0);
    }
}
