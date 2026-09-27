using DwgParser.Core.IO;

namespace DwgParser.Core.Objects;

public static partial class DwgObjectStreamWriter
{
    private const int TableCsTypeCol = 3, TableCsTypeRow = 2, TableCsTypeCell = 1, TableCsTypeBase = 4;
    private const int TableCsDataFlags = 1;
    private const uint TableCsBgRgb = 0xC8000000;
    private const uint TableCfColorRgb = 0xC1000000;
    private const int TableColCsPovr = 128, TableRowCsPovr = 15488, TableBaseCsPovr = 408576;
    private const int TableCellCsPovrPlain = 408576;
    private const int TableCellCsPovrAligned = 408592;
    private const int TableCfPovrPlain = 128, TableCfPovrAligned = 192;
    private const int TableCfVdtGeneral = 512, TableCfVdtString = 4;
    private const double TableColCsTextHeight = 0.09857894893196777;
    private const double TableCellCsTextHeight = 0.18;
    private const int TableMarginFlagsOn = 1, TableMarginFlagsOff = 0;
    private const int TableUnknownBs38 = 38;
    private const int TableCfAlignRow = 1, TableCfAlignCell = 5;

    private const int TableOracleCfPovr = 3;
    private const int TableOracleCsPovr = 0;
    private const int TableOracleTextHeight = 0;

    internal static bool TableCellHasContent(TableEntityCell? cell)
        => cell is not null
           && (cell.ValueDataType != 0
               || !string.IsNullOrEmpty(cell.Text) || !string.IsNullOrEmpty(cell.FormattedValue));

    private static bool CarriesTableR2010Up(DwgObjectRecord rec, Target t)
        => rec.LogicalType == TypeTable && t.R2010Plus
           && !rec.SourceVersion.IsR2010Plus()
           && rec.Table is { Captured: true, TypeAxisRefs.Count: >= 2 };

    private static void CarryTableR2010Up(BitWriter w, DwgObjectRecord rec, Target t)
    {
        var cap = rec.Table!;
        bool r2013Plus = t.Version >= DwgVersion.AC1027;
        bool oracleForm = rec.SourceVersion is DwgVersion.AC1015 or DwgVersion.AC1018;
        var central = new List<string>();

        w.WriteBD(cap.InsX); w.WriteBD(cap.InsY); w.WriteBD(cap.InsZ);
        w.WriteBB(cap.ScaleFlag);
        switch (cap.ScaleFlag)
        {
            case 0:
                w.WriteRD(cap.ScaleX);
                w.WriteDD(cap.ScaleY, cap.ScaleX);
                w.WriteDD(cap.ScaleZ, cap.ScaleX);
                break;
            case 1:
                w.WriteDD(cap.ScaleY, 1.0);
                w.WriteDD(cap.ScaleZ, 1.0);
                break;
            case 2:
                w.WriteRD(cap.ScaleX);
                break;
        }
        w.WriteBD(cap.Rotation);
        w.WriteBD(cap.NormalX); w.WriteBD(cap.NormalY); w.WriteBD(cap.NormalZ);
        w.WriteB(cap.HasAttribs);
        if (cap.HasAttribs) w.WriteBL(0);

        w.WriteRC(0);
        w.WriteBL(0);
        if (r2013Plus) w.WriteBL(0); else w.WriteB(true);
        Tv(central, string.Empty);
        Tv(central, string.Empty);

        int cols = cap.Columns, rows = cap.Rows;
        w.WriteBL(cols);
        for (int i = 0; i < cols; i++)
        {
            Tv(central, string.Empty);
            w.WriteBL(0); w.WriteBL(0);
            WriteTableCellStyle(w, central, TableCsTypeCol, TableColCsPovr,
                TableColCsPovr, TableCfAlignRow, TableColCsTextHeight,
                TableMarginFlagsOff, null, minimal: oracleForm);
            w.WriteBL(0);
            w.WriteBD(i < cap.ColWidths.Count ? cap.ColWidths[i] : 0.0);
        }

        w.WriteBL(rows);
        for (int r = 0; r < rows; r++)
        {
            w.WriteBL(cols);
            for (int c = 0; c < cols; c++)
            {
                int idx = r * cols + c;
                WriteTableCell(w, central, idx < cap.Cells.Count ? cap.Cells[idx] : null,
                    TableMargins(cap), oracleForm);
            }
            w.WriteBL(0); w.WriteBL(0);
            WriteTableCellStyle(w, central, TableCsTypeRow, TableRowCsPovr,
                TableColCsPovr, TableCfAlignRow, cap.DataRowHeight, TableMarginFlagsOff, null, minimal: oracleForm);
            w.WriteBL(3);
            w.WriteBD(r < cap.RowHeights.Count ? cap.RowHeights[r] : 0.0);
        }

        w.WriteBL(0);
        if (oracleForm)
            WriteTableCellStyle(w, central, TableCsTypeBase, TableOracleCsPovr,
                TableOracleCsPovr, TableCfAlignRow, TableCellCsTextHeight,
                TableMarginFlagsOff, null);
        else
        WriteTableCellStyle(w, central, TableCsTypeBase, TableBaseCsPovr,
            0, TableCfAlignRow, TableCellCsTextHeight, TableMarginFlagsOn,
            TableMargins(cap));
        var merged = TableMergedRanges(cap);
        w.WriteBL(merged.Count);
        for (int i = 0; i < merged.Count; i++)
        {
            w.WriteBL(merged[i].Row);
            w.WriteBL(merged[i].Col);
            w.WriteBL(merged[i].Height);
            w.WriteBL(merged[i].Width);
        }
        w.WriteBS(TableUnknownBs38);
        w.WriteBD(cap.HorizDirX); w.WriteBD(cap.HorizDirY); w.WriteBD(cap.HorizDirZ);
        w.WriteBL(0);
        w.WriteBL(1);
        w.WriteBD(0); w.WriteBD(0); w.WriteBD(0);
        w.WriteBL(0);
        w.WriteBL(1);

        long contentStart = w.BitPosition;
        foreach (var s in central)
            WriteCentralTu(w, s);
        WriteStringStreamSize(w, (uint)(w.BitPosition - contentStart));
        w.WriteB(true);
        rec.WroteCentralStringStream = true;
    }

