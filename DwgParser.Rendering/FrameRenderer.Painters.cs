using System.Text;
using DwgParser.Core.Objects;
using DwgParser.Core.Render;

namespace DwgParser.Rendering;

public sealed partial class FrameRenderer
{
    internal readonly struct RasterCtx
    {
        public RasterCtx(PixelBuffer buf, int thick, byte cr, byte cg, byte cb, PngDrawDevice device)
        {
            Buf = buf; Thick = thick;
            Cr = cr; Cg = cg; Cb = cb; Device = device;
            Px = x => device.Px(x); Py = y => device.Py(y);
        }

        public PixelBuffer Buf { get; }
        public Func<double, double> Px { get; }
        public Func<double, double> Py { get; }
        public double Scale => Device.ScalePx;
        public int Thick { get; }
        public byte Cr { get; }
        public PngDrawDevice Device { get; }
        public byte Cg { get; }
        public byte Cb { get; }
    }

    private Dictionary<DwgEntityType, Action<DwgEntity, RasterCtx>> BuildPainters()
    {
        var painters = new Dictionary<DwgEntityType, Action<DwgEntity, RasterCtx>>();
        foreach (var key in EntityKindRegistry.RasterPainterKeys)
            painters[key] = PainterFor(key);
        return painters;
    }

    private Action<DwgEntity, RasterCtx> PainterFor(DwgEntityType key) => key switch
    {
        DwgEntityType.Line => PaintLine,
        DwgEntityType.Circle => PaintCircle,
        DwgEntityType.Arc => PaintArc,
        DwgEntityType.Point => PaintPoint,
        DwgEntityType.Solid => PaintSolid,
        DwgEntityType.LwPolyline => PaintPolyline,
        DwgEntityType.PointSequence => PaintPointSequence,
        DwgEntityType.Hatch => PaintHatch,
        DwgEntityType.Ellipse => PaintEllipse,
        DwgEntityType.Viewport => PaintViewport,
        DwgEntityType.Table => PaintTable,
        DwgEntityType.MLeader => PaintMLeader,

        DwgEntityType.Unknown or DwgEntityType.Insert or DwgEntityType.Vertex2D
            or DwgEntityType.Attrib or DwgEntityType.SeqEnd or DwgEntityType.Polyline2D
            or DwgEntityType.BlockHeader or DwgEntityType.EndBlk or DwgEntityType.Block
            or DwgEntityType.Spline or DwgEntityType.Ray or DwgEntityType.XLine
            or DwgEntityType.Leader or DwgEntityType.MText or DwgEntityType.MLine
            or DwgEntityType.Tolerance or DwgEntityType.Region or DwgEntityType.Solid3d
            or DwgEntityType.Body or DwgEntityType.AttDef or DwgEntityType.Text
            or DwgEntityType.Dimension
            or DwgEntityType.Trace
            => throw new InvalidOperationException(
                $"EntityKindRegistry.RasterPainterKeys 不应包含 {key}（它没有位图绘制实现）"),
    };

    private static void PaintLine(DwgEntity e, RasterCtx c)
    {
        c.Buf.DrawLine((int)c.Px(e.Start.X), (int)c.Py(e.Start.Y),
            (int)c.Px(e.End.X), (int)c.Py(e.End.Y), c.Cr, c.Cg, c.Cb, c.Thick);
    }

    private static void PaintCircle(DwgEntity e, RasterCtx c)
    {
        c.Buf.DrawCircle((int)c.Px(e.Center.X), (int)c.Py(e.Center.Y),
            Math.Max(1, ClampPx(e.Radius * c.Scale)), c.Cr, c.Cg, c.Cb, c.Thick);
    }

    private static void PaintArc(DwgEntity e, RasterCtx c)
    {
        double a1 = e.StartAngle, a2 = e.EndAngle;
        if (e.ExtrusionZ < 0) { a1 = -a1; a2 = -a2; }
        c.Buf.DrawArc((int)c.Px(e.Center.X), (int)c.Py(e.Center.Y),
            Math.Max(1, ClampPx(e.Radius * c.Scale)),
            a1, a2, c.Cr, c.Cg, c.Cb, c.Thick);
    }

