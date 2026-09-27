using System.Text;
using DwgParser.Core.Objects;

using DwgParser.Core.Render;
namespace DwgParser.Rendering;

public sealed class SoftwareRasterizer
{
    private readonly PixelBuffer _buffer;
    private readonly int _margin;
    private readonly int _lineThickness;

    public SoftwareRasterizer(int width = 2048, int height = 2048, int? margin = null,
        int lineThickness = 1)
    {
        _buffer = new PixelBuffer(width, height);
        _margin = margin ?? FrameRenderer.ViewportMarginPx(width, height);
        _lineThickness = lineThickness;
    }

    public PixelBuffer Buffer => _buffer;

    internal DrawViewport LastPassViewport { get; private set; }

    public void Render(DwgObjectReader.WalkResult walkResult)
    {
        var ctx = new RenderContext(walkResult);
        var allEntities = ctx.ModelSpaceEntities;
        if (allEntities.Count == 0) return;

        var boundsEntities = FrameRenderer.CollectAllEntities(walkResult);
        FrameRenderer.ComputeBounds(boundsEntities, out double minX, out double minY, out double maxX, out double maxY);
        if (FrameRenderer.IsBlankBounds(minX, minY, maxX, maxY)) return;

        LastPassViewport = new DrawViewport(minX, minY, maxX, maxY, _buffer.Width, _buffer.Height, _margin, false);
        var (Tx, Ty, scale) = MakeTransform(minX, minY, maxX, maxY);
        foreach (var e in allEntities)
            if (e is DwgEntity dwg)
                RenderDwgEntity(dwg, ctx, Tx, Ty, scale);
    }

    public void Render(IReadOnlyList<IEntity> entities)
    {
        if (entities.Count == 0) return;
        var dwgList = new List<DwgEntity>(entities.Count);
        foreach (var e in entities) if (e is DwgEntity d) dwgList.Add(d);
        FrameRenderer.ComputeBounds(dwgList, out double minX, out double minY, out double maxX, out double maxY);
        if (FrameRenderer.IsBlankBounds(minX, minY, maxX, maxY)) return;

        LastPassViewport = new DrawViewport(minX, minY, maxX, maxY, _buffer.Width, _buffer.Height, _margin, false);
        var (Tx, Ty, scale) = MakeTransform(minX, minY, maxX, maxY);
        foreach (var entity in entities)
            if (entity is DwgEntity dwg)
                RenderDwgEntity(dwg, null, Tx, Ty, scale);
    }

    private (Func<double, double, double> Tx, Func<double, double, double> Ty, double scale) MakeTransform(
        double minX, double minY, double maxX, double maxY)
    {
        double worldW = maxX - minX;
        double worldH = maxY - minY;
        double drawW = _buffer.Width - 2 * _margin;
        double drawH = _buffer.Height - 2 * _margin;
        double scale = Math.Min(drawW / worldW, drawH / worldH);
        double offsetX = _margin + (drawW - worldW * scale) / 2;
        double offsetY = _margin + (drawH - worldH * scale) / 2;
        double ox = minX, oy = minY;
        int h = _buffer.Height;
        Func<double, double, double> Tx = (wx, wy) => offsetX + (wx - ox) * scale;
        Func<double, double, double> Ty = (wx, wy) => h - (offsetY + (wy - oy) * scale);
        return (Tx, Ty, scale);
    }