    internal readonly struct TableMergedRange
    {
        public TableMergedRange(int row, int col, int height, int width)
        {
            Row = row; Col = col; Height = height; Width = width;
        }

        public readonly int Row, Col, Height, Width;
    }

    internal static List<TableMergedRange> TableMergedRanges(Records.TableCapture cap)
    {
        var list = new List<TableMergedRange>();
        int cols = cap.Columns, rows = cap.Rows;
        if (cols <= 0 || rows <= 0) return list;
        for (int idx = 0; idx < cap.Cells.Count && idx < (long)cols * rows; idx++)
        {
            var c = cap.Cells[idx];
            if (c.IsMergedValue) continue;
            int spanCols = c.MergedWidth > 1 ? c.MergedWidth : 1;
            int spanRows = c.MergedHeight > 1 ? c.MergedHeight : 1;
            if (spanCols == 1 && spanRows == 1) continue;
            int r = idx / cols, col = idx % cols;
            if (r + spanRows > rows || col + spanCols > cols) continue;
            list.Add(new TableMergedRange(r, col, spanRows - 1, spanCols - 1));
        }
        return list;
    }

    private static double[] TableMargins(Records.TableCapture cap)
        => new[] { cap.HorzCellMargin, cap.HorzCellMargin, cap.HorzCellMargin, cap.HorzCellMargin,
                   cap.VertCellMargin, cap.VertCellMargin };

    private static void WriteTableCell(BitWriter w, List<string> central,
        TableEntityCell? cell, double[] margins, bool oracleForm)
    {
        w.WriteBL(0);
        Tv(central, string.Empty);
        if (oracleForm)
        {
            w.WriteBL(0); w.WriteBL(1);
            Tv(central, "ACAD_ROUNDTRIP_2008_CELL_CHECKSUM");
            w.WriteBL(0);
            w.WriteBL(2);
            w.WriteBD(0);
            w.WriteBL(0);
            Tv(central, string.Empty);
            Tv(central, string.Empty);
            w.WriteBL(0);
            if (TableCellHasContent(cell))
            {
                w.WriteBL(1);
                w.WriteBL(1);
                WriteTableCellValue(w, central, cell!, emptyFormattedValue: true);
                w.WriteBL(0);
                w.WriteBS(1);
                WriteTableContentFormat(w, central, TableOracleCfPovr, TableCfVdtGeneral,
                    TableCfAlignRow, TableCellCsTextHeight);
                WriteTableCellStyle(w, central, TableCsTypeCell, TableOracleCsPovr,
                    TableOracleCsPovr, TableCfAlignRow, TableCellCsTextHeight,
                    TableMarginFlagsOff, null);
                w.WriteBL(0);
            }
            else
            {
                w.WriteBL(0);
                WriteTableCellStyle(w, central, TableCsTypeCell, TableOracleCsPovr,
                    TableOracleCsPovr, TableCfAlignRow, TableCellCsTextHeight,
                    TableMarginFlagsOff, null);
                w.WriteBL(0);
            }
            w.WriteBL(0);
            return;
        }
        w.WriteBL(0); w.WriteBL(0);
        w.WriteBL(0);
        w.WriteBL(cell is null ? 0 : 1);

        if (cell is not null)
        {
            bool aligned = cell.Alignment is > 0;
            w.WriteBL(1);
            WriteTableCellValue(w, central, cell);
            w.WriteBL(0);
            w.WriteBS(1);
            WriteTableContentFormat(w, central,
                aligned ? TableCfPovrAligned : TableCfPovrPlain,
                TableCfVdtString, TableCfAlignRow, cell.TextHeight ?? 0.0);
            WriteTableCellStyle(w, central, TableCsTypeCell,
                aligned ? TableCellCsPovrAligned : TableCellCsPovrPlain,
                0, TableCfAlignCell, TableCellCsTextHeight, TableMarginFlagsOn,
                margins);
            w.WriteBL(0);
        }
        w.WriteBL(0);
    }

