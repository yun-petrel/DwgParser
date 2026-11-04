using DwgParser.Core.Render;

namespace DwgParser.Core.Objects;

public enum DwgEntityType
{
    Unknown, Text, Insert, Vertex2D, Attrib, SeqEnd, Polyline2D, Arc,
    Circle, Line, Point, Solid, Trace, Ellipse, LwPolyline, BlockHeader, EndBlk, Block,
    Dimension, Spline, Ray, XLine, Leader, Hatch, MText, MLine, Tolerance, Viewport, Region, Solid3d, Body,
    AttDef, Table, MLeader,

    PointSequence,
}

public readonly record struct Vec2(double X, double Y);

public readonly record struct Vec3(double X, double Y, double Z);

public class DwgEntity : EntityBase, IEntity
{
    EntityKind IEntity.Kind => ToEntityKind(Kind);

    private LineGeometry? _lineGeo;
    private RadialGeometry? _radialGeo;
    private PolylineGeometry? _polyGeo;
    private CornersGeometry? _cornersGeo;

    private LineGeometry LineGeo => _lineGeo ??= new();
    private RadialGeometry RadialGeo => _radialGeo ??= new();
    private PolylineGeometry PolyGeo => _polyGeo ??= new();
    private CornersGeometry CornersGeo => _cornersGeo ??= new();

    public Vec2 Start { get => LineGeo.Start; set => LineGeo.Start = value; }
    public Vec2 End { get => LineGeo.End; set => LineGeo.End = value; }

    public double StartZ { get => LineGeo.StartZ; set => LineGeo.StartZ = value; }

    public double EndZ { get => LineGeo.EndZ; set => LineGeo.EndZ = value; }

    public Vec2 Center { get => RadialGeo.Center; set => RadialGeo.Center = value; }
    public double Radius { get => RadialGeo.Radius; set => RadialGeo.Radius = value; }
    public double StartAngle { get => RadialGeo.StartAngle; set => RadialGeo.StartAngle = value; }
    public double EndAngle { get => RadialGeo.EndAngle; set => RadialGeo.EndAngle = value; }

    public string TextValue { get; set; } = "";
    public double Height { get; set; }
    public double Rotation { get; set; }
    public double WidthFactor { get; set; } = 1.0;

    public List<Vec2> Points => PolyGeo.Points;
    public List<double> Bulges => PolyGeo.Bulges;
    public ulong BlockHeaderHandle { get; set; }
    public bool HasAttributes { get; set; }
    public double ScaleX { get; set; } = 1.0;
    public double ScaleY { get; set; } = 1.0;
    public double ScaleZ { get; set; } = 1.0;

    public Matrix InsertPlacement()
        => Matrix.Translation(Start.X, Start.Y)
           * Matrix.Rotation(Rotation)
           * Matrix.Scale(ScaleX, ScaleY);

    public double ConstantWidth { get => PolyGeo.ConstantWidth; set => PolyGeo.ConstantWidth = value; }

    public List<Vec2> Corners => CornersGeo.Corners;

    public bool SolidFill { get => CornersGeo.SolidFill; set => CornersGeo.SolidFill = value; }

    public string Name { get; set; } = "";


    public bool Closed { get => PolyGeo.Closed; set => PolyGeo.Closed = value; }

    public List<double> VertexStartWidths => PolyGeo.VertexStartWidths;

    public List<double> VertexEndWidths => PolyGeo.VertexEndWidths;

    public bool PlineGen { get => PolyGeo.PlineGen; set => PolyGeo.PlineGen = value; }

    public string Tag { get; set; } = "";

    public string Prompt { get; set; } = "";

    public byte AttFlags { get; set; }

    public short AttFieldLength { get; set; }

    public int AttIsLockedInBlock { get; set; } = -1;

    public int AttKeepDuplicateRecords { get; set; } = -1;

    public int AttMTextType { get; set; } = -1;