    private void RenderDwgEntity(DwgEntity e, RenderContext? ctx,
        Func<double, double, double> Tx, Func<double, double, double> Ty, double scale)
    {
        var (cr, cg, cb) = ParseColor(ctx?.EntityColor(e) ?? RenderContext.Color(e));
        int thick = Math.Max(1, _lineThickness);

        switch (e.Kind)
        {
            case DwgEntityType.Line:
            case DwgEntityType.Ray:
            case DwgEntityType.XLine:
                _buffer.DrawLine(PxK(Tx, e.Start.X, e.Start.Y), PyK(Ty, e.Start.X, e.Start.Y),
                    PxK(Tx, e.End.X, e.End.Y), PyK(Ty, e.End.X, e.End.Y), cr, cg, cb, thick);
                break;

            case DwgEntityType.Circle:
                _buffer.DrawCircle(PxK(Tx, e.Center.X, e.Center.Y), PyK(Ty, e.Center.X, e.Center.Y),
                    Math.Max(1, e.Radius * scale), cr, cg, cb, thick);
                break;

            case DwgEntityType.Arc:
                {
                    double a1 = e.StartAngle, a2 = e.EndAngle;
                    if (e.ExtrusionZ < 0) { a1 = -a1; a2 = -a2; }
                    _buffer.DrawArc(PxK(Tx, e.Center.X, e.Center.Y), PyK(Ty, e.Center.X, e.Center.Y),
                        Math.Max(1, e.Radius * scale), a1, a2,
                        cr, cg, cb, thick);
                }
                break;

            case DwgEntityType.Ellipse:
                RenderEllipse(e, Tx, Ty, scale, cr, cg, cb, thick);
                break;

            case DwgEntityType.Point:
                _buffer.FillCircle(Px(Tx, e.Start.X, e.Start.Y), Py(Ty, e.Start.X, e.Start.Y),
                    3, cr, cg, cb);
                break;

            case DwgEntityType.Text:
            case DwgEntityType.MText:
            case DwgEntityType.AttDef:
            case DwgEntityType.Attrib:
            {
                string text = RenderContext.SanitizeText(e.TextValue);
                if (!string.IsNullOrWhiteSpace(text))
                    _buffer.DrawText(Px(Tx, e.Start.X, e.Start.Y), Py(Ty, e.Start.X, e.Start.Y),
                        text, cr, cg, cb, 2);
                break;
            }

            case DwgEntityType.Solid:
            case DwgEntityType.Trace:
            {
                if (e.Corners.Count < 3) break;
                var pts = new System.Collections.Generic.List<(int, int)>();
                foreach (var c in e.Corners)
                    pts.Add((Px(Tx, c.X, c.Y), Py(Ty, c.X, c.Y)));
                _buffer.FillPolygon(pts, cr, cg, cb, 80);
                break;
            }

            case DwgEntityType.LwPolyline:
            case DwgEntityType.Polyline2D:
                DrawPolylinePng(e, Tx, Ty, scale, cr, cg, cb, thick);
                break;

            case DwgEntityType.Leader:
            case DwgEntityType.Spline:
                for (int i = 1; i < e.Points.Count; i++)
                    _buffer.DrawLine(
                        PxK(Tx, e.Points[i - 1].X, e.Points[i - 1].Y), PyK(Ty, e.Points[i - 1].X, e.Points[i - 1].Y),
                        PxK(Tx, e.Points[i].X, e.Points[i].Y), PyK(Ty, e.Points[i].X, e.Points[i].Y),
                        cr, cg, cb, thick);
                break;

            case DwgEntityType.Hatch:
            {
                var rings = e is HatchEntity he
                    ? HatchGeometry.BoundaryRings(he)
                    : new System.Collections.Generic.List<DwgParser.Core.Objects.Vec2[]>();
                if (rings.Count == 0)
                {
                    if (e.SolidFill && e.Points.Count >= 3)
                    {
                        var fillPts = new System.Collections.Generic.List<(int, int)>();
                        foreach (var p in e.Points)
                            fillPts.Add((Px(Tx, p.X, p.Y), Py(Ty, p.X, p.Y)));
                        _buffer.FillPolygon(fillPts, cr, cg, cb, HatchGeometry.SolidFillAlpha);
                    }
                    for (int i = 1; i < e.Points.Count; i++)
                        _buffer.DrawLine(
                            PxK(Tx, e.Points[i - 1].X, e.Points[i - 1].Y), PyK(Ty, e.Points[i - 1].X, e.Points[i - 1].Y),
                            PxK(Tx, e.Points[i].X, e.Points[i].Y), PyK(Ty, e.Points[i].X, e.Points[i].Y),
                            cr, cg, cb, thick);
                    break;
                }
                foreach (var ring in rings)
                {
                    if (e.SolidFill && ring.Length >= 3)
                    {
                        var fillPts = new System.Collections.Generic.List<(int, int)>(ring.Length);
                        foreach (var p in ring)
                            fillPts.Add((Px(Tx, p.X, p.Y), Py(Ty, p.X, p.Y)));
                        _buffer.FillPolygon(fillPts, cr, cg, cb, HatchGeometry.SolidFillAlpha);
                    }
                    for (int i = 0; i < ring.Length; i++)
                    {
                        var a = ring[i];
                        var b = ring[(i + 1) % ring.Length];
                        _buffer.DrawLine(PxK(Tx, a.X, a.Y), PyK(Ty, a.X, a.Y),
                                         PxK(Tx, b.X, b.Y), PyK(Ty, b.X, b.Y), cr, cg, cb, thick);
                    }
                }
                foreach (var (pa, pb) in HatchGeometry.PatternSegments((HatchEntity)e))
                    _buffer.DrawLine(PxK(Tx, pa.X, pa.Y), PyK(Ty, pa.X, pa.Y),
                                     PxK(Tx, pb.X, pb.Y), PyK(Ty, pb.X, pb.Y), cr, cg, cb, thick);
                break;
            }

            case DwgEntityType.Dimension:
                if (e.Points.Count >= 2)
                {
                    for (int i = 1; i < e.Points.Count; i++)
                        _buffer.DrawLine(
                            PxK(Tx, e.Points[i - 1].X, e.Points[i - 1].Y), PyK(Ty, e.Points[i - 1].X, e.Points[i - 1].Y),
                            PxK(Tx, e.Points[i].X, e.Points[i].Y), PyK(Ty, e.Points[i].X, e.Points[i].Y),
                            cr, cg, cb, thick);
                }
                break;

            case DwgEntityType.Viewport:
                if (e.VportWidth > 0 && e.Height > 0)
                {
                    double hw = e.VportWidth / 2, hh = e.Height / 2;
                    var corners = new System.Collections.Generic.List<(double, double)>
                    {
                        (PxK(Tx, e.Center.X - hw, e.Center.Y - hh), PyK(Ty, e.Center.X - hw, e.Center.Y - hh)),
                        (PxK(Tx, e.Center.X + hw, e.Center.Y - hh), PyK(Ty, e.Center.X + hw, e.Center.Y - hh)),
                        (PxK(Tx, e.Center.X + hw, e.Center.Y + hh), PyK(Ty, e.Center.X + hw, e.Center.Y + hh)),
                        (PxK(Tx, e.Center.X - hw, e.Center.Y + hh), PyK(Ty, e.Center.X - hw, e.Center.Y + hh)),
                    };
                    for (int i = 0; i < 4; i++)
                        _buffer.DrawLine(corners[i].Item1, corners[i].Item2,
                            corners[(i + 1) % 4].Item1, corners[(i + 1) % 4].Item2,
                            cr, cg, cb, thick);
                }
                break;

            case DwgEntityType.Insert:
                DrawInsert(e, ctx, Tx, Ty, scale, cr, cg, cb, thick, 0);
                break;
        }
    }

