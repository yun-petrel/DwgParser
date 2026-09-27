using DwgParser.Core.IO;
using DwgParser.Core.Render;

namespace DwgParser.Core.Objects;

public readonly record struct MLeaderColor(short Index, uint Rgb, string Name, string BookName);

public sealed class MLeaderLine
{
    public List<(double X, double Y, double Z)> Points3D { get; } = new();

    public long LineIndex { get; set; }

    public long Type { get; set; }

    public long LineWeight { get; set; }

    public double ArrowSize { get; set; }

    public bool HasArrowSize { get; set; }

    public double ArrowSizeOr(double entityLevel)
    {
        if (!HasArrowSize) return entityLevel;
        if (double.IsFinite(ArrowSize) && ArrowSize > 0) return ArrowSize;
        return entityLevel;
    }

    public long Flags { get; set; }

    public MLeaderColor? Color { get; internal set; }
}

public sealed class MLeaderNode
{
    public bool HasLastLeaderLinePoint { get; set; }

    public bool HasDogleg { get; set; }

    public (double X, double Y, double Z) LastLeaderLinePoint { get; set; }

    public (double X, double Y, double Z) DoglegVector { get; set; }

    public long BranchIndex { get; set; }

    public double DoglegLength { get; set; }

    public long AttachDir { get; set; }

    public List<MLeaderLine> Lines { get; } = new();
}

public abstract class MLeaderEntity : DwgEntity, IMLeader
{
    public int VertexCount => Points.Count;

    public long ClassVersion { get; protected set; }

    public bool Captured { get; protected set; }

    public List<MLeaderNode> Leaders { get; } = new();

    public int LeaderCount => Leaders.Count;

    public double ContextScale { get; protected set; } = 1.0;

    public (double X, double Y, double Z) ContentBase { get; protected set; }

    public double MTextHeight { get; protected set; }

    public double CtxArrowSize { get; protected set; }

    public double LandingGap { get; protected set; }

    public MLeaderColor? ContextTextColor { get; protected set; }
    public MLeaderColor? ContextBackgroundColor { get; protected set; }
    public MLeaderColor? ContextBlockColor { get; protected set; }

    public long CtxTextLeft { get; protected set; }
    public long CtxTextRight { get; protected set; }
    public long CtxTextAngleType { get; protected set; }
    public long CtxTextAlignment { get; protected set; }

    public long CtxTextTop { get; protected set; }
    public long CtxTextBottom { get; protected set; }

    public bool HasTextContent { get; protected set; }

    public bool HasBlockContent { get; protected set; }

    public string ContentType => HasTextContent ? "MText" : HasBlockContent ? "Block" : "None";

    public string ContentText { get; protected set; } = "";

    public (double X, double Y, double Z) ContentLocation { get; protected set; }
    public (double X, double Y, double Z) ContentDirection { get; protected set; }
    public double ContentRotation { get; protected set; }
    public double ContentWidth { get; protected set; }
    public double ContentHeight { get; protected set; }

    public double MTextLineSpacingFactor { get; protected set; }
    public long MTextLineSpacingStyle { get; protected set; }
    public long MTextAlignment { get; protected set; }
    public long MTextFlow { get; protected set; }
    public double MTextBgScale { get; protected set; }
    public long MTextBgTransparency { get; protected set; }
    public bool MTextIsBgFill { get; protected set; }
    public bool MTextIsBgMaskFill { get; protected set; }
    public long MTextColType { get; protected set; }
    public bool MTextIsHeightAuto { get; protected set; }
    public double MTextColWidth { get; protected set; }
    public double MTextColGutter { get; protected set; }
    public bool MTextIsColFlowReversed { get; protected set; }
    public bool MTextWordBreak { get; protected set; }

    public bool MTextUnknownTailBit { get; protected set; }
    public System.Collections.Generic.List<double> MTextColSizes { get; } = new();
    public bool IsNormalReversed { get; protected set; }
    public long TopLineWeight { get; protected set; }
    public MLeaderColor? TopLineColor { get; protected set; }
    public MLeaderColor? TopTextColor { get; protected set; }
    public MLeaderColor? TopBlockColor { get; protected set; }
    public System.Collections.Generic.List<(string Text, long UiIndex, double Width)> BlockLabels { get; } = new();
    public System.Collections.Generic.List<bool> ArrowheadDefaults { get; } = new();
    public bool IsNegTextDir { get; protected set; }