    private static void PaintPoint(DwgEntity e, RasterCtx c)
    {
        c.Buf.FillCircle((int)c.Px(e.Start.X), (int)c.Py(e.Start.Y), 3, c.Cr, c.Cg, c.Cb);
    }
    private static void PaintSolid(DwgEntity e, RasterCtx c)
    {
        if (e.Corners.Count < 3) return;
        var pts = new List<(int, int)>();
        foreach (var corner in e.Corners)
            pts.Add(((int)c.Px(corner.X), (int)c.Py(corner.Y)));
        byte alpha = SolidRenderer.IsArrowheadForm(e.Corners) ? (byte)255 : (byte)80;
        c.Buf.FillPolygon(pts, c.Cr, c.Cg, c.Cb, alpha);
    }

    private static void PaintPolyline(DwgEntity e, RasterCtx c)
        => DrawPolylinePng(c.Buf, e, c.Px, c.Py, c.Cr, c.Cg, c.Cb, c.Thick);

    private void PaintMLeader(DwgEntity e, RasterCtx c)
    {
        if (e is not MLeaderEntity ml || !ml.Captured) return;
        byte cr = c.Cr, cg = c.Cg, cb = c.Cb;
        bool anyLine = false;
        double? styleArrow = _ctx?.ResolveMLeaderStyle(ml.MLeaderStyleHandle)?.ArrowHeadSize;

        foreach (var node in ml.Leaders)
        {
            foreach (var line in node.Lines)
            {
                if (line.Points3D.Count == 0) continue;
                double arrow = ml.ResolveArrowSize(line, styleArrow);
                for (int i = 1; i < line.Points3D.Count; i++)
                {
                    var a = line.Points3D[i - 1];
                    var b = line.Points3D[i];
                    c.Buf.DrawLine((int)c.Px(a.X), (int)c.Py(a.Y), (int)c.Px(b.X), (int)c.Py(b.Y), cr, cg, cb, c.Thick);
                }
                if (line.Points3D.Count >= 2)
                    DrawArrow(c, line.Points3D[0], line.Points3D[1], arrow, cr, cg, cb);
                else
                {
                    var p0 = line.Points3D[0];
                    double nx = p0.X - node.DoglegVector.X, ny = p0.Y - node.DoglegVector.Y;
                    if (node.HasLastLeaderLinePoint)
                    {
                        var lp = node.LastLeaderLinePoint;
                        nx = lp.X; ny = lp.Y;
                        c.Buf.DrawLine((int)c.Px(p0.X), (int)c.Py(p0.Y), (int)c.Px(lp.X), (int)c.Py(lp.Y), cr, cg, cb, c.Thick);
                    }
                    DrawArrow(c, p0, (nx, ny, p0.Z), arrow, cr, cg, cb);
                }
                anyLine = true;
            }
            if (node.HasLastLeaderLinePoint && node.HasDogleg)
            {
                double len = Finite(node.DoglegLength) && node.DoglegLength > 0 ? node.DoglegLength : ml.DoglegLength;
                if (Finite(len) && len > 1e-9)
                {
                    double dx = node.DoglegVector.X, dy = node.DoglegVector.Y;
                    double n = Math.Sqrt(dx * dx + dy * dy);
                    if (n < 1e-9) { dx = 1; dy = 0; } else { dx /= n; dy /= n; }
                    var from = node.LastLeaderLinePoint;
                    c.Buf.DrawLine((int)c.Px(from.X), (int)c.Py(from.Y),
                        (int)c.Px(from.X + dx * len), (int)c.Py(from.Y + dy * len), cr, cg, cb, c.Thick);
                }
            }
        }
        if (!anyLine && e.Points.Count >= 2)
        {
            for (int i = 1; i < e.Points.Count; i++)
                c.Buf.DrawLine((int)c.Px(e.Points[i - 1].X), (int)c.Py(e.Points[i - 1].Y),
                    (int)c.Px(e.Points[i].X), (int)c.Py(e.Points[i].Y), cr, cg, cb, c.Thick);
        }

        string text = RenderContext.SanitizeText(ml.TextValue);
        if (string.IsNullOrWhiteSpace(text) || !Finite(ml.MTextHeight) || ml.MTextHeight <= 0) return;
        double wx = ml.ContentLocation.X, wy = ml.ContentLocation.Y;
        if (!Finite(wx) || !Finite(wy) || (wx == 0 && wy == 0)) { wx = ml.Base.X; wy = ml.Base.Y; }
        if (!Finite(wx) || !Finite(wy)) return;
        var style = _walk.StyleFonts.TryGetValue(ml.ContentStyleHandle, out var st) ? st : null;
        int fontPx = Math.Max(TextRasterLimits.MinFontPx,
                              (int)(ml.MTextHeight * c.Scale * DwgParser.Core.Render.ShxTextShapes.EmFactor));
        var font = FontResolver.Resolve(style, text);
        if (c.Device.TryDrawShx(style, text, (int)c.Px(wx), (int)c.Py(wy),
                ml.MTextHeight * c.Scale, ml.ContentRotation, 1.0, 0, 0, 1.0, cr, cg, cb))
            return;
        _text.DrawText(c.Buf, (int)c.Px(wx), (int)c.Py(wy), text, cr, cg, cb, fontPx,
            ml.ContentRotation, font.Family, 1.0, 0, 0, 1.0, ml.MTextHeight * c.Scale);
    }

