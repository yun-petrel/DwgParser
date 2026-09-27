using DwgParser.Core.Objects;

namespace DwgParser.Core.Render;

public static class DimensionRenderer
{
    public const double ArrowLen = 2.5;
    public const double ArrowWidthRatio = 1.0 / 3.0;
    public const double ExtGap = 0.6;
    public const double ExtBeyond = 1.25;

    public sealed class DimShape
    {
        public List<(Vec2 A, Vec2 B)> Lines { get; } = new();
        public List<(Vec2 Tip, Vec2 Dir)> Arrows { get; } = new();
        public Vec2 TextPos { get; set; }
        public double TextRotation { get; set; }
        public short TextAttachmentPoint { get; set; }
        public string Text { get; set; } = "";
        public bool HasText { get; set; }
        public double ArrowLength { get; set; } = ArrowLen;
        public double TextHeight { get; set; }
    }

    public static Exception? LastError { get; private set; }

    public static DimShape? Plan(DwgEntity e) => Plan(e, null, null);

    public static DimShape? Plan(DwgEntity e, DwgObjectReader.DimStyleInfo? style)
        => Plan(e, style, null);

    public static DimShape? Plan(DwgEntity e, DwgObjectReader.DimStyleInfo? style,
        IReadOnlyList<IEntity>? blockDisplay)
    {
        try
        {
            LastError = null;
            var shape = e.DimSubtype switch
            {
                "Linear" => PlanLinearAligned(e, style, useRotation: true),
                "Aligned" => PlanLinearAligned(e, style, useRotation: false),
                "Radial" => PlanRadial(e, style),
                "Diametric" => PlanDiametric(e, style, blockDisplay),
                "Ang3pt" => PlanAngular(e, style),
                "Ang2ln" => PlanAng2ln(e, style),
                "Ordinate" => PlanOrdinate(e, style),
                _ => null,
            };
            if (shape is not null)
            {
                shape.TextAttachmentPoint = e is IDimension dim ? dim.TextAttachment : (short)0;
            }
            if (shape is not null && StyleMetrics(style) is { } mt)
            {
                shape.ArrowLength = mt.Arrow;
                if (shape.TextHeight <= 0 && mt.Text > 0) shape.TextHeight = mt.Text;
            }
            if (shape != null)
            {
                var (text, height) = TryBlockText(blockDisplay);
                if (height > 0) shape.TextHeight = height;
                if (text != null && string.IsNullOrEmpty(e.TextValue))
                {
                    shape.Text = text;
                    shape.HasText = true;
                }
                TryAdoptBlockText(shape, blockDisplay, e);
            }
            return shape;
        }
        catch (Exception ex)
        {
            LastError = ex;
            return null;
        }
    }

    private static (double Arrow, double ExtGap, double ExtBeyond, double Text)? StyleMetrics(
        DwgObjectReader.DimStyleInfo? s)
    {
        if (s is not { Decoded: true }) return null;
        double sc = s.Dimscale > 0 ? s.Dimscale : 1.0;
        return (s.Dimasz * sc, s.Dimexo * sc, s.Dimexe * sc, s.Dimtxt * sc);
    }

    private static (string? Text, double Height) TryBlockText(IReadOnlyList<IEntity>? blockDisplay)
    {
        var d = FirstBlockText(blockDisplay);
        return d is null ? (null, 0) : (d.TextValue, d.Height);
    }

    private static DwgEntity? FirstBlockText(IReadOnlyList<IEntity>? blockDisplay)
    {
        if (blockDisplay == null) return null;
        foreach (var be in blockDisplay)
        {
            if (be is not DwgEntity d) continue;
            if (d.Kind != DwgEntityType.MText && d.Kind != DwgEntityType.Text) continue;
            if (string.IsNullOrEmpty(d.TextValue)) continue;
            return d;
        }
        return null;
    }