    public (double X, double Y, double Z) Base { get; protected set; }
    public (double X, double Y, double Z) BaseDir { get; protected set; }
    public (double X, double Y, double Z) BaseVert { get; protected set; }

    public long OverrideFlags { get; protected set; }

    public long TypeBS { get; protected set; }

    public bool HasLanding { get; protected set; }
    public bool HasDogleg { get; protected set; }

    public double DoglegLength { get; protected set; }

    public double TopArrowSize { get; protected set; }

    public long StyleContent { get; protected set; }

    public long TextLeft { get; protected set; }
    public long TextRight { get; protected set; }
    public long TextAngleType { get; protected set; }
    public long TextAlignment { get; protected set; }

    public double BlockRotation { get; protected set; }
    public (double X, double Y, double Z) BlockScale { get; protected set; } = (1.0, 1.0, 1.0);

    public long StyleAttachment { get; protected set; }

    public bool IsAnnotative { get; protected set; }

    public bool HasTextFrame { get; protected set; }

    public double ScaleFactor { get; protected set; } = 1.0;

    public long IpeAlignment { get; protected set; }
    public long Justification { get; protected set; }

    public int ArrowheadCount { get; protected set; }

    public int BlockLabelCount { get; protected set; }

    public long AttachDir { get; protected set; }
    public long AttachTop { get; protected set; }
    public long AttachBottom { get; protected set; }

    public bool IsTextExtended { get; protected set; }

    public bool IsR2013Plus { get; internal set; }

    public ulong MLeaderStyleHandle { get; internal set; }

    public ulong ContentStyleHandle { get; internal set; }

    public abstract void ReadBody(BitReader br, DwgObjectRecord? record);

    protected void FallbackToKindOnly()
    {
        Captured = false;
        Leaders.Clear();
        Points.Clear();
        ContextTextColor = null;
        ContextBackgroundColor = null;
        ContextBlockColor = null;
        TopLineColor = null;
        TopTextColor = null;
        TopBlockColor = null;
        ContentText = "";
        TextValue = "";
    }


    protected override string WriteSvg(RenderContext ctx, Matrix m)
    {
        string stroke = ctx.EntityColor(this);
        var sb = new System.Text.StringBuilder();

        bool anyLine = false;
        double? styleArrow = ctx.ResolveMLeaderStyle(MLeaderStyleHandle)?.ArrowHeadSize;
        foreach (var node in Leaders)
        {
            foreach (var line in node.Lines)
            {
                double arrowSize = ResolveArrowSize(line, styleArrow);
                if (AppendPolyline(ctx, m, sb, stroke, line.Points3D, arrowSize)) anyLine = true;
                else if (line.Points3D.Count == 1)
                {
                    var p0 = line.Points3D[0];
                    double nx = p0.X - node.DoglegVector.X, ny = p0.Y - node.DoglegVector.Y;
                    if (node.HasLastLeaderLinePoint)
                    {
                        var lp = node.LastLeaderLinePoint;
                        nx = lp.X; ny = lp.Y;
                        sb.Append(Line2D(ctx, m, stroke, p0.X, p0.Y, lp.X, lp.Y));
                    }
                    sb.Append(ArrowTriangle(ctx, m, stroke, p0.X, p0.Y, nx, ny, arrowSize));
                    anyLine = true;
                }
            }
            if (node.HasLastLeaderLinePoint && node.HasDogleg)
            {
                double len = FiniteOrZero(node.DoglegLength) > 0 ? node.DoglegLength : DoglegLength;
                if (Finite(len) && len > 1e-9
                    && AppendDogleg(ctx, m, sb, stroke, node.LastLeaderLinePoint, node.DoglegVector, len))
                    anyLine = true;
            }
        }
        if (!anyLine && Points.Count >= 2)
        {
            var flat = new System.Collections.Generic.List<(double X, double Y, double Z)>(Points.Count);
            foreach (var p in Points) flat.Add((p.X, p.Y, 0));
            AppendPolyline(ctx, m, sb, stroke, flat, ArrowSize());
        }

        string text = RenderContext.SanitizeText(TextValue);
        if (!string.IsNullOrWhiteSpace(text) && Finite(MTextHeight) && MTextHeight > 0)
        {
            double wx = ContentLocation.X, wy = ContentLocation.Y;
            if (!Finite(wx) || !Finite(wy) || (wx == 0 && wy == 0)) { wx = Base.X; wy = Base.Y; }
            if (!Finite(wx) || !Finite(wy)) return sb.ToString();
            sb.Append(RenderTextAt(ctx, m, wx, wy, ContentRotation, MTextHeight, TextValue, stroke));
        }
        return sb.ToString();
    }

