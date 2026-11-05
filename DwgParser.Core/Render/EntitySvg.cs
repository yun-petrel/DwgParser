using DwgParser.Core.Objects;

namespace DwgParser.Core.Render;

internal static class EntitySvg
{
    internal static string RenderArc(DwgEntity e, RenderContext ctx, Matrix m, string strokeColor, bool dashed = false)
    {
        var (cx, cy) = m.Apply(e.Center.X, e.Center.Y);
        double r = e.Radius;
        double a1 = e.StartAngle, a2 = e.EndAngle;
        if (e.ExtrusionZ < 0) { a1 = -a1; a2 = -a2; }
        if (a2 < a1) a2 += Math.PI * 2;
        var (p1x, p1y) = m.Apply(cx + r * Math.Cos(a1), cy + r * Math.Sin(a1));
        var (p2x, p2y) = m.Apply(cx + r * Math.Cos(a2), cy + r * Math.Sin(a2));
        bool large = a2 - a1 > Math.PI;
        bool sweep = a2 > a1;
        ctx.Expand(p1x, p1y); ctx.Expand(p2x, p2y);
        for (int k = 0; k < 8; k++)
        {
            double a = a1 + (a2 - a1) * k / 8.0;
            var (qx, qy) = m.Apply(cx + r * Math.Cos(a), cy + r * Math.Sin(a));
            ctx.Expand(qx, qy);
        }
        string dash = dashed ? " stroke-dasharray=\"4 4\"" : "";
        return $"    <path d=\"M {RenderContext.F(p1x)} {RenderContext.F(p1y)} A {RenderContext.F(r)} {RenderContext.F(r)} 0 {(large ? 1 : 0)} {(sweep ? 1 : 0)} {RenderContext.F(p2x)} {RenderContext.F(p2y)}\" stroke=\"{strokeColor}\" stroke-width=\"1\"{dash} vector-effect=\"non-scaling-stroke\" fill=\"none\"/>\n";
    }

    internal static string RenderEllipse(DwgEntity e, RenderContext ctx, Matrix m, string strokeColor)
    {
        var (cx, cy) = m.Apply(e.Center.X, e.Center.Y);
        double semiMajor = e.Radius;
        double ratio = e.WidthFactor > 0 ? e.WidthFactor : 1.0;
        double semiMinor = semiMajor * ratio;

        double rotation = Math.Atan2(e.Start.Y, e.Start.X);
        bool mirror = e.ExtrusionZ < 0;

        double a1 = e.StartAngle;
        double a2 = e.EndAngle;
        bool isPartial = Math.Abs(a2 - a1) < Math.PI * 2 - 1e-6 && (Math.Abs(a1) > 1e-6 || Math.Abs(a2) > 1e-6);

        if (!isPartial)
        {
            var sb = new System.Text.StringBuilder();
            int steps = 32;
            for (int i = 0; i <= steps; i++)
            {
                double t = 2 * Math.PI * i / steps;
                double lx = semiMajor * Math.Cos(t);
                double ly = semiMinor * Math.Sin(t);
                double rx = lx * Math.Cos(rotation) - ly * Math.Sin(rotation);
                double ry = lx * Math.Sin(rotation) + ly * Math.Cos(rotation);
                if (mirror) ry = -ry;
                var (px, py) = m.Apply(cx + rx, cy + ry);
                ctx.Expand(px, py);

                if (i == 0)
                    sb.Append($"M {RenderContext.F(px)} {RenderContext.F(py)}");
                else
                    sb.Append($" L {RenderContext.F(px)} {RenderContext.F(py)}");
            }
            sb.Append(" Z");

            return $"    <path d=\"{sb}\" stroke=\"{strokeColor}\" stroke-width=\"1\" vector-effect=\"non-scaling-stroke\" fill=\"none\"/>\n";
        }
        else
        {
            if (a2 < a1) a2 += Math.PI * 2;

            var sb = new System.Text.StringBuilder();
            int steps = 32;
            for (int i = 0; i <= steps; i++)
            {
                double t = a1 + (a2 - a1) * i / steps;
                double lx = semiMajor * Math.Cos(t);
                double ly = semiMinor * Math.Sin(t);
                double rx = lx * Math.Cos(rotation) - ly * Math.Sin(rotation);
                double ry = lx * Math.Sin(rotation) + ly * Math.Cos(rotation);
                if (mirror) ry = -ry;
                var (px, py) = m.Apply(cx + rx, cy + ry);
                ctx.Expand(px, py);

                if (i == 0)
                    sb.Append($"M {RenderContext.F(px)} {RenderContext.F(py)}");
                else
                    sb.Append($" L {RenderContext.F(px)} {RenderContext.F(py)}");
            }

            return $"    <path d=\"{sb}\" stroke=\"{strokeColor}\" stroke-width=\"1\" vector-effect=\"non-scaling-stroke\" fill=\"none\"/>\n";
        }
    }

