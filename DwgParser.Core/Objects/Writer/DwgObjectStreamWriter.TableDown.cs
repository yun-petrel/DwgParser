using DwgParser.Core.IO;

namespace DwgParser.Core.Objects;

public static partial class DwgObjectStreamWriter
{
    private const short TableDownFlagForTableValue = 22;

    private const int TableDownCellOverrideContentFormat = 0x40000;

    private const int TableDownDefaultValueFormatFlags = 3;

    internal static bool CarriesTableR2010Down(DwgObjectRecord rec, Target t)
        => rec.LogicalType == TypeTable && rec.SourceVersion.IsR2010Plus() && !t.R2010Plus
           && rec.Table is { Captured: true } && rec.Table.TypeAxisRefs.Count >= 2;

    internal static void CarryTableR2010Down(BitWriter w, DwgObjectRecord rec, Target t)
    {
        var cap = rec.Table!;
        bool r2007Plus = t.Version >= DwgVersion.AC1021;
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
        w.WriteBD0(cap.Rotation);
        w.WriteBEExplicit(cap.NormalX, cap.NormalY, cap.NormalZ);
        w.WriteB(cap.HasAttribs);

        w.WriteBS(TableDownFlagForTableValue);
        w.WriteBD(cap.HorizDirX); w.WriteBD(cap.HorizDirY); w.WriteBD(cap.HorizDirZ);
        int cols = cap.Columns, rows = cap.Rows;
        w.WriteBL(cols);
        w.WriteBL(rows);

        for (int i = 0; i < cols; i++) w.WriteBD(i < cap.ColWidths.Count ? cap.ColWidths[i] : 0.0);
        for (int i = 0; i < rows; i++) w.WriteBD(i < cap.RowHeights.Count ? cap.RowHeights[i] : 0.0);

        var spans = TableDownMergeSpans(cap);
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                int idx = r * cols + c;
                WriteTableCellDown(w, central, idx < cap.Cells.Count ? cap.Cells[idx] : null, r2007Plus,
                    spans[idx].W, spans[idx].H, spans[idx].Covered,
                    cap.BaseTextHeight > 0 ? cap.BaseTextHeight : null);
            }

        w.WriteB(false);

        w.WriteB(false);
        w.WriteB(false);
        w.WriteB(false);

        if (r2007Plus)
        {
            long contentStart = w.BitPosition;
            foreach (var s in central)
                WriteCentralTu(w, s);
            WriteStringStreamSize(w, (uint)(w.BitPosition - contentStart));
            w.WriteB(true);
        }
        else
        {
        }