    private bool AppendPolyline(RenderContext ctx, Matrix m, System.Text.StringBuilder sb, string stroke,
        System.Collections.Generic.IReadOnlyList<(double X, double Y, double Z)> pts, double arrowSize)
    {
        if (pts.Count < 2) return false;
        var d = new System.Text.StringBuilder();
        for (int i = 0; i < pts.Count; i++)
        {
            var (x, y) = m.Apply(pts[i].X, pts[i].Y);
            ctx.Expand(x, y);
            d.Append(i == 0
                ? $"M {RenderContext.F(x)} {RenderContext.F(y)}"
                : $" L {RenderContext.F(x)} {RenderContext.F(y)}");
        }
        sb.Append($"    <path d=\"{d}\" stroke=\"{stroke}\" stroke-width=\"1\" vector-effect=\"non-scaling-stroke\" fill=\"none\"/>\n");
        sb.Append(ArrowTriangle(ctx, m, stroke, pts[1].X, pts[1].Y, pts[0].X, pts[0].Y, arrowSize));
        return true;
    }

    private bool AppendDogleg(RenderContext ctx, Matrix m, System.Text.StringBuilder sb, string stroke,
        (double X, double Y, double Z) from, (double X, double Y, double Z) dir, double len)
    {
        if (!Finite(len) || len <= 0) return false;
        double dx = dir.X, dy = dir.Y;
        double n = Math.Sqrt(dx * dx + dy * dy);
        if (n < 1e-9) { dx = 1; dy = 0; } else { dx /= n; dy /= n; }
        var (x1, y1) = m.Apply(from.X, from.Y);
        var (x2, y2) = m.Apply(from.X + dx * len, from.Y + dy * len);
        ctx.Expand(x1, y1); ctx.Expand(x2, y2);
        sb.Append($"    <path d=\"M {RenderContext.F(x1)} {RenderContext.F(y1)} L {RenderContext.F(x2)} {RenderContext.F(y2)}\" stroke=\"{stroke}\" stroke-width=\"1\" vector-effect=\"non-scaling-stroke\" fill=\"none\"/>\n");
        return true;
    }

    private static string ArrowTriangle(RenderContext ctx, Matrix m, string fill,
        double tipWx, double tipWy, double nxWx, double nxWy, double size)
    {
        if (!Finite(size) || size <= 0) return "";
        double vx = tipWx - nxWx, vy = tipWy - nxWy;
        double n = Math.Sqrt(vx * vx + vy * vy);
        if (n < 1e-9) return "";
        double w = size * DimensionRenderer.ArrowWidthRatio / 2;
        double bx = tipWx - vx / n * size, by = tipWy - vy / n * size;
        double px = -vy / n * w, py = vx / n * w;
        var (tx, ty) = m.Apply(tipWx, tipWy);
        var (ax, ay) = m.Apply(bx + px, by + py);
        var (cx, cy) = m.Apply(bx - px, by - py);
        ctx.Expand(tx, ty); ctx.Expand(ax, ay); ctx.Expand(cx, cy);
        return $"    <path d=\"M {RenderContext.F(tx)} {RenderContext.F(ty)} L {RenderContext.F(ax)} {RenderContext.F(ay)} L {RenderContext.F(cx)} {RenderContext.F(cy)} Z\" fill=\"{fill}\"/>\n";
    }

    private double ArrowSize()
    {
        double a = Finite(CtxArrowSize) ? CtxArrowSize : 0;
        double b = Finite(TopArrowSize) ? TopArrowSize : 0;
        return Math.Max(a, b);
    }

    private const long LeaderFlagArrowheadSize = 0x10;