    internal static string RenderPolyline(DwgEntity e, RenderContext ctx, Matrix m, string strokeColor)
    {
        int n = e.Points.Count;
        if (n == 0) return "";
        var d = new System.Text.StringBuilder();
        for (int i = 0; i < n; i++)
        {
            var (x, y) = m.Apply(e.Points[i].X, e.Points[i].Y);
            ctx.Expand(x, y);
            if (i == 0)
                d.Append($"M {RenderContext.F(x)} {RenderContext.F(y)}");
            else
            {
                double bulge = i - 1 < e.Bulges.Count ? e.Bulges[i - 1] : 0.0;
                if (Math.Abs(bulge) < 1e-6)
                    d.Append($" L {RenderContext.F(x)} {RenderContext.F(y)}");
                else
                {
                    var (px, py) = m.Apply(e.Points[i - 1].X, e.Points[i - 1].Y);
                    d.Append(BulgeArc(px, py, x, y, bulge));
                }
            }
        }
        if (e.Closed && n > 1)
        {
            var (x0, y0) = m.Apply(e.Points[0].X, e.Points[0].Y);
            var (x1, y1) = m.Apply(e.Points[n - 1].X, e.Points[n - 1].Y);
            double closeBulge = n - 1 < e.Bulges.Count ? e.Bulges[n - 1] : 0.0;
            if (Math.Abs(closeBulge) < 1e-6)
                d.Append($" L {RenderContext.F(x0)} {RenderContext.F(y0)}");
            else
                d.Append(BulgeArc(x1, y1, x0, y0, closeBulge));
        }
        return d.Length > 0
            ? $"    <path d=\"{d}\" stroke=\"{strokeColor}\" stroke-width=\"1\" vector-effect=\"non-scaling-stroke\" fill=\"none\"/>\n"
            : "";
    }

    private static string BulgeArc(double x1, double y1, double x2, double y2, double bulge)
    {
        double chord = Math.Sqrt((x2 - x1) * (x2 - x1) + (y2 - y1) * (y2 - y1));
        if (chord < 1e-9) return "";
        double radius = chord * (1 + bulge * bulge) / (4 * Math.Abs(bulge));
        double theta = 4 * Math.Atan(bulge);
        bool large = Math.Abs(theta) > Math.PI;
        bool sweep = bulge > 0;
        return $" A {RenderContext.F(radius)} {RenderContext.F(radius)} 0 {(large ? 1 : 0)} {(sweep ? 1 : 0)} {RenderContext.F(x2)} {RenderContext.F(y2)}";
    }

    internal static string RenderInsert(DwgEntity e, RenderContext ctx, Matrix m, int depth)
    {
        if (depth > RenderContext.MaxDepth) return "";
        if (!ctx.BlockContents.TryGetValue(e.BlockHeaderHandle, out var contents))
            return "";
        var t = m * e.InsertPlacement();
        var sb = new System.Text.StringBuilder();
        foreach (var sub in contents.Cast<DwgEntity>())
            sb.Append(sub.ToSvg(ctx, t));
        return sb.ToString();
    }

