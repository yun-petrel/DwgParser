using DwgParser.Core.Objects;

namespace DwgParser.Core.Render;

public sealed class TableLayout
{
    private const double FallbackTableWidth = 36.0;

    private const double FallbackRowHeight = 5.0;

    private const double FallbackTextHeight = 1.8;

    private const double TextHeightRowRatio = 0.8;

    private TableLayout(int cols, int rows)
    {
        Columns = cols;
        Rows = rows;
        ColBoundaries = new double[cols + 1];
        RowBoundaries = new double[rows + 1];
    }

    public int Columns { get; }

    public int Rows { get; }

    public double[] ColBoundaries { get; }

    public double[] RowBoundaries { get; }

    public double Width => ColBoundaries[^1];

    public double Height => -RowBoundaries[^1];

    public readonly record struct BitRect(double Left, double Bottom, double Right, double Top)
    {
        public double CenterX => (Left + Right) / 2;

        public double CenterY => (Bottom + Top) / 2;

        public double W => Right - Left;

        public double H => Top - Bottom;
    }

    public readonly record struct CellPlan(
        int Row, int Col, BitRect Rect, string Text, int Alignment,
        double TextHeight, double Rotation, TableCmcColor? Background, TableCmcColor? ContentColor,
        ulong StyleHandle)
    {
        public bool HasText => Text.Length > 0;
    }

    public List<CellPlan> Cells { get; } = new();

    public List<(int TopRow, int LeftCol, int BottomRow, int RightCol)> MergedRects { get; } = new();

    public bool[] Covered { get; private set; } = Array.Empty<bool>();

    private int[] _mergeGroup = Array.Empty<int>();

    public bool SameMerge(int row1, int col1, int row2, int col2)
    {
        if (_mergeGroup.Length == 0) return false;
        if ((uint)col1 >= Columns || (uint)row1 >= Rows) return false;
        if ((uint)col2 >= Columns || (uint)row2 >= Rows) return false;
        int g = _mergeGroup[row1 * Columns + col1];
        return g >= 0 && g == _mergeGroup[row2 * Columns + col2];
    }

    public (int Row, int Col) CellCoord(int index) => (index / Columns, index % Columns);

    public static TableLayout? Compute(TableEntity table)
    {
        int cols = table.Columns, rows = table.Rows;
        if (cols <= 0 || rows <= 0) return null;
        if (cols > 4096 || rows > 4096) return null;

        var lo = new TableLayout(cols, rows);
        lo.BuildColumns(table);
        lo.BuildRows(table);
        lo.BuildMerges(table);
        lo.BuildCells(table);
        return lo;
    }

    private void BuildColumns(TableEntity table)
    {
        var widths = table.ColWidths;
        double dx = 0;
        for (int c = 0; c < Columns; c++)
        {
            double w = c < widths.Count ? widths[c] : 0;
            if (!(w > 1e-9) || !double.IsFinite(w)) w = FallbackTableWidth / Columns;
            if (w > 1e6) w = FallbackTableWidth / Columns;
            dx += w;
            ColBoundaries[c + 1] = dx;
        }
    }

    private void BuildRows(TableEntity table)
    {
        var heights = table.RowHeights;
        double dy = 0;
        for (int r = 0; r < Rows; r++)
        {
            double h = r < heights.Count ? heights[r] : 0;
            if (!(h > 1e-9) || !double.IsFinite(h)) h = FallbackRowHeight;
            if (h > 1e6) h = FallbackRowHeight;
            dy += h;
            RowBoundaries[r + 1] = -dy;
        }
    }