    private static void WriteTableCellValue(BitWriter w, List<string> central,
        TableEntityCell cell, bool emptyFormattedValue = false)
    {
        bool textPromote = cell.ValueDataType == 0
            && (!string.IsNullOrEmpty(cell.Text) || !string.IsNullOrEmpty(cell.FormattedValue));
        w.WriteBL(cell.ValueFormatFlags);
        w.WriteBL(textPromote ? 4 : cell.ValueDataType);
        switch (textPromote ? 4 : cell.ValueDataType)
        {
            case 2:
                w.WriteBD(cell.ValueDouble);
                break;
            case 0:
                break;
            case 4: case 0x200:
                var bytes = System.Text.Encoding.Unicode.GetBytes(
                    cell.Text is { Length: > 0 } st ? st : cell.FormattedValue ?? string.Empty);
                w.WriteBL(bytes.Length + 2);
                foreach (var b in bytes) w.WriteRC(b);
                w.WriteRC(0); w.WriteRC(0);
                break;
            default:
                w.WriteBL(0);
                break;
        }
        w.WriteBL(cell.ValueUnitType);
        Tv(central, string.Empty);
        if (cell.ValueUnitType != 12)
            Tv(central, emptyFormattedValue ? string.Empty
                : cell.FormattedValue is { Length: > 0 } fv ? fv : cell.Text);
    }

    private static void WriteTableCellStyle(BitWriter w, List<string> central, int type,
        int propertyOverrideFlags, int cfPovr, int cfAlign, double cfTextHeight,
        int marginFlags, double[]? margins, bool minimal = false)
    {
        w.WriteBL(type);
        if (minimal)
        {
            w.WriteBS(0);
            return;
        }
        w.WriteBS(TableCsDataFlags);
        w.WriteBL(propertyOverrideFlags);
        w.WriteBL(0);
        WriteTableCmc(w, TableCsBgRgb);
        w.WriteBL(1);
        WriteTableContentFormat(w, central, cfPovr, TableCfVdtGeneral, cfAlign, cfTextHeight);
        w.WriteBS((short)marginFlags);
        if (marginFlags != 0 && margins is not null)
            for (int i = 0; i < 6; i++) w.WriteBD(margins[i]);
        w.WriteBL(0);
    }

    private static void WriteTableContentFormat(BitWriter w, List<string> central,
        int povr, int valueDataType, int alignment, double textHeight)
    {
        w.WriteBL(povr);
        w.WriteBL(0);
        w.WriteBL(valueDataType);
        w.WriteBL(0);
        Tv(central, string.Empty);
        w.WriteBD(0);
        w.WriteBD(1);
        w.WriteBL(alignment);
        WriteTableCmc(w, TableCfColorRgb);
        w.WriteBD(textHeight);
    }

    private static void WriteTableCmc(BitWriter w, uint rgb)
    {
        w.WriteBS(0);
        w.WriteBL(unchecked((int)rgb));
        w.WriteRC(0);
    }

    private static void Tv(List<string> central, string? value) => central.Add(value ?? string.Empty);

