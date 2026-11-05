using DwgParser.Core.Render;

namespace DwgParser.Core.Objects;

public abstract partial class TableEntity
{
    protected override string WriteSvg(RenderContext ctx, Matrix m)
    {
        var lo = TableLayout.Compute(this);
        if (lo is null) return "";

        var tm = m * Matrix.Translation(Start.X, Start.Y)
                   * Matrix.Rotation(Rotation)
                   * Matrix.Scale(ScaleX, ScaleY);

        foreach (var (x, y) in new[]
                 {
                     tm.Apply(0, 0), tm.Apply(lo.Width, 0),
                     tm.Apply(0, -lo.Height), tm.Apply(lo.Width, -lo.Height),
                 })
            ctx.Expand(x, y);

        string stroke = ctx.EntityColor(this);
        var sb = new System.Text.StringBuilder();

        foreach (var cp in lo.Cells)
        {
            if (cp.Background is not { } bg) continue;
            string fill = CmcHex(ctx, bg);
            if (fill.Length == 0) continue;
            AppendRect(sb, tm, cp.Rect, fill);
        }

        double hw = Math.Max(lo.Width, 1e-9) * 1e-4;
        for (int c = 0; c <= lo.Columns; c++)
        {
            double x = lo.ColBoundaries[c];
            for (int r = 0; r < lo.Rows; r++)
            {
                if (c > 0 && c < lo.Columns && lo.SameMerge(r, c - 1, r, c)) continue;
                double ya = lo.RowBoundaries[r], yb = lo.RowBoundaries[r + 1];
                AppendLine(sb, tm, x, Math.Min(ya, yb) - hw, x, Math.Max(ya, yb) + hw, stroke);
            }
        }
        for (int r = 0; r <= lo.Rows; r++)
        {
            double y = lo.RowBoundaries[r];
            for (int c = 0; c < lo.Columns; c++)
            {
                if (r > 0 && r < lo.Rows && lo.SameMerge(r - 1, c, r, c)) continue;
                AppendLine(sb, tm, lo.ColBoundaries[c] - hw, y, lo.ColBoundaries[c + 1] + hw, y, stroke);
            }
        }

        foreach (var cp in lo.Cells)
        {
            if (!cp.HasText) continue;
            string text = RenderContext.SanitizeText(cp.Text);
            if (text.Trim().Length == 0) continue;
            AppendCellText(sb, ctx, tm, stroke, cp, text);
        }
        return sb.ToString();
    }

    private static void AppendRect(System.Text.StringBuilder sb, Matrix tm, TableLayout.BitRect r, string fill)
    {
        var (x0, y0) = tm.Apply(r.Left, r.Bottom);
        var (x1, y1) = tm.Apply(r.Right, r.Bottom);
        var (x2, y2) = tm.Apply(r.Right, r.Top);
        var (x3, y3) = tm.Apply(r.Left, r.Top);
        sb.Append($"    <path d=\"M {RenderContext.F(x0)} {RenderContext.F(y0)}")
          .Append($" L {RenderContext.F(x1)} {RenderContext.F(y1)}")
          .Append($" L {RenderContext.F(x2)} {RenderContext.F(y2)}")
          .Append($" L {RenderContext.F(x3)} {RenderContext.F(y3)} Z\"")
          .Append($" fill=\"{fill}\" stroke=\"none\"/>\n");
    }

    private static void AppendLine(System.Text.StringBuilder sb, Matrix tm,
        double ax, double ay, double bx, double by, string stroke)
    {
        var (x1, y1) = tm.Apply(ax, ay);
        var (x2, y2) = tm.Apply(bx, by);
        sb.Append($"    <line x1=\"{RenderContext.F(x1)}\" y1=\"{RenderContext.F(y1)}\"")
          .Append($" x2=\"{RenderContext.F(x2)}\" y2=\"{RenderContext.F(y2)}\"")
          .Append($" stroke=\"{stroke}\" stroke-width=\"1\" vector-effect=\"non-scaling-stroke\"/>\n");
    }