    private static void TryAdoptBlockText(DimShape shape, IReadOnlyList<IEntity>? blockDisplay, DwgEntity e)
    {
        if (blockDisplay == null || e.Points.Count < 2) return;
        if (FirstBlockText(blockDisplay) is not { Kind: DwgEntityType.MText, TextAttachment: >= 1 and <= 9 } bt)
            return;
        double h = bt.Height > 0 ? bt.Height : shape.TextHeight;
        if (h <= 0 || Dist(bt.Start, shape.TextPos) > BlockTextAdoptMaxTextHeights * h) return;
        double tol = Math.Max(1e-6, Dist(e.Points[0], e.Points[1]) * 1e-4);
        if (!BlockAnchoredAt(blockDisplay, e.Points[0], e.Points[1], tol)) return;
        shape.TextPos = bt.Start;
        shape.TextRotation = NormalizeReadable(bt.Rotation);
        shape.TextAttachmentPoint = bt.TextAttachment;
    }

    private const double BlockTextAdoptMaxTextHeights = 4;

    private static bool BlockAnchoredAt(IReadOnlyList<IEntity> blockDisplay, Vec2 a, Vec2 b, double tol)
    {
        bool hitA = false, hitB = false;
        foreach (var be in blockDisplay)
        {
            if (be is not ILine ln) continue;
            if (!hitA && (Dist(ln.Start, a) <= tol || Dist(ln.End, a) <= tol)) hitA = true;
            if (!hitB && (Dist(ln.Start, b) <= tol || Dist(ln.End, b) <= tol)) hitB = true;
            if (hitA && hitB) return true;
        }
        return false;
    }


    public static string FormatMeasurement(double v)
    {
        if (!double.IsFinite(v)) return "";
        string s = v.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
        return s;
    }

    public static double NormalizeReadable(double a)
    {
        while (a > Math.PI / 2 + ReadableEps) a -= Math.PI;
        while (a <= -Math.PI / 2 + ReadableEps) a += Math.PI;
        return a;
    }

    private const double ReadableEps = 1e-9;

    private static string ResolveText(DwgEntity e, string prefix, DwgObjectReader.DimStyleInfo? style)
    {
        string raw = e.TextValue ?? "";
        string main = raw.Length == 0 ? prefix + FormatMeasurement(e.Measurement)
            : raw.Contains("<>") ? raw.Replace("<>", prefix + FormatMeasurement(e.Measurement))
            : raw;
        return main + FormatAlt(e, prefix, style);
    }

    private static string FormatAlt(DwgEntity e, string prefix, DwgObjectReader.DimStyleInfo? st)
    {
        if (st is null || st.Dimalt == 0) return "";
        double alt = e.Measurement * st.Dimaltf;
        if (!double.IsFinite(alt)) return "";
        if (st.Dimaltrnd > 0) alt = Math.Round(alt / st.Dimaltrnd) * st.Dimaltrnd;
        int dec = st.TailDecoded ? st.Dimadec : st.Dimaltd;
        if (dec < 0) dec = 0; else if (dec > 8) dec = 2;
        string num = alt.ToString("F" + dec, System.Globalization.CultureInfo.InvariantCulture);
        string ap = st.Dimapost ?? "";
        string pre = ap, suf = "";
        int plus = ap.IndexOf('+');
        if (plus >= 0) { pre = ap.Substring(0, plus); suf = ap.Substring(plus + 1); }
        return " (" + pre + prefix + num + suf + ")";
    }

    private static Vec2 Unit(Vec2 v)
    {
        double len = Math.Sqrt(v.X * v.X + v.Y * v.Y);
        return len < 1e-12 ? new Vec2(0, 0) : new Vec2(v.X / len, v.Y / len);
    }