    private static bool Finite(double v) => !double.IsNaN(v) && !double.IsInfinity(v);

    private static void DrawArrow(RasterCtx c, (double X, double Y, double Z) tip,
        (double X, double Y, double Z) next, double size, byte cr, byte cg, byte cb)
    {
        if (!Finite(size) || size <= 0) return;
        double vx = tip.X - next.X, vy = tip.Y - next.Y;
        double n = Math.Sqrt(vx * vx + vy * vy);
        if (n < 1e-9) return;
        double w = size * DimensionRenderer.ArrowWidthRatio / 2;
        double bx = tip.X - vx / n * size, by = tip.Y - vy / n * size;
        double px = -vy / n * w, py = vx / n * w;
        var pts = new List<(int, int)>
        {
            ((int)c.Px(tip.X), (int)c.Py(tip.Y)),
            ((int)c.Px(bx + px), (int)c.Py(by + py)),
            ((int)c.Px(bx - px), (int)c.Py(by - py)),
        };
        c.Buf.FillPolygon(pts, cr, cg, cb, 255);
    }

    private static void PaintPointSequence(DwgEntity e, RasterCtx c)
    {
        for (int i = 1; i < e.Points.Count; i++)
            c.Buf.DrawLine(
                (int)c.Px(e.Points[i - 1].X), (int)c.Py(e.Points[i - 1].Y),
                (int)c.Px(e.Points[i].X), (int)c.Py(e.Points[i].Y),
                c.Cr, c.Cg, c.Cb, c.Thick);
    }

    private static void PaintHatch(DwgEntity e, RasterCtx c)
    {
        if (e is not HatchEntity he || HatchGeometry.BoundaryRings(he) is not { Count: > 0 } rings)
        {
            if (e.SolidFill && e.Points.Count >= 3)
            {
                var fillPts = new List<(int, int)>();
                foreach (var p in e.Points)
                    fillPts.Add(((int)c.Px(p.X), (int)c.Py(p.Y)));
                c.Buf.FillPolygon(fillPts, c.Cr, c.Cg, c.Cb, HatchGeometry.SolidFillAlpha);
            }
            for (int i = 1; i < e.Points.Count; i++)
                c.Buf.DrawLine(
                    (int)c.Px(e.Points[i - 1].X), (int)c.Py(e.Points[i - 1].Y),
                    (int)c.Px(e.Points[i].X), (int)c.Py(e.Points[i].Y),
                    c.Cr, c.Cg, c.Cb, c.Thick);
            return;
        }

        foreach (var ring in rings)
        {
            if (e.SolidFill && ring.Length >= 3)
            {
                var fillPts = new List<(int, int)>(ring.Length);
                foreach (var p in ring)
                    fillPts.Add(((int)c.Px(p.X), (int)c.Py(p.Y)));
                c.Buf.FillPolygon(fillPts, c.Cr, c.Cg, c.Cb, HatchGeometry.SolidFillAlpha);
            }
            for (int i = 0; i < ring.Length; i++)
            {
                var a = ring[i];
                var b = ring[(i + 1) % ring.Length];
                c.Buf.DrawLine((int)c.Px(a.X), (int)c.Py(a.Y), (int)c.Px(b.X), (int)c.Py(b.Y), c.Cr, c.Cg, c.Cb, c.Thick);
            }
        }
        foreach (var (a, b) in HatchGeometry.PatternSegments(he))
            c.Buf.DrawLine((int)c.Px(a.X), (int)c.Py(a.Y), (int)c.Px(b.X), (int)c.Py(b.Y), c.Cr, c.Cg, c.Cb, c.Thick);
    }

