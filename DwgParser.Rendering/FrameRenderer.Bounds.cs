using DwgParser.Core.Objects;
using DwgParser.Core.Render;

namespace DwgParser.Rendering;

public sealed partial class FrameRenderer
{
    private static void ExpandPoint(double x, double y,
        ref double minX, ref double minY, ref double maxX, ref double maxY)
    {
        if (x < minX) minX = x;
        if (y < minY) minY = y;
        if (x > maxX) maxX = x;
        if (y > maxY) maxY = y;
    }

    internal static void ComputeBounds(List<DwgEntity> entities,
        out double minX, out double minY, out double maxX, out double maxY,
        Dictionary<ulong, List<DwgEntity>>? blockContents = null)
    {
        minX = double.MaxValue; minY = double.MaxValue;
        maxX = double.MinValue; maxY = double.MinValue;
        foreach (var e in entities)
            ExpandBounds(e, ref minX, ref minY, ref maxX, ref maxY, blockContents);
        if (!IsBlankBounds(minX, minY, maxX, maxY)) return;
        foreach (var e in entities)
        {
            if (e.Kind is DwgEntityType.Ray or DwgEntityType.XLine)
                ExpandPoint(e.End.X, e.End.Y, ref minX, ref minY, ref maxX, ref maxY);
            else if (e.Kind is DwgEntityType.Text or DwgEntityType.MText
                        or DwgEntityType.AttDef or DwgEntityType.Attrib)
                ExpandTextRescueBox(e, ref minX, ref minY, ref maxX, ref maxY);
        }
    }

    internal static bool IsBlankBounds(double minX, double minY, double maxX, double maxY)
        => !double.IsFinite(minX) ||
           (maxX - minX < 1e-9 && maxY - minY < 1e-9);

    private static void ExpandTextRescueBox(DwgEntity e, ref double minX, ref double minY,
        ref double maxX, ref double maxY)
    {
        double h = e.Height > 1e-9 && double.IsFinite(e.Height) ? e.Height : 1.0;
        string raw = e.TextValue ?? "";
        string text = RenderContext.SanitizeText(raw);
        double wf = e.WidthFactor > 1e-9 ? e.WidthFactor : 1.0;
        double w = Math.Max(text.Length, 1) * h * wf;
        int lines = 1;
        double gap = 1.0;
        if (e.Kind == DwgEntityType.MText)
        {
            lines = raw.Split("\\P", StringSplitOptions.None).Length;
            if (e.LineSpacingFactor > 1e-9) gap = e.LineSpacingFactor;
            if (e.RectWidth > w) w = e.RectWidth;
        }
        double hh = h * DwgParser.Core.Render.ShxTextShapes.EmFactor * lines * gap;
        double cos = Math.Cos(e.Rotation), sin = Math.Sin(e.Rotation);
        ExpandBoxCorners(e.Start, w, hh, cos, sin, ref minX, ref minY, ref maxX, ref maxY);
        if (e.HorizMode != 0 || e.VertMode != 0)
            ExpandBoxCorners(e.AlignmentPoint, w, hh, cos, sin, ref minX, ref minY, ref maxX, ref maxY);
    }

    private static void ExpandBoxCorners(Vec2 center, double w, double hh,
        double cos, double sin, ref double minX, ref double minY, ref double maxX, ref double maxY)
    {
        foreach (var (lx, ly) in new[] { (w, hh), (w, -hh), (-w, hh), (-w, -hh) })
            ExpandPoint(center.X + lx * cos - ly * sin,
                center.Y + lx * sin + ly * cos, ref minX, ref minY, ref maxX, ref maxY);
    }

