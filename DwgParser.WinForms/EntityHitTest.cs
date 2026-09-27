using System.Drawing;
using DwgParser.Core.Objects;
using DwgParser.Core.Render;
using DwgParser.Rendering;

namespace DwgParser.WinForms;

internal static class EntityHitTest
{
    public static DwgEntity? Pick(DwgScene scene, double x, double y, double tolerance)
    {
        DwgEntity? best = null;
        double bestDistance = double.MaxValue;
        double bestArea = double.MaxValue;

        foreach (var e in scene.VisibleEntities)
        {
            var b = scene.BoundsOf(e);
            if (!b.IsFinite) continue;

            if (b.DistanceTo(x, y) > tolerance) continue;

            double d = DistanceTo(e, x, y);
            if (d == double.MaxValue) d = BoundsFallback(b, x, y);
            if (d > tolerance) continue;

            double area = Math.Max(b.Width, 1e-9) * Math.Max(b.Height, 1e-9);
            if (d < bestDistance - 1e-12 || (Math.Abs(d - bestDistance) <= 1e-12 && area < bestArea))
            {
                best = e; bestDistance = d; bestArea = area;
            }
        }
        return best;
    }

    public static double DistanceTo(DwgEntity e, double x, double y)
    {
        switch (e.Kind)
        {
            case DwgEntityType.Line:
            case DwgEntityType.Ray:
            case DwgEntityType.XLine:
                return Segment(e.Start.X, e.Start.Y, e.End.X, e.End.Y, x, y);

            case DwgEntityType.Point:
                return Hypot(e.Start.X - x, e.Start.Y - y);

            case DwgEntityType.Circle:
            {
                double d = Hypot(e.Center.X - x, e.Center.Y - y);
                return Math.Abs(d - Math.Abs(e.Radius));
            }

            case DwgEntityType.Arc:
                return ArcDistance(e, x, y);

            case DwgEntityType.Ellipse:
                return PolylineDistance(EllipseSamples(e), false, x, y);

            case DwgEntityType.LwPolyline:
            case DwgEntityType.Polyline2D:
            case DwgEntityType.MLine:
            case DwgEntityType.Leader:
            case DwgEntityType.MLeader:
            case DwgEntityType.Spline:
            case DwgEntityType.PointSequence:
                return PolylineDistance(e.Points, e.Closed, x, y);

            case DwgEntityType.Solid:
            case DwgEntityType.Trace:
                return RingDistance(e.Corners, true, x, y);

            case DwgEntityType.Text:
            case DwgEntityType.MText:
            case DwgEntityType.AttDef:
            case DwgEntityType.Attrib:
                return TextBoxDistance(e, x, y);

            default:
                return double.MaxValue;
        }
    }

    private static double TextBoxDistance(DwgEntity e, double x, double y)
    {
        double h = e.Height > 1e-9 && double.IsFinite(e.Height) ? e.Height : 1.0;
        double wf = e.WidthFactor > 1e-9 ? e.WidthFactor : 1.0;
        bool mt = e.Kind == DwgEntityType.MText;
        string raw = e.TextValue ?? "";

        int hAlign, vAlign;
        Vec2 anchor;
        if (mt)
        {
            int att = e.TextAttachment is >= 1 and <= 9 ? e.TextAttachment : 1;
            hAlign = ((att - 1) % 3) switch { 0 => 0, 1 => 1, _ => 2 };
            vAlign = ((att - 1) / 3) switch { 0 => 3, 1 => 2, _ => 1 };
            anchor = e.Start;
        }
        else
        {
            hAlign = e.HorizMode switch { 1 => 1, 2 => 2, 4 => 1, _ => 0 };
            vAlign = e.VertMode switch { 1 => 1, 2 => 2, 3 => 3, _ => 0 };
            bool nonDefault = e.HorizMode != 0 || e.VertMode != 0;
            anchor = nonDefault ? e.AlignmentPoint : e.Start;
        }

        double gap = mt && e.LineSpacingFactor > 1e-9 ? e.LineSpacingFactor : 1.0;

        LayoutMText(h, wf, mt, raw, e.RectWidth, gap, out double boxW, out int effLines);

        double emWorld = h * DwgParser.Core.Render.DwgTextMetrics.EmFactor;
        double pitchWorld = h * (5.0 / 3.0) * 1.1 * gap;
        double hh = effLines <= 1 ? emWorld : (effLines - 1) * pitchWorld + emWorld;

        double u0 = hAlign switch { 1 => -boxW / 2, 2 => -boxW, _ => 0 };
        double v0 = vAlign switch { 2 => -hh / 2, 3 => -hh, _ => 0 };
        double u1 = u0 + boxW, v1 = v0 + hh;

        double cos = Math.Cos(e.Rotation), sin = Math.Sin(e.Rotation);
        double dx = x - anchor.X, dy = y - anchor.Y;
        double u = dx * cos + dy * sin;
        double v = -dx * sin + dy * cos;
        double du = Math.Max(Math.Max(u0 - u, u - u1), 0);
        double dv = Math.Max(Math.Max(v0 - v, v - v1), 0);
        return Hypot(du, dv);
    }

