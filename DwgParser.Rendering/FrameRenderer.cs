using DwgParser.Core.Objects;

using DwgParser.Core.Render;
namespace DwgParser.Rendering;

public sealed partial class FrameRenderer
{
    private readonly ITextRendererBackend _text;

    public FrameRenderer() : this(TextBackends.Current) { }

    public FrameRenderer(ITextRendererBackend text)
    {
        _text = text;
    }

    public ITextRendererBackend TextBackend => _text;

    public (double MinX, double MinY, double MaxX, double MaxY)? FrameBounds { get; private set; }

    public bool UseFrameBounds { get; set; } = true;

    public bool DarkBackground { get; set; }

    private RenderContext? _ctx;

    internal DrawViewport LastPassViewport { get; private set; }

    private Dictionary<ulong, short> _layerColors = new();

    private DwgObjectReader.WalkResult _walk = null!;

    private Dictionary<ulong, List<DwgEntity>> _blockContents = new();

    public PixelBuffer Render(DwgObjectReader.WalkResult walk,
        int width = 2048, int height = 2048, int lineThickness = 2)
    {
        var buffer = new PixelBuffer(width, height);
        _layerColors = walk.LayerColors;
        _walk = walk;
        _ctx = new RenderContext(walk) { DarkBackground = DarkBackground };
        RenderInk.DarkBackground = DarkBackground;
        if (DarkBackground) buffer.Clear(0, 0, 0);
        _blockContents = BuildBlockContents(walk);

        var allEntities = CollectAllEntities(walk);

        FrameBounds = DetectBlueFrame(allEntities)
            ?? (allEntities.Count == 0 ? DetectBlueFrame(walk.Entities) : null);

        double minX, minY, maxX, maxY;
        if (UseFrameBounds && FrameBounds.HasValue)
        {
            (minX, minY, maxX, maxY) = FrameBounds.Value;
        }
        else
        {
            ComputeBounds(allEntities, out minX, out minY, out maxX, out maxY, _blockContents);
        }

        var view = new DrawViewport(minX, minY, maxX, maxY, width, height, ViewportMarginPx(width, height), false);
        LastPassViewport = view;

        if (IsBlankBounds(minX, minY, maxX, maxY)) return buffer;

        int thick = Math.Max(1, lineThickness);
        var device = NewDevice(buffer, view, thick);
        foreach (var e in allEntities)
            RenderEntity(e, null, thick, buffer, _layerColors, device);

        return buffer;
    }

    internal static List<DwgEntity> CollectAllEntities(DwgObjectReader.WalkResult walk)
    {
        var result = new List<DwgEntity>();
        var blockContents = BuildBlockContents(walk);

        ulong ms = walk.ModelSpaceHandle;
        foreach (var e in walk.Entities)
        {
            if (e is not DwgEntity dwg) continue;
            bool isModel = dwg.OwnerHandle == ms || dwg.EntMode == 2;
            if (dwg.EntMode == 1) isModel = false;
            if (!isModel && dwg.OwnerHandle != 0 && !blockContents.ContainsKey(dwg.OwnerHandle))
                isModel = true;
            if (isModel && !walk.Layers.IsOff(dwg.LayerHandle))
                ExpandAndTransform(dwg, blockContents, Matrix.Identity, result, 0, walk.Layers);
        }
        return result;
    }

    private static Dictionary<ulong, List<DwgEntity>> BuildBlockContents(DwgObjectReader.WalkResult walk)
    {
        var dict = new Dictionary<ulong, List<DwgEntity>>();
        var contentOwners = BlockOwnership.ContentOwners(walk);
        foreach (var e in walk.Entities)
        {
            if (e is not DwgEntity dwg || dwg.OwnerHandle == 0) continue;
            if (!contentOwners.Contains(dwg.OwnerHandle)) continue;
            if (!dict.TryGetValue(dwg.OwnerHandle, out var list))
                dict[dwg.OwnerHandle] = list = new();
            list.Add(dwg);
        }
        return dict;
    }