    internal static void ExpandBounds(DwgEntity e, ref double minX, ref double minY,
        ref double maxX, ref double maxY,
        Dictionary<ulong, List<DwgEntity>>? blockContents = null)
    {
        if (e.Kind is DwgEntityType.Ray or DwgEntityType.XLine)
        {
            ExpandPoint(e.Start.X, e.Start.Y, ref minX, ref minY, ref maxX, ref maxY);
            return;
        }
        switch (EntityKindRegistry.RasterKindOf(e.Kind))
        {
            case DwgEntityType.PointSequence:
                foreach (var p in e.Points)
                    ExpandPoint(p.X, p.Y, ref minX, ref minY, ref maxX, ref maxY);
                break;
            case DwgEntityType.Ellipse:
            {
                double rmaj = Math.Max(e.Radius, 1e-9);
                double r = rmaj * Math.Max(1.0, Math.Abs(e.WidthFactor));
                ExpandPoint(e.Center.X - r, e.Center.Y - r, ref minX, ref minY, ref maxX, ref maxY);
                ExpandPoint(e.Center.X + r, e.Center.Y + r, ref minX, ref minY, ref maxX, ref maxY);
                break;
            }
            case DwgEntityType.Line:
                ExpandPoint(e.Start.X, e.Start.Y, ref minX, ref minY, ref maxX, ref maxY);
                ExpandPoint(e.End.X, e.End.Y, ref minX, ref minY, ref maxX, ref maxY);
                break;
            case DwgEntityType.Hatch:
                if (e is HatchEntity he && HatchGeometry.BoundaryRings(he) is { Count: > 0 } hRings)
                    foreach (var ring in hRings)
                        foreach (var p in ring)
                            ExpandPoint(p.X, p.Y, ref minX, ref minY, ref maxX, ref maxY);
                else
                    foreach (var p in e.Points)
                        ExpandPoint(p.X, p.Y, ref minX, ref minY, ref maxX, ref maxY);
                break;
            case DwgEntityType.Circle:
            case DwgEntityType.Arc:
                ExpandPoint(e.Center.X - e.Radius, e.Center.Y - e.Radius,
                    ref minX, ref minY, ref maxX, ref maxY);
                ExpandPoint(e.Center.X + e.Radius, e.Center.Y + e.Radius,
                    ref minX, ref minY, ref maxX, ref maxY);
                break;
            case DwgEntityType.Point:
                ExpandPoint(e.Start.X, e.Start.Y, ref minX, ref minY, ref maxX, ref maxY);
                break;
            case DwgEntityType.Text:
            case DwgEntityType.MText:
            case DwgEntityType.AttDef:
            case DwgEntityType.Attrib:
                ExpandPoint(e.Start.X, e.Start.Y, ref minX, ref minY, ref maxX, ref maxY);
                break;
            case DwgEntityType.LwPolyline:
            case DwgEntityType.Polyline2D:
                foreach (var p in e.Points)
                    ExpandPoint(p.X, p.Y, ref minX, ref minY, ref maxX, ref maxY);
                break;
            case DwgEntityType.Solid:
            case DwgEntityType.Trace:
                foreach (var c in e.Corners)
                    ExpandPoint(c.X, c.Y, ref minX, ref minY, ref maxX, ref maxY);
                break;
            case DwgEntityType.Insert:
                ExpandPoint(e.Start.X, e.Start.Y, ref minX, ref minY, ref maxX, ref maxY);
                break;
            case DwgEntityType.Dimension:
                var dBlock = e.DimBlockHandle != 0 && blockContents != null
                    && blockContents.TryGetValue(e.DimBlockHandle, out var dbl) ? dbl : null;
                var dshape = DimensionRenderer.Plan(e, null, dBlock);
                if (dshape != null)
                {
                    foreach (var (la, lb) in dshape.Lines)
                    {
                        ExpandPoint(la.X, la.Y, ref minX, ref minY, ref maxX, ref maxY);
                        ExpandPoint(lb.X, lb.Y, ref minX, ref minY, ref maxX, ref maxY);
                    }
                    foreach (var (atip, adir) in dshape.Arrows)
                    {
                        double alen = dshape.ArrowLength;
                        double aw = alen * DimensionRenderer.ArrowWidthRatio;
                        double abx = atip.X - adir.X * alen, aby = atip.Y - adir.Y * alen;
                        double anx = -adir.Y * aw / 2, any = adir.X * aw / 2;
                        ExpandPoint(atip.X, atip.Y, ref minX, ref minY, ref maxX, ref maxY);
                        ExpandPoint(abx + anx, aby + any, ref minX, ref minY, ref maxX, ref maxY);
                        ExpandPoint(abx - anx, aby - any, ref minX, ref minY, ref maxX, ref maxY);
                    }
                    if (dshape.HasText)
                    {
                        double mh = Math.Max(Math.Max(e.Height, dshape.TextHeight), 1.0);
                        ExpandPoint(dshape.TextPos.X - mh, dshape.TextPos.Y - mh, ref minX, ref minY, ref maxX, ref maxY);
                        ExpandPoint(dshape.TextPos.X + mh, dshape.TextPos.Y + 2 * mh, ref minX, ref minY, ref maxX, ref maxY);
                    }
                }
                else
                {
                    foreach (var p in e.Points)
                        ExpandPoint(p.X, p.Y, ref minX, ref minY, ref maxX, ref maxY);
                    ExpandPoint(e.DimTextMid.X, e.DimTextMid.Y, ref minX, ref minY, ref maxX, ref maxY);
                }
                break;
            case DwgEntityType.Viewport:
                if (e.VportWidth > 0 && e.Height > 0)
                {
                    double hw = e.VportWidth / 2, hh = e.Height / 2;
                    ExpandPoint(e.Center.X - hw, e.Center.Y - hh, ref minX, ref minY, ref maxX, ref maxY);
                    ExpandPoint(e.Center.X + hw, e.Center.Y + hh, ref minX, ref minY, ref maxX, ref maxY);
                }
                break;
            case DwgEntityType.Table:
            {
                var lo = e is TableEntity te ? TableLayout.Compute(te) : null;
                if (lo is null) { ExpandPoint(e.Start.X, e.Start.Y, ref minX, ref minY, ref maxX, ref maxY); break; }
                double cos = Math.Cos(e.Rotation), sin = Math.Sin(e.Rotation);
                foreach (var (lx, ly) in new[]
                         {
                             (0.0, 0.0), (lo.Width, 0.0), (lo.Width, -lo.Height), (0.0, -lo.Height),
                         })
                {
                    double wx = e.Start.X + lx * cos * e.ScaleX - ly * sin * e.ScaleY;
                    double wy = e.Start.Y + lx * sin * e.ScaleX + ly * cos * e.ScaleY;
                    ExpandPoint(wx, wy, ref minX, ref minY, ref maxX, ref maxY);
                }
                break;
            }
            case DwgEntityType.MLeader:
                foreach (var p in e.Points)
                    ExpandPoint(p.X, p.Y, ref minX, ref minY, ref maxX, ref maxY);
                if (e is MLeaderEntity bml && !string.IsNullOrEmpty(bml.TextValue))
                {
                    double bx = bml.ContentLocation.X, by = bml.ContentLocation.Y;
                    if (bx == 0 && by == 0) { bx = bml.Base.X; by = bml.Base.Y; }
                    double mh = double.IsNaN(bml.MTextHeight) || double.IsInfinity(bml.MTextHeight)
                        ? 1.0 : Math.Max(bml.MTextHeight, 1.0);
                    ExpandPoint(bx, by, ref minX, ref minY, ref maxX, ref maxY);
                    ExpandPoint(bx + mh * 8, by + mh, ref minX, ref minY, ref maxX, ref maxY);
                }
                break;
        }
    }
}