    private static int Px(Func<double, double, double> T, double x, double y) => FrameRenderer.ClampPx(T(x, y));
    private static int Py(Func<double, double, double> T, double x, double y) => FrameRenderer.ClampPx(T(x, y));
    private static double PxK(Func<double, double, double> T, double x, double y) => FrameRenderer.ClampPxKeep(T(x, y));
    private static double PyK(Func<double, double, double> T, double x, double y) => FrameRenderer.ClampPxKeep(T(x, y));

    private void DrawInsert(DwgEntity e, RenderContext? ctx,
        Func<double, double, double> Tx, Func<double, double, double> Ty,
        double scale, byte cr, byte cg, byte cb, int thick, int depth)
    {
        if (depth > 16 || ctx == null) return;
        if (!ctx.BlockContents.TryGetValue(e.BlockHeaderHandle, out var contents)) return;
        var ins = e.InsertPlacement();
        Func<double, double, double> nTx = (lx, ly) => { var q = ins.Apply(lx, ly); return Tx(q.X, q.Y); };
        Func<double, double, double> nTy = (lx, ly) => { var q = ins.Apply(lx, ly); return Ty(q.X, q.Y); };
        double factor = scale * DwgParser.Core.Render.CurveFamily.AxisScale(ins);
        foreach (var sub in contents)
            if (sub is DwgEntity se)
                RenderDwgEntity(se, ctx, nTx, nTy, factor);
    }