    internal static bool WriteTableTypeAxisHandlesR2010Up(BitWriter w, DwgObjectRecord rec)
    {
        var cap = rec.Table!;
        if (cap.TypeAxisRefs.Count < 2) return false;

        var blockHeader = cap.TypeAxisRefs[0];
        var tableStyle = cap.TypeAxisRefs[1];
        bool oracleForm = rec.SourceVersion is DwgVersion.AC1015 or DwgVersion.AC1018;
        w.WriteHandleRef(blockHeader.Code, blockHeader.Value);
        w.WriteHandleRef(4, 0);
        w.WriteHandleRef(5, 0);
        if (!oracleForm)
            for (int i = 0; i < cap.Columns; i++)
                w.WriteHandleRef(5, 0);
        for (int r = 0; r < cap.Rows; r++)
        {
            for (int c = 0; c < cap.Columns; c++)
            {
                int idx = r * cap.Columns + c;
                var cell = idx < cap.Cells.Count ? cap.Cells[idx] : null;
                if (!oracleForm || TableCellHasContent(cell))
                    w.WriteHandleRef(5, 0);
                w.WriteHandleRef(5, 0);
            }
            if (!oracleForm)
                w.WriteHandleRef(5, 0);
        }
        w.WriteHandleRef(5, 0);
        w.WriteHandleRef(tableStyle.Code, tableStyle.Value);
        return true;
    }

    private static bool RewritesTableR2013Up(DwgObjectRecord rec, Target t)
        => rec.LogicalType == TypeTable && rec.SourceVersion == DwgVersion.AC1024
           && t.Version >= DwgVersion.AC1027;

    internal static bool RewriteTableR2013Up(BitWriter w, DwgObjectRecord rec, byte[] src,
        long geoStart, long geoLen)
    {
        long geoEnd = geoStart + geoLen;
        if (geoLen <= 0 || geoEnd > (long)src.Length * 8) return false;
        var tmp = new BitWriter(64);
        try
        {
            var br = new BitReader(src, 0);
            DwgObjectRecord.SeekTo(br, geoStart);
            br.Read3BD();
            byte sf = br.ReadBB();
            double sx = 1.0;
            switch (sf)
            {
                case 0: sx = br.ReadRD(); br.ReadDD(sx); br.ReadDD(sx); break;
                case 1: br.ReadDD(1.0); br.ReadDD(1.0); break;
                case 2: sx = br.ReadRD(); break;
                case 3: break;
            }
            br.ReadBD();
            br.Read3BD();
            if (br.ReadB()) br.ReadBL();
            br.ReadRC();
            br.ReadBL();
            long fieldStart = br.BitPosition;
            br.ReadB();
            long fieldEnd = br.BitPosition;
            if (fieldEnd > geoEnd) return false;

            CopyBits(tmp, src, geoStart, (int)(fieldStart - geoStart));
            tmp.WriteBL(0);
            long rest = geoEnd - fieldEnd;
            if (rest > 0) CopyBits(tmp, src, fieldEnd, (int)rest);
        }
        catch
        {
            return false;
        }
        Append(w, tmp);
        if (DebugTrace)
            System.Console.Error.WriteLine(
                $"TBLUP h={rec.Handle:X} {rec.SourceVersion} unknown_b(B)→BL(0) " +
                $"geoBits={geoLen}→{tmp.BitPosition}");
        return true;
    }

    private static bool RewritesTableR2013Down(DwgObjectRecord rec, Target t)
        => rec.LogicalType == TypeTable && rec.SourceVersion >= DwgVersion.AC1027
           && t.Version == DwgVersion.AC1024;

    internal static bool RewriteTableR2013Down(BitWriter w, DwgObjectRecord rec, byte[] src,
        long geoStart, long geoLen)
    {
        long geoEnd = geoStart + geoLen;
        if (geoLen <= 0 || geoEnd > (long)src.Length * 8) return false;
        var tmp = new BitWriter(64);
        try
        {
            var br = new BitReader(src, 0);
            DwgObjectRecord.SeekTo(br, geoStart);
            br.Read3BD();
            byte sf = br.ReadBB();
            double sx = 1.0;
            switch (sf)
            {
                case 0: sx = br.ReadRD(); br.ReadDD(sx); br.ReadDD(sx); break;
                case 1: br.ReadDD(1.0); br.ReadDD(1.0); break;
                case 2: sx = br.ReadRD(); break;
                case 3: break;
            }
            br.ReadBD();
            br.Read3BD();
            if (br.ReadB()) br.ReadBL();
            br.ReadRC();
            br.ReadBL();
            long fieldStart = br.BitPosition;
            br.ReadBL();
            long fieldEnd = br.BitPosition;
            if (fieldEnd > geoEnd) return false;

            CopyBits(tmp, src, geoStart, (int)(fieldStart - geoStart));
            tmp.WriteB(true);
            long rest = geoEnd - fieldEnd;
            if (rest > 0) CopyBits(tmp, src, fieldEnd, (int)rest);
        }
        catch
        {
            return false;
        }
        Append(w, tmp);
        if (DebugTrace)
            System.Console.Error.WriteLine(
                $"TBLDN h={rec.Handle:X} {rec.SourceVersion} unknown_bl(BL0)→B(True) " +
                $"geoBits={geoLen}→{tmp.BitPosition}");
        return true;
    }
}