    private static void ExpandAndTransform(DwgEntity e,
        Dictionary<ulong, List<DwgEntity>> blockContents, Matrix m,
        List<DwgEntity> outList, int depth,
        DwgParser.Core.Objects.Records.LayerTable? layers = null)
    {
        if (depth > RenderContext.MaxDepth) return;
        if (layers is not null && layers.IsOff(e.LayerHandle)) return;
        if (e.Kind == DwgEntityType.Insert)
        {
            if (!blockContents.TryGetValue(e.BlockHeaderHandle, out var contents)) return;
            var t = m * e.InsertPlacement();
            foreach (var sub in contents)
                ExpandAndTransform(sub, blockContents, t, outList, depth + 1, layers);
            return;
        }
        var transformed = TransformGeometry(e, m);
        if (transformed != null)
            outList.Add(transformed);
    }

    private static DwgEntity? TransformGeometry(DwgEntity e, Matrix m)
    {
        if (m == Matrix.Identity) return e;
        var t = new DwgEntity
        {
            Kind = e.Kind,
            Handle = e.Handle,
            ColorIndex = e.ColorIndex,
            LayerHandle = e.LayerHandle,
            TextValue = e.TextValue,
            Height = e.Height,
            Rotation = e.Rotation,
            ExtrusionZ = e.ExtrusionZ,
            Thickness = e.Thickness,
            Elevation = e.Elevation,
            TextStyleHandle = e.TextStyleHandle,
            WidthFactor = e.WidthFactor,
            TextAttachment = e.TextAttachment,
            RectWidth = e.RectWidth,
            RectHeight = e.RectHeight,
            LineSpacingStyle = e.LineSpacingStyle,
            LineSpacingFactor = e.LineSpacingFactor,
            HorizMode = e.HorizMode,
            VertMode = e.VertMode,
            ObliqueAngle = e.ObliqueAngle,
            TextGenerationFlags = e.TextGenerationFlags,
            Dir = e.Dir,
            ArrowheadOn = e.ArrowheadOn,
            ArrowheadType = e.ArrowheadType,
        };
        switch (e.Kind)
        {
            case DwgEntityType.Line:
            case DwgEntityType.Ray:
            case DwgEntityType.XLine:
                var (sx, sy) = m.Apply(e.Start.X, e.Start.Y);
                var (ex, ey) = m.Apply(e.End.X, e.End.Y);
                t.Start = new Vec2(sx, sy);
                t.End = new Vec2(ex, ey);
                return t;
            case DwgEntityType.Point:
                var (px, py) = m.Apply(e.Start.X, e.Start.Y);
                t.Start = new Vec2(px, py);
                return t;
            case DwgEntityType.Text:
            case DwgEntityType.MText:
            case DwgEntityType.AttDef:
            case DwgEntityType.Attrib:
            {
                var (tx, ty) = m.Apply(e.Start.X, e.Start.Y);
                t.Start = new Vec2(tx, ty);
                t.AlignmentPoint = new Vec2(
                    m.Apply(e.AlignmentPoint.X, e.AlignmentPoint.Y).X,
                    m.Apply(e.AlignmentPoint.X, e.AlignmentPoint.Y).Y);
                double rot = Math.Atan2(m.M12, m.M11);
                t.Rotation = e.Rotation + rot;
                t.Height = e.Height * m.YAxisScale;
                return t;
            }
            case DwgEntityType.Circle:
            case DwgEntityType.Arc:
                var (cx, cy) = m.Apply(e.Center.X, e.Center.Y);
                t.Center = new Vec2(cx, cy);
                t.Radius = e.Radius * DwgParser.Core.Render.CurveFamily.AxisScale(m);
                t.StartAngle = e.StartAngle;
                t.EndAngle = e.EndAngle;
                return t;
            case DwgEntityType.Ellipse:
            {
                var (ecx, ecy) = m.Apply(e.Center.X, e.Center.Y);
                var (ax, ay) = m.Apply(e.Center.X + e.Start.X, e.Center.Y + e.Start.Y);
                t.Center = new Vec2(ecx, ecy);
                t.Start = new Vec2(ax - ecx, ay - ecy);
                t.Radius = Math.Sqrt(t.Start.X * t.Start.X + t.Start.Y * t.Start.Y);
                t.WidthFactor = e.WidthFactor;
                t.StartAngle = e.StartAngle;
                t.EndAngle = e.EndAngle;
                return t;
            }
            case DwgEntityType.Dimension:
                t.SolidFill = e.SolidFill;
                t.Closed = e.Closed;
                t.ConstantWidth = e.ConstantWidth;
                t.Measurement = e.Measurement;
                t.DimSubtype = e.DimSubtype;
                t.DimTextRotation = e.DimTextRotation;
                t.DimLineRotation = e.DimLineRotation;
                t.DimTextMid = new Vec2(m.Apply(e.DimTextMid.X, e.DimTextMid.Y).X,
                                        m.Apply(e.DimTextMid.X, e.DimTextMid.Y).Y);
                bool mirrored = m.M11 * m.M22 - m.M12 * m.M21 < 0;
                foreach (var b in e.Bulges)
                    t.Bulges.Add(mirrored ? -b : b);
                foreach (var p in e.Points)
                {
                    var (qpx, qpy) = m.Apply(p.X, p.Y);
                    t.Points.Add(new Vec2(qpx, qpy));
                }
                return t;
            case DwgEntityType.Solid:
            case DwgEntityType.Trace:
                foreach (var c in e.Corners)
                {
                    var (scx, scy) = m.Apply(c.X, c.Y);
                    t.Corners.Add(new Vec2(scx, scy));
                }
                return t;
            case DwgEntityType.Table:
            {
                if (e is not TableEntity te) return null;
                var ct = EntityFactory.CreateTable(te.Handle, r2010Plus: false);
                ct.Kind = DwgEntityType.Table;
                ct.Handle = te.Handle;
                ct.ColorIndex = te.ColorIndex;
                ct.LayerHandle = te.LayerHandle;
                ct.TextStyleHandle = te.TextStyleHandle;
                ct.OwnerHandle = te.OwnerHandle;
                ct.EntMode = te.EntMode;
                ct.LtypeName = te.LtypeName;
                ct.TableFlag = te.TableFlag;
                ct.HorizDirection = te.HorizDirection;
                ct.TableColumns = te.TableColumns;
                ct.TableRows = te.TableRows;
                ct.ColWidths.AddRange(te.ColWidths);
                ct.RowHeights.AddRange(te.RowHeights);
                ct.Cells.AddRange(te.Cells);
                ct.StyleOverride = te.StyleOverride;
                ct.BorderOverrides.AddRange(te.BorderOverrides);
                ct.TableName = te.TableName;
                ct.Description = te.Description;
                ct.ColumnNames.AddRange(te.ColumnNames);
                ct.ColumnStyles.AddRange(te.ColumnStyles);
                ct.RowStyles.AddRange(te.RowStyles);
                ct.BaseCellStyle = te.BaseCellStyle;
                ct.MergedCells.AddRange(te.MergedCells);

                var (ix, iy) = m.Apply(te.Start.X, te.Start.Y);
                ct.Start = new Vec2(ix, iy);
                ct.Rotation = te.Rotation + Math.Atan2(m.M12, m.M11);
                double tscale = (Math.Sqrt(m.M11 * m.M11 + m.M12 * m.M12) +
                                 Math.Sqrt(m.M21 * m.M21 + m.M22 * m.M22)) / 2;
                ct.ScaleX = te.ScaleX * Math.Max(0.001, tscale);
                ct.ScaleY = te.ScaleY * Math.Max(0.001, tscale);
                return ct;
            }
            case DwgEntityType.MLine:
                foreach (var p in e.Points)
                {
                    var (mx, my) = m.Apply(p.X, p.Y);
                    t.Points.Add(new Vec2(mx, my));
                }
                return t;
            case DwgEntityType.Tolerance:
            {
                var (tx, ty) = m.Apply(e.Start.X, e.Start.Y);
                t.Start = new Vec2(tx, ty);
                return t;
            }
            case DwgEntityType.Hatch:
                return e is HatchEntity hatch ? hatch.TransformedClone(m) : null;
            case DwgEntityType.Region:
            case DwgEntityType.Solid3d:
            case DwgEntityType.Body:
                if (e.AcisVertices is { Length: > 0 } vs)
                {
                    var xyz = new Vec3[vs.Length];
                    for (int i = 0; i < vs.Length; i++)
                    {
                        var (vx, vy) = m.Apply(vs[i].X, vs[i].Y);
                        xyz[i] = new Vec3(vx, vy, vs[i].Z);
                    }
                    t.AcisVertices = xyz;
                }
                return t;
        }
        return null;
    }