    private void BuildMerges(TableEntity table)
    {
        Covered = new bool[Columns * Rows];
        _mergeGroup = new int[Columns * Rows];
        Array.Fill(_mergeGroup, -1);
        int n = Columns * Rows;

        var isAnchor = new bool[n];
        var rects = new List<(int TopRow, int LeftCol, int BottomRow, int RightCol)>();

        for (int i = 0; i < n; i++)
        {
            int w = 1, h = 1;
            if (i < table.Cells.Count)
            {
                var cl = table.Cells[i];
                w = cl.MergedWidth > 1 ? cl.MergedWidth : 1;
                h = cl.MergedHeight > 1 ? cl.MergedHeight : 1;
            }
            if (w <= 1 && h <= 1) continue;
            int r = i / Columns, c = i % Columns;
            if (r + h > Rows || c + w > Columns) continue;
            isAnchor[i] = true;
            rects.Add((r, c, r + h - 1, c + w - 1));
        }

        foreach (var mc in table.MergedCells)
        {
            if (mc.BottomRow < mc.TopRow || mc.RightCol < mc.LeftCol) continue;
            if (mc.TopRow < 0 || mc.BottomRow >= Rows) continue;
            if (mc.LeftCol < 0 || mc.RightCol >= Columns) continue;
            isAnchor[mc.TopRow * Columns + mc.LeftCol] = true;
            rects.Add((mc.TopRow, mc.LeftCol, mc.BottomRow, mc.RightCol));
        }

        rects.Sort((a, b) => a.TopRow != b.TopRow ? a.TopRow - b.TopRow : a.LeftCol - b.LeftCol);
        foreach (var (top, left, bottom, right) in rects)
        {
            int i = top * Columns + left;
            if (!isAnchor[i] || Covered[i]) continue;
            int gid = MergedRects.Count;
            MergedRects.Add((top, left, bottom, right));
            for (int rr = top; rr <= bottom; rr++)
                for (int cc = left; cc <= right; cc++)
                {
                    Covered[rr * Columns + cc] = true;
                    _mergeGroup[rr * Columns + cc] = gid;
                }
            Covered[i] = false;
        }
    }

    private Dictionary<int, (int TopRow, int LeftCol, int BottomRow, int RightCol)>? _rectByAnchor;

    private void BuildCells(TableEntity table)
    {
        _rectByAnchor = new Dictionary<int, (int, int, int, int)>();
        foreach (var rect in MergedRects)
            _rectByAnchor[rect.TopRow * Columns + rect.LeftCol] = rect;

        double defaultHeight = DefaultTextHeight(table);
        for (int r = 0; r < Rows; r++)
        {
            for (int c = 0; c < Columns; c++)
            {
                int i = r * Columns + c;
                TableEntityCell? cell = i < table.Cells.Count ? table.Cells[i] : null;

                if (Covered[i] || IsInsideMergedRect(i, c, r)) continue;
                if (cell is { Type: 2 }) continue;

                var rect = RectOf(r, c);
                string text = CellText(cell);

                var styles = StyleChain(table, cell, c, r);

                TableCmcColor? bg = cell?.BgColor;
                if (bg is null)
                    foreach (var s in styles)
                        if (s.BackgroundColor is { } sbg && HasExplicitColor(sbg)) { bg = sbg; break; }
                bg ??= RowFill(table, r);

                if (text.Length == 0 && bg is null
                    && cell is not { HasOverride: true }) continue;

                var cf = FirstContentFormat(styles, f => f.TextHeight is > 0.5 && double.IsFinite(f.TextHeight));
                double? own = cell?.ContentFormat is { } ocf
                              && ocf.TextHeight is > 0.5 && double.IsFinite(ocf.TextHeight)
                    ? ocf.TextHeight : null;
                double? ch = cell?.TextHeight ?? own ?? cf?.TextHeight;
                double th = ch is > 1e-9 && double.IsFinite(ch.Value) ? ch.Value : defaultHeight;
                double cap = Math.Max(rect.H * TextHeightRowRatio, 1e-6);
                if (th > cap) th = cap;
                if (th < 1e-6) th = Math.Min(FallbackTextHeight, cap);

                int align = cell?.Alignment ?? 0;
                if (align is not (>= 1 and <= 9))
                    align = FirstContentFormat(styles, f => f.CellAlignment is >= 1 and <= 9)?.CellAlignment ?? 0;
                if (align is not (>= 1 and <= 9))
                    align = RowAlignment(table, r) ?? 5;

                double rot = cell?.Rotation ?? 0;
                if (rot == 0)
                    rot = FirstContentFormat(styles, f => f.Rotation != 0)?.Rotation ?? 0;

                var cc = cell?.ContentColor;
                if (cc is null)
                    foreach (var s in styles)
                        if (s.ContentFormat?.ContentColor is { } sc && HasExplicitColor(sc)) { cc = sc; break; }

                ulong styleH = cell?.TextStyleHandle ?? 0;
                if (styleH == 0) styleH = cell?.ContentFormat?.TextStyleHandle ?? 0;
                if (styleH == 0)
                    styleH = FirstContentFormat(styles, f => f.TextStyleHandle != 0)?.TextStyleHandle ?? 0;

                Cells.Add(new CellPlan(r, c, rect, text, align, th, rot, bg, cc, styleH));
            }
        }
    }