    private const long EntityFlagArrowheadSize = 0x200;

    internal double ResolveArrowSize(MLeaderLine line, double? styleArrowHeadSize)
    {
        if (line.HasArrowSize && (line.Flags & LeaderFlagArrowheadSize) != 0
            && Finite(line.ArrowSize) && line.ArrowSize > 0)
            return line.ArrowSize;
        if ((OverrideFlags & EntityFlagArrowheadSize) != 0 && Finite(CtxArrowSize) && CtxArrowSize > 0)
            return CtxArrowSize;
        double ent = ArrowSize();
        if (ent > 0) return ent;
        if (styleArrowHeadSize is { } s && Finite(s) && s > 0) return s;
        return 0;
    }

    private static bool Finite(double v) => !double.IsNaN(v) && !double.IsInfinity(v);

    private static double FiniteOrZero(double v) => Finite(v) ? v : 0;

    private static string Line2D(RenderContext ctx, Matrix m, string stroke,
        double x0, double y0, double x1, double y1)
    {
        var (ax, ay) = m.Apply(x0, y0);
        var (bx, by) = m.Apply(x1, y1);
        ctx.Expand(ax, ay); ctx.Expand(bx, by);
        return $"    <path d=\"M {RenderContext.F(ax)} {RenderContext.F(ay)} L {RenderContext.F(bx)} {RenderContext.F(by)}\" stroke=\"{stroke}\" stroke-width=\"1\" vector-effect=\"non-scaling-stroke\" fill=\"none\"/>\n";
    }


    private MLeaderColor ReadEntColor(BitReader br, bool cmc)
    {
        if (cmc)
        {
            short index = DwgObjectReader.ReadCmc(
                br,
                out uint rgb,
                out string name,
                out string bookName);
            return new MLeaderColor(index, rgb, name, bookName);
        }

        ShortCmcSink?.Add(br.BitPosition);
        return new MLeaderColor(br.ReadBS(), 0, "", "");
    }

    protected List<long>? ShortCmcSink;

    protected void ReadLeaders(BitReader br, bool r2010Plus)
    {
        long numLeaders = br.ReadBL();
        if (numLeaders is < 0 or > 5000) throw new System.IO.EndOfStreamException();
        for (int i = 0; i < numLeaders; i++)
        {
            var node = new MLeaderNode();
            node.HasLastLeaderLinePoint = br.ReadB();
            node.HasDogleg = br.ReadB();
            if (node.HasLastLeaderLinePoint) node.LastLeaderLinePoint = br.Read3BD();
            if (node.HasDogleg) node.DoglegVector = br.Read3BD();
            long numBreaks = br.ReadBL();
            if (numBreaks is < 0 or > 5000) throw new System.IO.EndOfStreamException();
            for (int k = 0; k < numBreaks; k++) { br.Read3BD(); br.Read3BD(); }
            node.BranchIndex = br.ReadBL();
            node.DoglegLength = br.ReadBD();
            long numLines = br.ReadBL();
            if (numLines is < 0 or > 5000) throw new System.IO.EndOfStreamException();
            for (int l = 0; l < numLines; l++)
            {
                var line = new MLeaderLine();
                long numPoints = br.ReadBL();
                if (numPoints is < 0 or > 100000) throw new System.IO.EndOfStreamException();
                for (int p = 0; p < numPoints; p++)
                {
                    var pt = br.Read3BD();
                    line.Points3D.Add(pt);
                    Points.Add(new Vec2(pt.X, pt.Y));
                }
                long lineBreaks = br.ReadBL();
                if (lineBreaks is < 0 or > 5000) throw new System.IO.EndOfStreamException();
                for (int k = 0; k < lineBreaks; k++) { br.Read3BD(); br.Read3BD(); }
                line.LineIndex = br.ReadBL();
                if (r2010Plus)
                {
                    line.Type = br.ReadBS();
                    line.Color = ReadEntColor(br, cmc: true);
                    line.LineWeight = br.ReadBL();
                    line.ArrowSize = br.ReadBD();
                    line.HasArrowSize = true;
                    line.Flags = br.ReadBL();
                }
                node.Lines.Add(line);
            }
            if (r2010Plus) node.AttachDir = br.ReadBS();
            Leaders.Add(node);
        }
    }