    private static void LayoutMText(double h, double wf, bool mt, string raw,
        double rectWidth, double gap, out double boxW, out int effLines)
    {
        boxW = 0;
        effLines = 0;
        string defaultFamily = FontResolver.Resolve(null, raw).Family;
        if (!mt)
        {
            boxW = MeasureRunWidth(raw, defaultFamily, (float)(h * 100), false, false, wf) / 100.0;
            effLines = 1;
            return;
        }
        var lines = MTextParser.Parse(raw);
        const double S = 100.0;
        foreach (var ln in lines)
        {
            double lineW = 0;
            foreach (var run in ln.Runs)
            {
                if (run.Text.Length == 0 && !run.IsStack) continue;
                double px = run.AbsoluteHeight.HasValue ? run.AbsoluteHeight.Value * S : h * S * run.HeightScale;
                string fam = FontResolver.ResolveRunFamily(run.FontSpec, defaultFamily);
                double runWf = run.WidthFactor ?? wf;
                double wpx;
                if (run.IsStack)
                {
                    double wt = MeasureRunWidth(run.StackTop, fam, (float)px, run.Bold, run.Italic, runWf);
                    double wb = MeasureRunWidth(run.StackBottom, fam, (float)px, run.Bold, run.Italic, runWf);
                    wpx = Math.Max(wt, wb);
                }
                else
                {
                    wpx = MeasureRunWidth(run.Text, fam, (float)px, run.Bold, run.Italic, runWf);
                }
                lineW += wpx / S;
            }
            if (rectWidth > 1e-9 && lineW > rectWidth)
            {
                effLines += (int)System.Math.Ceiling(lineW / rectWidth);
                boxW = System.Math.Max(boxW, rectWidth);
            }
            else
            {
                effLines += 1;
                boxW = System.Math.Max(boxW, lineW);
            }
        }
    }

    private static double MeasureRunWidth(string text, string family, float px, bool bold, bool italic, double wf)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        var style = FontStyle.Regular
            | (bold ? FontStyle.Bold : 0)
            | (italic ? FontStyle.Italic : 0);
        try
        {
            using var bmp = new Bitmap(1, 1);
            using var g = Graphics.FromImage(bmp);
            using var font = new Font(string.IsNullOrWhiteSpace(family) ? "Arial" : family,
                Math.Clamp((int)px, 2, 200), style, GraphicsUnit.Pixel);
            using var fmt = StringFormat.GenericTypographic;
            return g.MeasureString(text, font, PointF.Empty, fmt).Width * wf;
        }
        catch { return 0; }
    }

    public static double BoundsFallback(Bounds2 b, double x, double y)
        => b.Contains(x, y) ? 0 : b.DistanceTo(x, y);


    private static double Hypot(double dx, double dy) => Math.Sqrt(dx * dx + dy * dy);

    private static double Segment(double ax, double ay, double bx, double by, double px, double py)
    {
        double dx = bx - ax, dy = by - ay;
        double len2 = dx * dx + dy * dy;
        double t = len2 > 0 ? ((px - ax) * dx + (py - ay) * dy) / len2 : 0;
        t = Math.Clamp(t, 0, 1);
        return Hypot(ax + t * dx - px, ay + t * dy - py);
    }

    private static double ArcDistance(DwgEntity e, double x, double y)
    {
        double r = Math.Abs(e.Radius);
        double dx = x - e.Center.X, dy = y - e.Center.Y;
        double ang = Math.Atan2(dy, dx);
        double a0 = e.StartAngle, a1 = e.EndAngle;
        if (a1 < a0) a1 += Math.PI * 2;
        double sweep = a1 - a0;
        if (sweep <= 0 || sweep >= Math.PI * 2) return Math.Abs(Hypot(dx, dy) - r);

        double rel = ang - a0;
        while (rel < 0) rel += Math.PI * 2;
        while (rel >= Math.PI * 2) rel -= Math.PI * 2;
        if (rel <= sweep) return Math.Abs(Hypot(dx, dy) - r);

        double ex0 = e.Center.X + r * Math.Cos(a0), ey0 = e.Center.Y + r * Math.Sin(a0);
        double ex1 = e.Center.X + r * Math.Cos(a1), ey1 = e.Center.Y + r * Math.Sin(a1);
        return Math.Min(Hypot(ex0 - x, ey0 - y), Hypot(ex1 - x, ey1 - y));
    }

    private static double PolylineDistance(IReadOnlyList<Vec2> pts, bool closed, double x, double y)
        => RingDistance(pts, closed, x, y);

    private static double RingDistance(IReadOnlyList<Vec2> pts, bool closed, double x, double y)
    {
        if (pts.Count == 0) return double.MaxValue;
        if (pts.Count == 1) return Hypot(pts[0].X - x, pts[0].Y - y);
        double best = double.MaxValue;
        for (int i = 1; i < pts.Count; i++)
            best = Math.Min(best, Segment(pts[i - 1].X, pts[i - 1].Y, pts[i].X, pts[i].Y, x, y));
        if (closed)
            best = Math.Min(best, Segment(pts[^1].X, pts[^1].Y, pts[0].X, pts[0].Y, x, y));
        return best;
    }

    private static Vec2[] EllipseSamples(DwgEntity e)
    {
        const int n = 64;
        double a = Math.Max(1e-9, Math.Abs(e.Radius));
        double b = Math.Max(1e-9, a * (Math.Abs(e.WidthFactor) > 1e-9 ? Math.Abs(e.WidthFactor) : 1.0));
        double theta = Math.Atan2(e.Start.Y, e.Start.X);
        double cosT = Math.Cos(theta), sinT = Math.Sin(theta);
        double start = e.StartAngle, end = e.EndAngle;
        if (end < start) end += Math.PI * 2;
        if (end - start < 1e-6) { start = 0; end = Math.PI * 2; }

        var pts = new Vec2[n + 1];
        for (int i = 0; i <= n; i++)
        {
            double t = start + (end - start) * i / n;
            double ct = Math.Cos(t), st = Math.Sin(t);
            pts[i] = new Vec2(e.Center.X + a * ct * cosT - b * st * sinT,
                              e.Center.Y + a * ct * sinT + b * st * cosT);
        }
        return pts;
    }
}