    private void DrawPolylinePng(DwgEntity e,
        Func<double, double, double> Tx, Func<double, double, double> Ty, double scale,
        byte cr, byte cg, byte cb, int thick)
    {
        int n = e.Points.Count;
        if (n < 2) return;
        for (int i = 1; i < n; i++)
            DrawPolySegment(e.Points[i - 1], e.Points[i],
                i - 1 < e.Bulges.Count ? e.Bulges[i - 1] : 0.0, Tx, Ty, cr, cg, cb, thick);
        if (e.Closed && n > 1)
            DrawPolySegment(e.Points[n - 1], e.Points[0],
                n - 1 < e.Bulges.Count ? e.Bulges[n - 1] : 0.0, Tx, Ty, cr, cg, cb, thick);
    }

    private void DrawPolySegment(Vec2 p1, Vec2 p2, double bulge,
        Func<double, double, double> Tx, Func<double, double, double> Ty,
        byte cr, byte cg, byte cb, int thick)
    {
        if (Math.Abs(bulge) < 1e-6)
        {
            _buffer.DrawLine(PxK(Tx, p1.X, p1.Y), PyK(Ty, p1.X, p1.Y),
                PxK(Tx, p2.X, p2.Y), PyK(Ty, p2.X, p2.Y), cr, cg, cb, thick);
            return;
        }
        var pts = BulgeSampler.Sample(p1, p2, bulge);
        for (int i = 1; i < pts.Count; i++)
            _buffer.DrawLine(PxK(Tx, pts[i - 1].X, pts[i - 1].Y), PyK(Ty, pts[i - 1].X, pts[i - 1].Y),
                PxK(Tx, pts[i].X, pts[i].Y), PyK(Ty, pts[i].X, pts[i].Y), cr, cg, cb, thick);
    }

    private void RenderEllipse(DwgEntity e,
        Func<double, double, double> Tx, Func<double, double, double> Ty, double scale,
        byte cr, byte cg, byte cb, int thick)
    {
        double a = Math.Max(0.001, e.Radius);
        double b = Math.Max(0.001, a * (e.WidthFactor > 0 ? e.WidthFactor : 1.0));
        double theta = Math.Atan2(e.Start.Y, e.Start.X);
        double start = e.StartAngle, end = e.EndAngle;
        if (end < start) end += Math.PI * 2;
        if (end - start < 1e-6) { start = 0; end = Math.PI * 2; }
        bool mirror = e.ExtrusionZ < 0;
        int n = Math.Max(16, (int)((end - start) / (Math.PI / 48)));
        double cosT = Math.Cos(theta), sinT = Math.Sin(theta);
        double px = 0, py = 0;
        for (int i = 0; i <= n; i++)
        {
            double t = start + (end - start) * i / n;
            double ct = Math.Cos(t), st = Math.Sin(t);
            double lx = a * ct * cosT - b * st * sinT;
            double ly = a * ct * sinT + b * st * cosT;
            if (mirror) ly = -ly;
            double x = e.Center.X + lx, y = e.Center.Y + ly;
            double nx = PxK(Tx, x, y), ny = PyK(Ty, x, y);
            if (i > 0)
                _buffer.DrawLine(px, py, nx, ny, cr, cg, cb, thick);
            px = nx; py = ny;
        }
    }

    private static (byte r, byte g, byte b) ParseColor(string hex)
    {
        if (string.IsNullOrEmpty(hex) || hex.Length < 7) return (0, 0, 0);
        return ((byte)Convert.ToInt32(hex.Substring(1, 2), 16),
                (byte)Convert.ToInt32(hex.Substring(3, 2), 16),
                (byte)Convert.ToInt32(hex.Substring(5, 2), 16));
    }
}