    public short TextAttachment { get; set; }

    public double RectWidth { get; set; }

    public double RectHeight { get; set; }

    public short LineSpacingStyle { get; set; }
    public double LineSpacingFactor { get; set; } = 1.0;

    public Vec2 AlignmentPoint { get; set; }

    public double ObliqueAngle { get; set; }
    public short TextGenerationFlags { get; set; }

    public short HorizMode { get; set; }
    public short VertMode { get; set; }

    public ulong TextStyleHandle { get; set; }

    public Vec2 Dir { get; set; }

    public bool ArrowheadOn { get; set; }

    public short ArrowheadType { get; set; }

    public double VportWidth { get; set; }
    public Vec2 VportViewCenter { get; set; }
    public double VportViewSize { get; set; }
    public double VportTwist { get; set; }
    public double VportSnapAngle { get; set; }
    public Vec2 VportViewDirection { get; set; }
    public double VportFrontClipZ { get; set; }
    public double VportBackClipZ { get; set; }
    public Vec2 VportViewTarget { get; set; }

    public double Measurement { get; set; }
    public Vec2 DimTextMid { get; set; }
    public Vec2 DimXLine1 { get; set; }
    public Vec2 DimXLine2 { get; set; }
    public Vec2 DimDefPoint { get; set; }
    public Vec2 DimDefPoint2 { get; set; }
    public Vec2 DimLinePoint { get; set; }
    public Vec2 DimCenter { get; set; }
    public ulong DimBlockHandle { get; set; }
    public ulong DimStyleHandle { get; set; }
    public string DimSubtype { get; set; } = "";
    public double DimTextRotation { get; set; }
    public double DimLineRotation { get; set; }

    public short TableRows { get; set; }
    public short TableColumns { get; set; }

    public static EntityKind ToEntityKind(DwgEntityType k) => k switch
    {
        DwgEntityType.Line => EntityKind.Line,
        DwgEntityType.Circle => EntityKind.Circle,
        DwgEntityType.Arc => EntityKind.Arc,
        DwgEntityType.Point => EntityKind.Point,
        DwgEntityType.Text => EntityKind.Text,
        DwgEntityType.Insert => EntityKind.Insert,
        DwgEntityType.LwPolyline => EntityKind.LwPolyline,
        DwgEntityType.Polyline2D => EntityKind.Polyline2D,
        DwgEntityType.Vertex2D => EntityKind.Vertex2D,
        DwgEntityType.Solid => EntityKind.Solid,
        DwgEntityType.Trace => EntityKind.Trace,
        DwgEntityType.BlockHeader => EntityKind.BlockHeader,
        DwgEntityType.EndBlk => EntityKind.EndBlk,
        DwgEntityType.Block => EntityKind.Block,
        DwgEntityType.Attrib => EntityKind.Attrib,
        DwgEntityType.AttDef => EntityKind.AttDef,
        DwgEntityType.MText => EntityKind.MText,
        DwgEntityType.Ellipse => EntityKind.Ellipse,
        DwgEntityType.Spline => EntityKind.Spline,
        DwgEntityType.Leader => EntityKind.Leader,
        DwgEntityType.MLeader => EntityKind.MLeader,
        DwgEntityType.Hatch => EntityKind.Hatch,
        DwgEntityType.Dimension => EntityKind.Dimension,
        DwgEntityType.Viewport => EntityKind.Viewport,
        DwgEntityType.Table => EntityKind.Table,
        DwgEntityType.MLine => EntityKind.MLine,
        DwgEntityType.Tolerance => EntityKind.Tolerance,
        DwgEntityType.Ray => EntityKind.Ray,
        DwgEntityType.XLine => EntityKind.XLine,
        DwgEntityType.SeqEnd => EntityKind.SeqEnd,
        _ => EntityKind.Unknown,
    };