    protected void ReadContext(BitReader br, bool cmc)
    {
        ContextScale = br.ReadBD();
        ContentBase = br.Read3BD();
        MTextHeight = br.ReadBD();
        CtxArrowSize = br.ReadBD();
        LandingGap = br.ReadBD();
        CtxTextLeft = br.ReadBS();
        CtxTextRight = br.ReadBS();
        CtxTextAngleType = br.ReadBS();
        CtxTextAlignment = br.ReadBS();

        HasTextContent = br.ReadB();
        if (HasTextContent)
        {
            ContentText = br.ReadTV();
            TextValue = ContentText;
            br.Read3BD();
            ContentStyleHandle = 0;
            ContentLocation = br.Read3BD();
            ContentDirection = br.Read3BD();
            ContentRotation = br.ReadBD();
            ContentWidth = br.ReadBD();
            ContentHeight = br.ReadBD();
            MTextLineSpacingFactor = br.ReadBD();
            MTextLineSpacingStyle = br.ReadBS();
            ContextTextColor = ReadEntColor(br, cmc);
            MTextAlignment = br.ReadBS();
            MTextFlow = br.ReadBS();
            ContextBackgroundColor = ReadEntColor(br, cmc);
            MTextBgScale = br.ReadBD();
            MTextBgTransparency = br.ReadBL();
            MTextIsBgFill = br.ReadB();
            MTextIsBgMaskFill = br.ReadB();
            MTextColType = br.ReadBS();
            MTextIsHeightAuto = br.ReadB();
            MTextColWidth = br.ReadBD();
            MTextColGutter = br.ReadBD();
            MTextIsColFlowReversed = br.ReadB();
            long numColSizes = br.ReadBL();
            if (numColSizes is < 0 or > 100000) throw new System.IO.EndOfStreamException();
            for (int i = 0; i < numColSizes; i++) MTextColSizes.Add(br.ReadBD());
            MTextWordBreak = br.ReadB();
            MTextUnknownTailBit = br.ReadB();
        }
        else
        {
            HasBlockContent = br.ReadB();
            if (HasBlockContent)
            {
                br.Read3BD();
                br.Read3BD();
                BlockScale = br.Read3BD();
                BlockRotation = br.ReadBD();
                ContextBlockColor = ReadEntColor(br, cmc);
                for (int i = 0; i < 16; i++) br.ReadBD();
            }
        }

        Base = br.Read3BD();
        BaseDir = br.Read3BD();
        BaseVert = br.Read3BD();
        IsNormalReversed = br.ReadB();
    }

    protected void ReadTopCommon(BitReader br, bool cmc)
    {
        OverrideFlags = br.ReadBL();
        TypeBS = br.ReadBS();
        TopLineColor = ReadEntColor(br, cmc);
        TopLineWeight = br.ReadBL();
        HasLanding = br.ReadB();
        HasDogleg = br.ReadB();
        DoglegLength = br.ReadBD();
        TopArrowSize = br.ReadBD();
        StyleContent = br.ReadBS();
        TextLeft = br.ReadBS();
        TextRight = br.ReadBS();
        TextAngleType = br.ReadBS();
        TextAlignment = br.ReadBS();
        TopTextColor = ReadEntColor(br, cmc);
        HasTextFrame = br.ReadB();
        TopBlockColor = ReadEntColor(br, cmc);
        BlockScale = br.Read3BD();
        BlockRotation = br.ReadBD();
        StyleAttachment = br.ReadBS();
        IsAnnotative = br.ReadB();
    }

    protected void ReadCommonTail(BitReader br)
    {
        long numBlockLabels = br.ReadBL();
        if (numBlockLabels is < 0 or > 5000) throw new System.IO.EndOfStreamException();
        for (int i = 0; i < numBlockLabels; i++)
        {
            string labelText = br.ReadTV();
            long uiIndex = br.ReadBS();
            double labelWidth = br.ReadBD();
            BlockLabels.Add((labelText, uiIndex, labelWidth));
        }
        BlockLabelCount = (int)numBlockLabels;
        IsNegTextDir = br.ReadB();
        IpeAlignment = br.ReadBS();
        Justification = br.ReadBS();
        ScaleFactor = br.ReadBD();
    }
}