    internal static string RenderDimension(DwgEntity e, RenderContext ctx, Matrix m, string strokeColor, string fillColor)
    {
        var sb = new System.Text.StringBuilder();

        var blockDisplay = ctx.BlockContents.TryGetValue(e.DimBlockHandle, out var bd) ? bd : null;
        var shape = DimensionRenderer.Plan(e, ctx.ResolveDimStyle(e.DimStyleHandle), blockDisplay);
        if (shape != null)
        {
            var d = new System.Text.StringBuilder();
            bool first = true;
            foreach (var (a, b) in shape.Lines)
            {
                var (x1, y1) = m.Apply(a.X, a.Y);
                var (x2, y2) = m.Apply(b.X, b.Y);
                ctx.Expand(x1, y1); ctx.Expand(x2, y2);
                d.Append(first ? $"M {RenderContext.F(x1)} {RenderContext.F(y1)}" : $" M {RenderContext.F(x1)} {RenderContext.F(y1)}");
                d.Append($" L {RenderContext.F(x2)} {RenderContext.F(y2)}");
                first = false;
            }
            if (d.Length > 0)
                sb.Append($"    <path d=\"{d}\" stroke=\"{strokeColor}\" stroke-width=\"1\" vector-effect=\"non-scaling-stroke\" fill=\"none\"/>\n");

            foreach (var (tip, dir) in shape.Arrows)
            {
                double len = shape.ArrowLength;
                double w = len * DimensionRenderer.ArrowWidthRatio;
                double bx = tip.X - dir.X * len, by = tip.Y - dir.Y * len;
                double nx = -dir.Y * w / 2, ny = dir.X * w / 2;
                var (t1, t2, t3) = (m.Apply(tip.X, tip.Y),
                                    m.Apply(bx + nx, by + ny),
                                    m.Apply(bx - nx, by - ny));
                ctx.Expand(t1.Item1, t1.Item2); ctx.Expand(t2.Item1, t2.Item2); ctx.Expand(t3.Item1, t3.Item2);
                sb.Append($"    <polygon points=\"{RenderContext.F(t1.Item1)},{RenderContext.F(t1.Item2)} {RenderContext.F(t2.Item1)},{RenderContext.F(t2.Item2)} {RenderContext.F(t3.Item1)},{RenderContext.F(t3.Item2)}\" fill=\"{fillColor}\"/>\n");
            }

            if (shape.HasText)
            {
                double rot = e.DimTextRotation != 0 ? e.DimTextRotation : shape.TextRotation;
                double th = shape.TextHeight > 0 ? shape.TextHeight : e.Height;
                sb.Append(RenderTextAt(ctx, m, shape.TextPos.X, shape.TextPos.Y, rot, th,
                    DimensionSvgText(shape.Text), fillColor, e.TextStyleHandle));
            }
            return sb.ToString();
        }

        if (e.Points.Count >= 2)
        {
            var d = new System.Text.StringBuilder();
            for (int i = 0; i < e.Points.Count; i++)
            {
                var (x, y) = m.Apply(e.Points[i].X, e.Points[i].Y);
                ctx.Expand(x, y);
                if (i == 0)
                    d.Append($"M {RenderContext.F(x)} {RenderContext.F(y)}");
                else
                    d.Append($" L {RenderContext.F(x)} {RenderContext.F(y)}");
            }
            sb.Append($"    <path d=\"{d}\" stroke=\"{strokeColor}\" stroke-width=\"1\" fill=\"none\"/>\n");
        }

        string dimText = e.TextValue is { Length: > 0 }
            ? e.TextValue.Replace("<>", DimensionRenderer.FormatMeasurement(e.Measurement))
            : DimensionRenderer.FormatMeasurement(e.Measurement);
        if (dimText.Length > 0)
        {
            double rot = e.DimTextRotation != 0 ? e.DimTextRotation : e.Rotation;
            sb.Append(RenderTextAt(ctx, m, e.Start.X, e.Start.Y, rot, e.Height, dimText, fillColor, e.TextStyleHandle));
        }

        return sb.ToString();
    }

    internal static string RenderText(DwgEntity e, RenderContext ctx, Matrix m, string fillColor)
    {
        if (string.IsNullOrEmpty(e.TextValue)) return "";
        var (x, y) = m.Apply(e.Start.X, e.Start.Y);
        ctx.Expand(x, y);
        return RenderTextAt(ctx, m, e.Start.X, e.Start.Y, e.Rotation, e.Height, e.TextValue, fillColor, e.TextStyleHandle);
    }

    private static string DimensionSvgText(string raw)
    {
        var lines = MTextParser.Parse(raw);
        var sb = new System.Text.StringBuilder();
        foreach (var ln in lines)
        {
            foreach (var run in ln.Runs)
            {
                if (run.IsStack) sb.Append(run.StackTop).Append('/').Append(run.StackBottom);
                else sb.Append(run.Text);
            }
            sb.Append('\n');
        }
        return sb.ToString().TrimEnd('\n');
    }