    public string ToSvg(RenderContext ctx, Matrix transform)
        => EntityRenderRegistry.TryRenderSvg(this, ctx, transform) ?? WriteSvg(ctx, transform);

    protected virtual string WriteSvg(RenderContext ctx, Matrix m) => "";

    protected string RenderArc(RenderContext ctx, Matrix m, string strokeColor, bool dashed = false)
    {
        var (cx, cy) = m.Apply(Center.X, Center.Y);
        double r = Radius;
        double a1 = StartAngle, a2 = EndAngle;
        if (ExtrusionZ < 0) { a1 = -a1; a2 = -a2; }
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

    protected string RenderEllipse(RenderContext ctx, Matrix m, string strokeColor)
    {

        var (cx, cy) = m.Apply(Center.X, Center.Y);
        double semiMajor = Radius;
        double ratio = WidthFactor > 0 ? WidthFactor : 1.0;
        double semiMinor = semiMajor * ratio;

        double rotation = Math.Atan2(Start.Y, Start.X);
        double rotDeg = rotation * 180 / Math.PI;
        bool mirror = ExtrusionZ < 0;

        double a1 = StartAngle;
        double a2 = EndAngle;
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

    protected string RenderPolyline(RenderContext ctx, Matrix m, string strokeColor)
    {
        int n = Points.Count;
        if (n == 0) return "";
        var d = new System.Text.StringBuilder();
        for (int i = 0; i < n; i++)
        {
            var (x, y) = m.Apply(Points[i].X, Points[i].Y);
            ctx.Expand(x, y);
            if (i == 0)
                d.Append($"M {RenderContext.F(x)} {RenderContext.F(y)}");
            else
            {
                double bulge = i - 1 < Bulges.Count ? Bulges[i - 1] : 0.0;
                if (Math.Abs(bulge) < 1e-6)
                    d.Append($" L {RenderContext.F(x)} {RenderContext.F(y)}");
                else
                {
                    var (px, py) = m.Apply(Points[i - 1].X, Points[i - 1].Y);
                    d.Append(BulgeArc(px, py, x, y, bulge));
                }
            }
        }
        if (Closed && n > 1)
        {
            var (x0, y0) = m.Apply(Points[0].X, Points[0].Y);
            var (x1, y1) = m.Apply(Points[n - 1].X, Points[n - 1].Y);
            double closeBulge = n - 1 < Bulges.Count ? Bulges[n - 1] : 0.0;
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

    protected string RenderInsert(RenderContext ctx, Matrix m, int depth)
    {
        if (depth > RenderContext.MaxDepth) return "";
        if (!ctx.BlockContents.TryGetValue(BlockHeaderHandle, out var contents))
            return "";
        var t = m * InsertPlacement();
        var sb = new System.Text.StringBuilder();
        foreach (var sub in contents.Cast<DwgEntity>())
            sb.Append(sub.ToSvg(ctx, t));
        return sb.ToString();
    }

    protected string RenderDimension(RenderContext ctx, Matrix m, string strokeColor, string fillColor)
    {
        var sb = new System.Text.StringBuilder();

        var blockDisplay = ctx.BlockContents.TryGetValue(DimBlockHandle, out var bd) ? bd : null;
        var shape = Render.DimensionRenderer.Plan(this, ctx.ResolveDimStyle(DimStyleHandle), blockDisplay);
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
                double w = len * Render.DimensionRenderer.ArrowWidthRatio;
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
                double rot = DimTextRotation != 0 ? DimTextRotation : shape.TextRotation;
                double th = shape.TextHeight > 0 ? shape.TextHeight : Height;
                sb.Append(RenderTextAt(ctx, m, shape.TextPos.X, shape.TextPos.Y, rot, th,
                    DimensionSvgText(shape.Text), fillColor, TextStyleHandle));
            }
            return sb.ToString();
        }

        if (Points.Count >= 2)
        {
            var d = new System.Text.StringBuilder();
            for (int i = 0; i < Points.Count; i++)
            {
                var (x, y) = m.Apply(Points[i].X, Points[i].Y);
                ctx.Expand(x, y);
                if (i == 0)
                    d.Append($"M {RenderContext.F(x)} {RenderContext.F(y)}");
                else
                    d.Append($" L {RenderContext.F(x)} {RenderContext.F(y)}");
            }
            sb.Append($"    <path d=\"{d}\" stroke=\"{strokeColor}\" stroke-width=\"1\" fill=\"none\"/>\n");
        }

        string dimText = TextValue is { Length: > 0 }
            ? TextValue.Replace("<>", Render.DimensionRenderer.FormatMeasurement(Measurement))
            : Render.DimensionRenderer.FormatMeasurement(Measurement);
        if (dimText.Length > 0)
        {
            double rot = DimTextRotation != 0 ? DimTextRotation : Rotation;
            sb.Append(RenderTextAt(ctx, m, Start.X, Start.Y, rot, Height, dimText, fillColor, TextStyleHandle));
        }

        return sb.ToString();
    }

    protected string RenderText(RenderContext ctx, Matrix m, string fillColor)
    {
        if (string.IsNullOrEmpty(TextValue)) return "";
        var (x, y) = m.Apply(Start.X, Start.Y);
        ctx.Expand(x, y);
        return RenderTextAt(ctx, m, Start.X, Start.Y, Rotation, Height, TextValue, fillColor, TextStyleHandle);
    }

    private static string DimensionSvgText(string raw)
    {
        var lines = Render.MTextParser.Parse(raw);
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

    protected static string RenderTextAt(RenderContext ctx, Matrix m,
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
        double linePitch = Render.ShxTextShapes.LinePitch(h, 1.0);
        double fontSize = h * Render.ShxTextShapes.EmFactor;
        for (int i = 0; i < lines.Length; i++)
        {
            double dy = i * linePitch;
            sb.Append($"<text x=\"0\" y=\"{RenderContext.F(dy)}\" font-size=\"{RenderContext.F(fontSize)}\" font-family=\"{fontFamily}\" fill=\"{fillColor}\">{lines[i]}</text>");
        }
        sb.Append("</g>\n");
        return sb.ToString();
    }

    protected string RenderPointSequence(RenderContext ctx, Matrix m, string strokeColor)
    {
        if (Points.Count < 2) return "";
        var d = new System.Text.StringBuilder();
        for (int i = 0; i < Points.Count; i++)
        {
            var (x, y) = m.Apply(Points[i].X, Points[i].Y);
            ctx.Expand(x, y);
            d.Append(i == 0
                ? $"M {RenderContext.F(x)} {RenderContext.F(y)}"
                : $" L {RenderContext.F(x)} {RenderContext.F(y)}");
        }
        return $"    <path d=\"{d}\" stroke=\"{strokeColor}\" stroke-width=\"1\" vector-effect=\"non-scaling-stroke\" fill=\"none\"/>\n";
    }

    protected string RenderHatch(RenderContext ctx, Matrix m, string strokeColor, string fillColor)
    {
        if (Points.Count < 4) return "";
        var d = new System.Text.StringBuilder();
        var segments = Points.Count / 2;
        var drawn = new List<Vec2>();
        for (int s = 0; s < segments; s++)
        {
            var p1 = Points[s * 2];
            var p2 = Points[s * 2 + 1];
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
        string fill = closed && SolidFill
            ? $" fill=\"{fillColor}\" fill-opacity=\"{RenderContext.F(HatchGeometry.SolidFillOpacity)}\""
            : " fill=\"none\"";
        return $"    <path d=\"{d}\" stroke=\"{strokeColor}\" stroke-width=\"1\"{fill}/>\n";
    }

    private static bool SamePt(Vec2 a, Vec2 b)
        => Math.Abs(a.X - b.X) < 1e-9 && Math.Abs(a.Y - b.Y) < 1e-9;
}