    private static void PaintEllipse(DwgEntity e, RasterCtx c)
        => RenderEllipse(c.Buf, e, c.Px, c.Py, c.Scale, c.Cr, c.Cg, c.Cb, c.Thick);

    private static void PaintViewport(DwgEntity e, RasterCtx c)
    {
        if (e.VportWidth > 0 && e.Height > 0)
        {
            double hw = e.VportWidth / 2, hh = e.Height / 2;
            var corners = new List<(int, int)>
            {
                ((int)c.Px(e.Center.X - hw), (int)c.Py(e.Center.Y - hh)),
                ((int)c.Px(e.Center.X + hw), (int)c.Py(e.Center.Y - hh)),
                ((int)c.Px(e.Center.X + hw), (int)c.Py(e.Center.Y + hh)),
                ((int)c.Px(e.Center.X - hw), (int)c.Py(e.Center.Y + hh)),
            };
            for (int i = 0; i < 4; i++)
                c.Buf.DrawLine(corners[i].Item1, corners[i].Item2,
                    corners[(i + 1) % 4].Item1, corners[(i + 1) % 4].Item2,
                    c.Cr, c.Cg, c.Cb, c.Thick);
        }
    }


    private void PaintTable(DwgEntity e, RasterCtx c)
    {
        if (e is not TableEntity t) return;
        var lo = TableLayout.Compute(t);
        if (lo is null) return;

        var buf = c.Buf;
        double rot = t.Rotation;
        double cos = Math.Cos(rot), sin = Math.Sin(rot);

        (int X, int Y) W(double lx, double ly)
        {
            double wx = t.Start.X + lx * cos * t.ScaleX - ly * sin * t.ScaleY;
            double wy = t.Start.Y + lx * sin * t.ScaleX + ly * cos * t.ScaleY;
            return ((int)c.Px(wx), (int)c.Py(wy));
        }

        foreach (var cp in lo.Cells)
        {
            if (cp.Background is not { } bg) continue;
            if (!TryCmcRgb(bg, out byte br, out byte bgr, out byte bb)) continue;
            var pts = new List<(int, int)>
            {
                W(cp.Rect.Left, cp.Rect.Bottom),
                W(cp.Rect.Right, cp.Rect.Bottom),
                W(cp.Rect.Right, cp.Rect.Top),
                W(cp.Rect.Left, cp.Rect.Top),
            };
            buf.FillPolygon(pts, br, bgr, bb, 255);
        }

        int th = Math.Max(1, c.Thick);
        for (int col = 0; col <= lo.Columns; col++)
        {
            double lx = lo.ColBoundaries[col];
            for (int r = 0; r < lo.Rows; r++)
            {
                if (col > 0 && col < lo.Columns && lo.SameMerge(r, col - 1, r, col)) continue;
                var (x1, y1) = W(lx, lo.RowBoundaries[r]);
                var (x2, y2) = W(lx, lo.RowBoundaries[r + 1]);
                buf.DrawLine(x1, y1, x2, y2, c.Cr, c.Cg, c.Cb, th);
            }
        }
        for (int r = 0; r <= lo.Rows; r++)
        {
            double ly = lo.RowBoundaries[r];
            for (int col = 0; col < lo.Columns; col++)
            {
                if (r > 0 && r < lo.Rows && lo.SameMerge(r - 1, col, r, col)) continue;
                var (x1, y1) = W(lo.ColBoundaries[col], ly);
                var (x2, y2) = W(lo.ColBoundaries[col + 1], ly);
                buf.DrawLine(x1, y1, x2, y2, c.Cr, c.Cg, c.Cb, th);
            }
        }

        var style = _walk.StyleFonts.TryGetValue(t.TextStyleHandle, out var st) ? st
            : TableFallbackStyle();
        const double CjkHeightEmFactor = DwgParser.Core.Render.ShxTextShapes.EmFactor;
        foreach (var cp in lo.Cells)
        {
            if (!cp.HasText) continue;
            string text = RenderContext.SanitizeText(cp.Text);
            if (text.Trim().Length == 0) continue;

            byte tr = c.Cr, tg = c.Cg, tb = c.Cb;
            if (cp.ContentColor is { } cc && TryCmcRgb(cc, out byte cr2, out byte cg2, out byte cb2))
            {
                tr = cr2; tg = cg2; tb = cb2;
            }
            (tr, tg, tb) = RenderInk.Map(tr, tg, tb);

            int a = cp.Alignment is >= 1 and <= 9 ? cp.Alignment : 5;
            int hAlign = ((a - 1) % 3) switch { 0 => 0, 1 => 1, _ => 2 };
            int vAlign = ((a - 1) / 3) switch { 0 => 3, 1 => 2, _ => 1 };
            double margin = Math.Min(cp.Rect.W, cp.Rect.H) * 0.15;
            double anchX = hAlign switch { 0 => cp.Rect.Left + margin, 1 => cp.Rect.CenterX, _ => cp.Rect.Right - margin };
            double anchY = vAlign switch { 3 => cp.Rect.Top, 2 => cp.Rect.CenterY, _ => cp.Rect.Bottom };

            var (px, py) = W(anchX, anchY);
            int fontPx = Math.Max(TextRasterLimits.MinFontPx,
                                  (int)(cp.TextHeight * c.Scale * CjkHeightEmFactor));
            double familyRot = t.Rotation + cp.Rotation;
            var cellStyle = cp.StyleHandle != 0 && _walk.StyleFonts.TryGetValue(cp.StyleHandle, out var s2)
                ? s2 : style;
            ResolvedCellStyle?.Invoke(cp.Text, cellStyle);
            var runs = MTextParser.Parse(cp.Text);
            if (MTextParser.HasFormatting(runs))
            {
                if (c.Device.TryDrawShxRuns(runs, cellStyle, px, py, cp.TextHeight * c.Scale,
                        familyRot, 1.0, hAlign, vAlign, 1.0, c.Scale, tr, tg, tb))
                    continue;
                _text.DrawMTextRuns(buf, px, py, runs, tr, tg, tb, fontPx, familyRot,
                    FontResolver.Resolve(cellStyle, text).Family, 1.0, hAlign, vAlign, 1.0,
                    c.Scale, cp.TextHeight * c.Scale);
                continue;
            }
            if (c.Device.TryDrawShx(cellStyle, text, px, py,
                    cp.TextHeight * c.Scale, familyRot, 1.0, hAlign, vAlign, 1.0, tr, tg, tb))
                continue;
            string family = FontResolver.Resolve(cellStyle, text).Family;
            _text.DrawText(buf, px, py, text, tr, tg, tb, fontPx, familyRot,
                family, 1.0, hAlign, vAlign, 1.0, cp.TextHeight * c.Scale);
        }
    }

    internal static Action<string, DwgObjectReader.TextStyleInfo?>? ResolvedCellStyle;

    internal DwgObjectReader.TextStyleInfo? TableFallbackStyle(DwgObjectReader.WalkResult? walk = null)
    {
        walk ??= _walk;
        if (walk is null) return null;
        DwgObjectReader.TextStyleInfo? best = null;
        ulong bestHandle = ulong.MaxValue;
        foreach (var (handle, st) in walk.StyleFonts)
        {
            string main = st.FontFile ?? "";
            if (main.Length == 0) continue;
            if (main.EndsWith(".shx", StringComparison.OrdinalIgnoreCase)) continue;
            if (handle < bestHandle) { bestHandle = handle; best = st; }
        }
        return best;
    }

    private static bool TryCmcRgb(TableCmcColor c, out byte r, out byte g, out byte b)
        => AciPalette.TryCmcToRgb(c.Rgb, c.Index, out r, out g, out b);
}