    private void AppendCellText(System.Text.StringBuilder sb, RenderContext ctx,
        Matrix tm, string stroke, TableLayout.CellPlan cp, string text)
    {
        int a = cp.Alignment is >= 1 and <= 9 ? cp.Alignment : 5;
        int col = (a - 1) % 3;
        int row = (a - 1) / 3;

        var rect = cp.Rect;
        double margin = Math.Min(rect.W, rect.H) * 0.15;
        double x = col switch { 0 => rect.Left + margin, 1 => rect.CenterX, _ => rect.Right - margin };
        double y = row switch { 0 => rect.Top, 1 => rect.CenterY + cp.TextHeight / 2, _ => rect.Bottom + cp.TextHeight };

        var (wx, wy) = tm.Apply(x, y);
        double scaleY = Math.Sqrt(tm.M12 * tm.M12 + tm.M22 * tm.M22);
        double h = cp.TextHeight * Math.Max(scaleY, 1e-6);
        if (!(h > 1e-9)) return;

        string rotAttr = Math.Abs(cp.Rotation) > 1e-9
            ? $" rotate({RenderContext.F(-cp.Rotation * 180 / Math.PI)})"
            : "";
        string anchor = col switch { 0 => "start", 1 => "middle", _ => "end" };
        ulong styleH = cp.StyleHandle != 0 ? cp.StyleHandle : TextStyleHandle;
        string family = ctx.ResolveFontFamily(styleH, text);
        string fill = cp.ContentColor is { } cc ? CmcHex(ctx, cc) : stroke;
        if (fill.Length == 0) fill = stroke;

        string escaped = RenderContext.Escape(text);
        sb.Append($"    <g transform=\"translate({RenderContext.F(wx)},{RenderContext.F(wy)}) scale(1,-1){rotAttr}\">");
        double linePitch = Render.ShxTextShapes.LinePitch(h, 1.0);
        double fontSize = h * Render.ShxTextShapes.EmFactor;
        var parsed = Render.MTextParser.Parse(cp.Text);
        var flatLines = escaped.Split('\n');
        var runLines = parsed.Count == flatLines.Length ? parsed : null;
        for (int i = 0; i < flatLines.Length; i++)
        {
            double dy = i * linePitch;
            sb.Append($"<text x=\"0\" y=\"{RenderContext.F(dy)}\" font-size=\"{RenderContext.F(fontSize)}\"")
              .Append($" font-family=\"{family}\" text-anchor=\"{anchor}\" fill=\"{fill}\">")
              .Append(CellLineSvg(runLines, i, flatLines[i], family)).Append("</text>");
        }
        sb.Append("</g>\n");
    }

    private static string CellLineSvg(System.Collections.Generic.IReadOnlyList<Render.MTextParser.Line>? parsed,
        int lineIndex, string flatLine, string baseFamily)
    {
        if (parsed is null) return flatLine;
        var runs = parsed[lineIndex].Runs;
        bool anyOverride = false;
        foreach (var r in runs)
            if (r.FontSpec is { Length: > 0 }) { anyOverride = true; break; }
        if (!anyOverride) return flatLine;

        var outSb = new System.Text.StringBuilder();
        foreach (var r in runs)
        {
            if (r.Text.Length == 0) continue;
            string fam = r.FontSpec is { Length: > 0 } fs
                ? Render.FontResolver.ResolveRunFamily(fs, baseFamily) : baseFamily;
            if (string.Equals(fam, baseFamily, StringComparison.Ordinal))
                outSb.Append(RenderContext.Escape(r.Text));
            else
                outSb.Append($"<tspan font-family=\"{fam}\">{RenderContext.Escape(r.Text)}</tspan>");
        }
        return outSb.Length > 0 ? outSb.ToString() : flatLine;
    }

    private static string CmcHex(RenderContext ctx, TableCmcColor c)
    {
        _ = ctx;
        if (AciPalette.TryCmcToRgb(c.Rgb, c.Index, out byte r, out byte g, out byte b))
            return $"#{r:X2}{g:X2}{b:X2}";
        return "";
    }
}