    internal static (double MinX, double MinY, double MaxX, double MaxY)?
        DetectBlueFrame(IReadOnlyList<IEntity> entities)
    {
        double minX = double.MaxValue, minY = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue;
        int count = 0;

        foreach (var e in entities)
        {
            if (e is not DwgEntity dwg) continue;
            if (dwg.ColorIndex != 5) continue;

            switch (dwg.Kind)
            {
                case DwgEntityType.Line:
                    ExpandPoint(dwg.Start.X, dwg.Start.Y, ref minX, ref minY, ref maxX, ref maxY);
                    ExpandPoint(dwg.End.X, dwg.End.Y, ref minX, ref minY, ref maxX, ref maxY);
                    count++;
                    break;
                case DwgEntityType.LwPolyline:
                case DwgEntityType.Polyline2D:
                    foreach (var p in dwg.Points)
                        ExpandPoint(p.X, p.Y, ref minX, ref minY, ref maxX, ref maxY);
                    count++;
                    break;
            }
        }

        if (count < 4) return null;

        double w = maxX - minX, h = maxY - minY;
        if (w < 10 || h < 10) return null;

        return (minX, minY, maxX, maxY);
    }


    internal const double ViewportMarginFraction = 0.002;

    internal static int ViewportMarginPx(int widthPx, int heightPx) =>
        Math.Max(1, (int)Math.Round(Math.Min(widthPx, heightPx) * ViewportMarginFraction));