        if (DebugTrace)
            System.Console.Error.WriteLine(
                $"TDN h={rec.Handle:X} {rec.SourceVersion}→{t.Version} cols={cols} rows={rows} " +
                $"cells={cap.Cells.Count} body={w.BitPosition}位 r2007={r2007Plus}");
    }

    private static (int W, int H, bool Covered)[] TableDownMergeSpans(Records.TableCapture cap)
    {
        int cols = cap.Columns, rows = cap.Rows;
        int n = cols > 0 && rows > 0 ? cols * rows : 0;
        var spans = new (int W, int H, bool Covered)[Math.Max(n, 0)];
        for (int i = 0; i < spans.Length; i++) spans[i] = (1, 1, false);
        foreach (var mc in cap.MergedCells)
        {
            int w = mc.RightCol - mc.LeftCol + 1, h = mc.BottomRow - mc.TopRow + 1;
            if (w < 1 || h < 1) continue;
            if (mc.TopRow < 0 || mc.LeftCol < 0 || mc.BottomRow >= rows || mc.RightCol >= cols) continue;
            int anchor = mc.TopRow * cols + mc.LeftCol;
            if (anchor < spans.Length) spans[anchor] = (w, h, false);
            for (int rr = mc.TopRow; rr <= mc.BottomRow; rr++)
                for (int cc = mc.LeftCol; cc <= mc.RightCol; cc++)
                    if ((rr != mc.TopRow || cc != mc.LeftCol) && rr * cols + cc < spans.Length)
                        spans[rr * cols + cc] = (1, 1, true);
        }
        return spans;
    }

    private static void WriteTableCellDown(BitWriter w, List<string> central,
        TableEntityCell? cell, bool r2007Plus, int spanW, int spanH, bool covered,
        double? fallbackHeight = null)
    {
        var c = cell ?? new TableEntityCell();
        short wType = (short)(c.Type == 2 ? 2 : 1);
        w.WriteBS(wType);
        w.WriteRC(unchecked((byte)c.EdgeFlags));
        w.WriteB(covered);
        w.WriteB(c.IsAutoFit);
        w.WriteBL(spanW);
        w.WriteBL(spanH);
        w.WriteBD(c.Rotation);
        if (!r2007Plus && wType == 1)
            WriteThinCellText(w, c.Text);

        bool propsOverride = c.ContentFormat is { PropertyOverrideFlags: not 0 };
        int f = r2007Plus
            ? (propsOverride ? TableDownCellOverrideContentFormat : 0)
            : propsOverride ? c.CellFlagOverride : 0;
        double? ownHeight = c.TextHeight is > 0 ? c.TextHeight : c.ContentFormat?.TextHeight;
        double? downHeight = r2007Plus || ownHeight is not > 0 || fallbackHeight is not > 0 ? null
            : Math.Abs(ownHeight.Value - fallbackHeight.Value) < 1e-12 ? null
            : ownHeight;
        bool wantOverride = r2007Plus
            ? f != 0
            : (propsOverride || c.HasOverride || c.Alignment is > 0 || c.TextHeight is > 0);
        if (!r2007Plus) f = downHeight is > 0 ? 0x20 : 0;
        if (c.Alignment is > 0) f |= 0x01;
        if (!r2007Plus && propsOverride && f == 0) f = TableDownCellOverrideContentFormat;
        if (wantOverride || f != 0)
        {
            w.WriteB(true);
            w.WriteBL(f);
            w.WriteRC(unchecked((byte)c.VirtualEdgeFlag));
            if ((f & 0x01) != 0) w.WriteBS(c.Alignment is > 0 ? c.Alignment!.Value : (short)5);
            if ((f & 0x20) != 0)
                w.WriteBD((c.TextHeight is > 0 ? c.TextHeight : downHeight)!.Value);
        }
        else
        {
            w.WriteB(false);
        }

        if (r2007Plus)
            WriteTableCellValueDown(w, central, c);
    }

    private static void WriteTableCellValueDown(BitWriter w, List<string> central, TableEntityCell c)
    {
        w.WriteBL(0);
        w.WriteBL(c.ValueFormatFlags != 0 ? c.ValueFormatFlags : TableDownDefaultValueFormatFlags);
        int dt = c.ValueDataType;
        if (dt is 0 or 1 && c.Text.Length > 0) dt = 4;
        w.WriteBL(dt);
        switch (dt)
        {
            case 0:
                break;
            case 2:
                w.WriteBD(c.ValueDouble);
                break;
            case 4:
            case 0x200:
            {
                var bytes = System.Text.Encoding.Unicode.GetBytes(c.Text ?? string.Empty);
                w.WriteBS((short)(bytes.Length + 2));
                foreach (var b in bytes) w.WriteRC(b);
                w.WriteRC(0); w.WriteRC(0);
                break;
            }
            case 8:
                w.WriteBL(0);
                break;
            default:
                w.WriteBL(0);
                break;
        }
        int ut = c.ValueUnitType;
        w.WriteBL(ut);
        central.Add(string.Empty);
        if (ut != 12)
            central.Add(c.FormattedValue is { Length: > 0 } fv ? fv : (c.Text ?? string.Empty));
    }

    internal static bool WriteTableTypeAxisHandlesR2010Down(BitWriter w, DwgObjectRecord rec)
    {
        var cap = rec.Table!;
        if (cap.TypeAxisRefs.Count < 2) return false;
        var blockHeader = cap.TypeAxisRefs[0];
        var tableStyle = cap.TypeAxisRefs[^1];
        w.WriteHandleRef(blockHeader.Code, blockHeader.Value);
        w.WriteHandleRef(tableStyle.Code, tableStyle.Value);
        if (DebugTrace) System.Console.Error.WriteLine(
            $"TDNHS h={rec.Handle:X} cols={cap.Columns} rows={cap.Rows} nAxis={cap.TypeAxisRefs.Count} " +
            $"ax0=(c{blockHeader.Code},{blockHeader.Value:X}) ax1=(c{tableStyle.Code},{tableStyle.Value:X})");
        int cols = cap.Columns, rows = cap.Rows;
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                int idx = r * cols + c;
                bool isBlock = idx < cap.Cells.Count && cap.Cells[idx].Type == 2;
                w.WriteHandleRef(5, 0);
                if (isBlock) w.WriteHandleRef(5, 0);
            }
        return true;
    }
}