    private static double Dist(Vec2 a, Vec2 b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));


    private static DimShape? PlanLinearAligned(DwgEntity e, DwgObjectReader.DimStyleInfo? style, bool useRotation)
    {
        if (e.Points.Count < 3) return null;
        Vec2 x1 = e.Points[0], x2 = e.Points[1], def = e.Points[2];

        Vec2 d = new Vec2(x2.X - x1.X, x2.Y - x1.Y);
        double span = Math.Sqrt(d.X * d.X + d.Y * d.Y);
        if (span < 1e-9) return null;

        Vec2 dir;
        double m = Math.Abs(e.Measurement);
        double ratio = m / span;
        if (ratio >= 1.0 - 1e-9)
        {
            dir = new Vec2(d.X / span, d.Y / span);
        }
        else if (ratio < 1e-9)
        {
            return null;
        }
        else
        {
            double phi = Math.Atan2(d.Y, d.X);
            double delta = Math.Acos(ratio);
            dir = PickMeasurementDir(def, x1, x2, phi + delta, phi - delta);
        }
        Vec2 perp = new Vec2(-dir.Y, dir.X);

        Vec2 Foot(Vec2 p)
        {
            double t = (p.X - def.X) * dir.X + (p.Y - def.Y) * dir.Y;
            return new Vec2(def.X + dir.X * t, def.Y + dir.Y * t);
        }
        Vec2 f1 = Foot(x1), f2 = Foot(x2);
        double len = Dist(f1, f2);
        if (len < 1e-9 || !double.IsFinite(len)) return null;

        var shape = new DimShape();
        var mt = StyleMetrics(style);
        double extGap = mt?.ExtGap ?? ExtGap, extBeyond = mt?.ExtBeyond ?? ExtBeyond;
        foreach (var (p, f) in new[] { (x1, f1), (x2, f2) })
        {
            Vec2 u = Unit(new Vec2(f.X - p.X, f.Y - p.Y));
            if (u.X == 0 && u.Y == 0) continue;
            double gap = Dist(p, f) > extGap + extBeyond ? extGap : 0.0;
            shape.Lines.Add((new Vec2(p.X + u.X * gap, p.Y + u.Y * gap),
                             new Vec2(f.X + u.X * extBeyond, f.Y + u.Y * extBeyond)));
        }

        double arrow = Math.Clamp(mt?.Arrow ?? ArrowLen, 0.4, len * 0.35);
        Vec2 nrm = perp;
        if (len >= 2.2 * arrow)
        {
            shape.Lines.Add((f1, f2));
            shape.Arrows.Add((f1, new Vec2(-dir.X, -dir.Y)));
            shape.Arrows.Add((f2, dir));
        }
        else
        {
            double ext = arrow * 1.6;
            shape.Lines.Add((new Vec2(f1.X - dir.X * ext, f1.Y - dir.Y * ext),
                             new Vec2(f2.X + dir.X * ext, f2.Y + dir.Y * ext)));
            shape.Arrows.Add((f1, dir));
            shape.Arrows.Add((f2, new Vec2(-dir.X, -dir.Y)));
        }

        shape.Text = ResolveText(e, "", style);
        shape.HasText = shape.Text.Length > 0;
        shape.TextPos = e.DimTextMid.X != 0 || e.DimTextMid.Y != 0
            ? e.DimTextMid
            : new Vec2((f1.X + f2.X) / 2 + nrm.X * 1.5, (f1.Y + f2.Y) / 2 + nrm.Y * 1.5);
        shape.TextRotation = NormalizeReadable(Math.Atan2(dir.Y, dir.X));
        return shape;
    }

    private static Vec2 PickMeasurementDir(Vec2 def, Vec2 x1, Vec2 x2, double t1, double t2)
    {
        double best = double.MaxValue;
        Vec2 bestDir = new Vec2(1, 0);
        foreach (double th in new[] { t1, t2 })
        {
            var dir = new Vec2(Math.Cos(th), Math.Sin(th));
            Vec2 Foot(Vec2 p)
            {
                double t = (p.X - def.X) * dir.X + (p.Y - def.Y) * dir.Y;
                return new Vec2(def.X + dir.X * t, def.Y + dir.Y * t);
            }
            var f1 = Foot(x1);
            var f2 = Foot(x2);
            double len2 = Dist(f1, f2);
            double dist;
            if (len2 < 1e-9) dist = Dist(def, f1);
            else
            {
                double s = ((def.X - f1.X) * (f2.X - f1.X) + (def.Y - f1.Y) * (f2.Y - f1.Y)) / (len2 * len2);
                s = Math.Clamp(s, 0.0, 1.0);
                var q = new Vec2(f1.X + (f2.X - f1.X) * s, f1.Y + (f2.Y - f1.Y) * s);
                dist = Dist(def, q);
            }
            if (dist < best) { best = dist; bestDir = dir; }
        }
        return bestDir;
    }


    private static DimShape? PlanRadial(DwgEntity e, DwgObjectReader.DimStyleInfo? style)
    {
        if (e.Points.Count < 2) return null;
        Vec2 center = e.Points[0], arc = e.Points[1];
        Vec2 dir = Unit(new Vec2(arc.X - center.X, arc.Y - center.Y));
        if (dir.X == 0 && dir.Y == 0) return null;

        var shape = new DimShape();
        shape.Lines.Add((center, arc));
        shape.Arrows.Add((arc, dir));
        shape.Text = ResolveText(e, "R", style);
        shape.HasText = shape.Text.Length > 0;
        shape.TextPos = e.DimTextMid.X != 0 || e.DimTextMid.Y != 0
            ? e.DimTextMid
            : new Vec2((center.X + arc.X) / 2, (center.Y + arc.Y) / 2);
        shape.TextRotation = NormalizeReadable(Math.Atan2(dir.Y, dir.X));
        return shape;
    }

    private static DimShape? PlanDiametric(DwgEntity e, DwgObjectReader.DimStyleInfo? style,
        IReadOnlyList<IEntity>? blockDisplay)
    {
        if (e.Points.Count < 2) return null;
        Vec2 p1 = e.Points[0], p2 = e.Points[1];
        double len = Dist(p1, p2);
        if (len < 1e-9) return null;
        Vec2 dir = Unit(new Vec2(p2.X - p1.X, p2.Y - p1.Y));

        var shape = new DimShape();
        shape.Text = ResolveText(e, "Ø", style);
        shape.HasText = shape.Text.Length > 0;
        shape.TextPos = e.DimTextMid.X != 0 || e.DimTextMid.Y != 0
            ? e.DimTextMid
            : new Vec2((p1.X + p2.X) / 2, (p1.Y + p2.Y) / 2);
        shape.TextRotation = NormalizeReadable(Math.Atan2(dir.Y, dir.X));

        double along = (shape.TextPos.X - p1.X) * dir.X + (shape.TextPos.Y - p1.Y) * dir.Y;
        if (along > len + len * 1e-9 || along < -len * 1e-9)
        {
            bool textAtP2 = along > len / 2;
            Vec2 tip = textAtP2 ? p2 : p1;
            Vec2 far = textAtP2 ? p1 : p2;
            Vec2 outward = textAtP2 ? dir : new Vec2(-dir.X, -dir.Y);
            Vec2 inward = new Vec2(-outward.X, -outward.Y);
            shape.Arrows.Add((tip, inward));
            shape.Arrows.Add((far, outward));
            if (TryAdoptBlockForm(shape, blockDisplay, p1, p2, dir, len)) return shape;

            double arrow = StyleMetrics(style)?.Arrow ?? ArrowLen;
            double anchorOut = textAtP2 ? along - len : -along;
            shape.Lines.Add((p1, p2));
            if (anchorOut > arrow)
                shape.Lines.Add((Offset(tip, outward, arrow), Offset(tip, outward, anchorOut)));
            shape.Lines.Add((Offset(far, inward, arrow), Offset(far, inward, arrow * 2)));
            return shape;
        }

        shape.Lines.Add((p1, p2));
        shape.Arrows.Add((p1, new Vec2(-dir.X, -dir.Y)));
        shape.Arrows.Add((p2, dir));
        return shape;
    }

    private static Vec2 Offset(Vec2 p, Vec2 unit, double d) => new(p.X + unit.X * d, p.Y + unit.Y * d);

    private static bool TryAdoptBlockForm(DimShape shape, IReadOnlyList<IEntity>? blockDisplay,
        Vec2 p1, Vec2 p2, Vec2 dir, double len)
    {
        if (blockDisplay == null) return false;
        double tol = Math.Max(1e-6, len * 1e-4);
        double Perp(Vec2 q) => Math.Abs((q.X - p1.X) * dir.Y - (q.Y - p1.Y) * dir.X);
        bool chord = false;
        foreach (var be in blockDisplay)
        {
            if (be is not ILine ln) continue;
            if ((Dist(ln.Start, p1) <= tol && Dist(ln.End, p2) <= tol) ||
                (Dist(ln.Start, p2) <= tol && Dist(ln.End, p1) <= tol)) { chord = true; break; }
        }
        if (!chord) return false;
        int taken = 0;
        foreach (var be in blockDisplay)
        {
            if (be is not ILine ln) continue;
            if (Perp(ln.Start) > tol || Perp(ln.End) > tol) continue;
            if (Dist(ln.Start, ln.End) <= tol) continue;
            shape.Lines.Add((ln.Start, ln.End));
            taken++;
        }
        return taken > 0;
    }


    private static void FillAngular(DimShape shape, Vec2 v, Vec2 x1, Vec2 x2,
        double a1, double a2, double ad, double r, DwgEntity e, DwgObjectReader.DimStyleInfo? style)
    {
        while (a1 - ad > Math.PI) a1 -= 2 * Math.PI;
        while (a1 - ad < -Math.PI) a1 += 2 * Math.PI;
        while (a2 - ad > Math.PI) a2 -= 2 * Math.PI;
        while (a2 - ad < -Math.PI) a2 += 2 * Math.PI;
        double lo = Math.Min(a1, a2), hi = Math.Max(a1, a2);
        if (ad > hi) hi += 2 * Math.PI;
        else if (ad < lo) lo -= 2 * Math.PI;
        if (hi - lo < 1e-9 || hi - lo > 2 * Math.PI - 1e-9) return;

        const int seg = 48;
        Vec2 prev = new Vec2(v.X + r * Math.Cos(lo), v.Y + r * Math.Sin(lo));
        for (int i = 1; i <= seg; i++)
        {
            double a = lo + (hi - lo) * i / seg;
            var p = new Vec2(v.X + r * Math.Cos(a), v.Y + r * Math.Sin(a));
            shape.Lines.Add((prev, p));
            prev = p;
        }

        Vec2 e1 = new Vec2(v.X + r * Math.Cos(lo), v.Y + r * Math.Sin(lo));
        Vec2 e2 = new Vec2(v.X + r * Math.Cos(hi), v.Y + r * Math.Sin(hi));
        var mt = StyleMetrics(style);
        double extGap = mt?.ExtGap ?? ExtGap, extBeyond = mt?.ExtBeyond ?? ExtBeyond;
        foreach (var (p, ep) in new[] { (x1, e1), (x2, e2) })
        {
            Vec2 u = Unit(new Vec2(ep.X - p.X, ep.Y - p.Y));
            if (u.X == 0 && u.Y == 0) continue;
            double gap = Dist(p, ep) > extGap + extBeyond ? extGap : 0.0;
            shape.Lines.Add((new Vec2(p.X + u.X * gap, p.Y + u.Y * gap),
                             new Vec2(ep.X + u.X * extBeyond, ep.Y + u.Y * extBeyond)));
        }

        Vec2 t1 = new Vec2(-Math.Sin(lo), Math.Cos(lo));
        Vec2 t2 = new Vec2(-Math.Sin(hi), Math.Cos(hi));
        shape.Arrows.Add((e1, new Vec2(-t1.X, -t1.Y)));
        shape.Arrows.Add((e2, t2));

        shape.Text = ResolveText(e, "", style);
        shape.HasText = shape.Text.Length > 0;
        shape.TextPos = e.DimTextMid.X != 0 || e.DimTextMid.Y != 0
            ? e.DimTextMid
            : new Vec2(v.X + r * Math.Cos(ad), v.Y + r * Math.Sin(ad));
        Vec2 tanMid = new Vec2(-Math.Sin(ad), Math.Cos(ad));
        shape.TextRotation = NormalizeReadable(Math.Atan2(tanMid.Y, tanMid.X));
    }

    private static DimShape? PlanAngular(DwgEntity e, DwgObjectReader.DimStyleInfo? style)
    {
        if (e.Points.Count < 4) return null;
        Vec2 def = e.Points[0], x1 = e.Points[1], x2 = e.Points[2], cen = e.Points[3];
        double r = Dist(def, cen);
        if (r < 1e-9) return null;

        var shape = new DimShape();
        FillAngular(shape, cen, x1, x2,
            Math.Atan2(x1.Y - cen.Y, x1.X - cen.X),
            Math.Atan2(x2.Y - cen.Y, x2.X - cen.X),
            Math.Atan2(def.Y - cen.Y, def.X - cen.X), r, e, style);
        return shape.Lines.Count > 0 ? shape : null;
    }

    private static DimShape? PlanAng2ln(DwgEntity e, DwgObjectReader.DimStyleInfo? style)
    {
        if (e.Points.Count < 4) return null;
        Vec2 a = e.Points[0], b = e.Points[1], c = e.Points[2], d = e.Points[3];
        Vec2? inter = LineIntersect(a, b, c, d);
        if (inter is not { } v) return null;

        Vec2 x1 = Dist(a, v) > Dist(b, v) ? a : b;
        Vec2 x2 = Dist(c, v) > Dist(d, v) ? c : d;
        double r = Math.Max(Dist(x1, v), Dist(x2, v)) * 0.7;
        if (r < 1e-9) return null;

        var shape = new DimShape();
        Vec2 adir = e.DimTextMid.X != 0 || e.DimTextMid.Y != 0
            ? e.DimTextMid
            : new Vec2((x1.X + x2.X) / 2, (x1.Y + x2.Y) / 2);
        double ad = Math.Atan2(adir.Y - v.Y, adir.X - v.X);
        FillAngular(shape, v, x1, x2,
            Math.Atan2(x1.Y - v.Y, x1.X - v.X),
            Math.Atan2(x2.Y - v.Y, x2.X - v.X), ad, r, e, style);
        return shape.Lines.Count > 0 ? shape : null;
    }

    private static Vec2? LineIntersect(Vec2 a, Vec2 b, Vec2 c, Vec2 d)
    {
        double d1x = b.X - a.X, d1y = b.Y - a.Y;
        double d2x = d.X - c.X, d2y = d.Y - c.Y;
        double den = d1x * d2y - d1y * d2x;
        if (Math.Abs(den) < 1e-12) return null;
        double t = ((c.X - a.X) * d2y - (c.Y - a.Y) * d2x) / den;
        return new Vec2(a.X + d1x * t, a.Y + d1y * t);
    }


    private static DimShape? PlanOrdinate(DwgEntity e, DwgObjectReader.DimStyleInfo? style)
    {
        if (e.Points.Count < 3) return null;
        Vec2 origin = e.Points[0], feat = e.Points[1], lead = e.Points[2];

        var shape = new DimShape();
        shape.Lines.Add((origin, feat));
        shape.Lines.Add((feat, lead));
        shape.Text = ResolveText(e, "", style);
        shape.HasText = shape.Text.Length > 0;
        shape.TextPos = e.DimTextMid.X != 0 || e.DimTextMid.Y != 0 ? e.DimTextMid : lead;
        shape.TextRotation = NormalizeReadable(Math.Atan2(lead.Y - feat.Y, lead.X - feat.X));
        return shape;
    }
}