    internal void RenderEntity(DwgEntity e, RenderContext? ctx, int thick,
        PixelBuffer buf, Dictionary<ulong, short> layerColors, PngDrawDevice device)
    {
        if (EntityRenderRegistry.For(e.Kind) is not { } renderer)
            return;
        renderer.Render(e, _ctx ?? throw new InvalidOperationException(
            "RenderEntity 前必须建立 RenderContext（Render/Prepare）"), Matrix.Identity, device);
    }

    internal static int ClampPx(double v) => PxMath.ClampPx(v);

    internal static double ClampPxKeep(double v) => PxMath.ClampPxKeep(v);

    private PngDrawDevice NewDevice(PixelBuffer buf, DrawViewport view, int thick)
        => new(buf, view, thick, _text, _walk);

    internal void Prepare(DwgObjectReader.WalkResult walk)
    {
        _layerColors = walk.LayerColors;
        _walk = walk;
        _ctx = new RenderContext(walk) { DarkBackground = DarkBackground };
    }

    internal void RenderEntities(IReadOnlyList<DwgEntity> entities, PixelBuffer buf,
        int thick, DrawViewport view)
    {
        LastPassViewport = view;
        RenderInk.DarkBackground = DarkBackground;
        var device = NewDevice(buf, view, Math.Max(1, thick));
        foreach (var e in entities)
            RenderEntity(e, null, thick, buf, _layerColors, device);
    }

    internal static (double MinX, double MinY, double MaxX, double MaxY)? DetectFrame(
        DwgObjectReader.WalkResult walk, List<DwgEntity> allEntities)
        => DetectBlueFrame(allEntities)
           ?? (allEntities.Count == 0 ? DetectBlueFrame(walk.Entities) : null);
}