    internal static string RenderTextAt(RenderContext ctx, Matrix m,
        double wx, double wy, double rotation, double height, string rawText, string fillColor,
        ulong styleHandle = 0)
    {
        var (x, y) = m.Apply(wx, wy);
        ctx.Expand(x, y);
        string rotAttr = Math.Abs(rotation) > 1e-9
            ? $" rotate({RenderContext.F(-rotation * 180 / Math.PI)})"
            : "";
        double h = height > 0 ? height * m.YAxisScale : 2.5;
        string text = RenderContext.Escape(RenderContext.SanitizeText(rawText));
        string fontFamily = ctx.ResolveFontFamily(styleHandle, rawText);
        var sb = new System.Text.StringBuilder();
        sb.Append($"    <g transform=\"translate({RenderContext.F(x)},{RenderContext.F(y)}) scale(1,-1){rotAttr}\">");
        var lines = text.Split('\n');
        double linePitch = ShxTextShapes.LinePitch(h, 1.0);
        double fontSize = h * ShxTextShapes.EmFactor;
        for (int i = 0; i < lines.Length; i++)
        {
            double dy = i * linePitch;
            sb.Append($"<text x=\"0\" y=\"{RenderContext.F(dy)}\" font-size=\"{RenderContext.F(fontSize)}\" font-family=\"{fontFamily}\" fill=\"{fillColor}\">{lines[i]}</text>");
        }
        sb.Append("</g>\n");
        return sb.ToString();
    }

    internal static string RenderPointSequence(DwgEntity e, RenderContext ctx, Matrix m, string strokeColor)
    {
        if (e.Points.Count < 2) return "";
        var d = new System.Text.StringBuilder();
        for (int i = 0; i < e.Points.Count; i++)
        {
            var (x, y) = m.Apply(e.Points[i].X, e.Points[i].Y);
            ctx.Expand(x, y);
            d.Append(i == 0
                ? $"M {RenderContext.F(x)} {RenderContext.F(y)}"
                : $" L {RenderContext.F(x)} {RenderContext.F(y)}");
        }
        return $"    <path d=\"{d}\" stroke=\"{strokeColor}\" stroke-width=\"1\" vector-effect=\"non-scaling-stroke\" fill=\"none\"/>\n";
    }

    internal static string RenderHatch(DwgEntity e, RenderContext ctx, Matrix m, string strokeColor, string fillColor)
    {
        if (e.Points.Count < 4) return "";
        var d = new System.Text.StringBuilder();
        var segments = e.Points.Count / 2;
        var drawn = new List<Vec2>();
        for (int s = 0; s < segments; s++)
        {
            var p1 = e.Points[s * 2];
            var p2 = e.Points[s * 2 + 1];
            var (x1, y1) = m.Apply(p1.X, p1.Y);
            var (x2, y2) = m.Apply(p2.X, p2.Y);
            ctx.Expand(x1, y1); ctx.Expand(x2, y2);

            bool continues = drawn.Count >= 2 &&
                             SamePt(drawn[^1], p1);
            if (drawn.Count == 0)
                d.Append($"M {RenderContext.F(x1)} {RenderContext.F(y1)}");
            else if (!continues)
                d.Append($" M {RenderContext.F(x1)} {RenderContext.F(y1)}");
            d.Append($" L {RenderContext.F(x2)} {RenderContext.F(y2)}");
            drawn.Add(p1);
            drawn.Add(p2);
        }
        if (d.Length == 0) return "";

        bool closed = SamePt(drawn[^1], drawn[0]);
        if (closed) d.Append(" Z");
        string fill = closed && e.SolidFill
            ? $" fill=\"{fillColor}\" fill-opacity=\"{RenderContext.F(HatchGeometry.SolidFillOpacity)}\""
            : " fill=\"none\"";
        return $"    <path d=\"{d}\" stroke=\"{strokeColor}\" stroke-width=\"1\"{fill}/>\n";
    }

    private static bool SamePt(Vec2 a, Vec2 b)
        => Math.Abs(a.X - b.X) < 1e-9 && Math.Abs(a.Y - b.Y) < 1e-9;
}