    private static bool HasExplicitColor(TableCmcColor c) =>
        (c.Rgb & 0xFFFFFF) != 0 || c.Index is >= 1 and <= 255;

    private static List<TableCellStyle> StyleChain(TableEntity table, TableEntityCell? cell, int col, int row)
    {
        var list = new List<TableCellStyle>(4);
        void Add(TableCellStyle? s) { if (s is { DataFlags: not 0 }) list.Add(s); }
        Add(cell?.CellStyle);
        if (col < table.ColumnStyles.Count) Add(table.ColumnStyles[col]);
        if (row < table.RowStyles.Count) Add(table.RowStyles[row]);
        Add(table.BaseCellStyle);
        return list;
    }

    private static TableContentFormat? FirstContentFormat(
        List<TableCellStyle> styles, Func<TableContentFormat, bool> hasValue)
    {
        foreach (var s in styles)
            if (s.ContentFormat is { } f && hasValue(f)) return f;
        return null;
    }

    private bool IsInsideMergedRect(int index, int col, int row)
    {
        foreach (var m in MergedRects)
        {
            if (col < m.LeftCol || col > m.RightCol) continue;
            if (row < m.BottomRow || row > m.TopRow) continue;
            return index != m.TopRow * Columns + m.LeftCol;
        }
        return false;
    }

    public BitRect RectOf(int row, int col)
    {
        if (_rectByAnchor != null && _rectByAnchor.TryGetValue(row * Columns + col, out var m))
            return new BitRect(ColBoundaries[m.LeftCol], RowBoundaries[m.BottomRow + 1],
                ColBoundaries[m.RightCol + 1], RowBoundaries[m.TopRow]);
        return new BitRect(ColBoundaries[col], RowBoundaries[row + 1],
            ColBoundaries[col + 1], RowBoundaries[row]);
    }

    private static TableCmcColor? RowFill(TableEntity table, int row)
    {
        var so = table.StyleOverride;
        if (so is null) return null;
        if (row == 0) return so.TitleSuppressed == true ? so.HeaderFillColor : so.TitleFillColor;
        if (row == 1) return so.HeaderFillColor;
        return so.DataFillColor;
    }

    private static int? RowAlignment(TableEntity table, int row)
    {
        var so = table.StyleOverride;
        if (so is null) return null;
        short? a = row == 0
            ? (so.TitleSuppressed == true ? so.HeaderRowAlignment : so.TitleRowAlignment)
            : row == 1 ? so.HeaderRowAlignment : so.DataRowAlignment;
        return a is >= 1 and <= 9 ? a : null;
    }

    private static string CellText(TableEntityCell? cell)
    {
        if (cell is null) return "";
        if (cell.Type is not (0 or 1)) return "";
        string s = cell.FormattedValue is { Length: > 0 } fv ? fv : cell.Text;
        return s ?? "";
    }

    private static double DefaultTextHeight(TableEntity table)
    {
        foreach (var h in table.RowHeights)
            if (h > 0.5 && double.IsFinite(h)) return h * 0.5;
        return FallbackTextHeight;
    }
}
