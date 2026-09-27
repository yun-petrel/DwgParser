namespace DwgParser.Core.Objects;

using System;
using System.Collections.Generic;
using DwgParser.Core.Render;

internal static class HatchGeometry
{
    private const int MaxRings = 256;
    private const int MaxRingPoints = 8000;
    private const int MaxLines = 4000;
    private const int MaxSegments = 50000;
    private const double Eps = 1e-9;

    internal const double SolidFillOpacity = 1.0;

    internal const byte SolidFillAlpha = 255;

    public static List<Vec2[]> BoundaryRings(HatchEntity h)
    {
        var rings = new List<Vec2[]>();
        foreach (var (start, count) in h.PathSegmentRanges)
        {
            if (count == 0) continue;
            var pts = new List<Vec2>();
            bool bad = false;
            for (int i = start; i < start + count && !bad; i++)
            {
                var s = h.Segments[i];
                if (s.IsPolylinePath) AddPolyline(pts, s);
                else switch (s.CurveType)
                {
                    case 1: pts.Add(s.P1); pts.Add(s.P2); break;
                    case 2: SampleArc(pts, s.Center, s.Radius, s.StartAngle, s.EndAngle); break;
                    case 3: SampleEllipse(pts, s); break;
                    default: bad = true; break;
                }
                if (pts.Count > MaxRingPoints) bad = true;
            }
            if (bad) { rings.Clear(); return rings; }
            var ring = Close(pts);
            if (ring.Length < 3) continue;
            rings.Add(ring);
            if (rings.Count > MaxRings) { rings.Clear(); return rings; }
        }
        return rings;
    }

    public static List<(Vec2 A, Vec2 B)> PatternSegments(HatchEntity h)
    {
        var segs = new List<(Vec2 A, Vec2 B)>();
        if (h.SolidFill || h.PatternLines.Count == 0) return segs;
        double ang = h.PatternAngle ?? 0, scale = h.PatternScale ?? 1;
        if (!Fin(ang, scale) || scale <= 0) return segs;
        var rings = BoundaryRings(h);
        if (rings.Count == 0) return segs;
        if (h.Style == 1) rings = OutermostOnly(rings);
        if (rings.Count == 0) return segs;

        double cos = Math.Cos(ang), sin = Math.Sin(ang);
        var bb = BoundsOf(rings);
        foreach (var (lineAngle, basePat, offPat, dashes) in h.PatternLines)
        {
            if (!Fin(lineAngle, basePat.X, basePat.Y, offPat.X, offPat.Y)) continue;
            double phi = ang + lineAngle, dx = Math.Cos(phi), dy = Math.Sin(phi);
            var dir = new Vec2(dx, dy);
            var b = Rot(Mul(basePat, scale), cos, sin);
            var o = Rot(Mul(offPat, scale), cos, sin);
            double denom = Cross(o, dir), stagger = o.X * dx + o.Y * dy;

            if (Math.Abs(denom) < Eps)
                EmitLine(segs, h.Style, rings, bb, b, dir, 0, dashes, scale);
            else
            {
                double n0 = Math.Min(Math.Min(Nat(bb.MinX, bb.MinY, b, dir, denom), Nat(bb.MaxX, bb.MinY, b, dir, denom)),
                                     Math.Min(Nat(bb.MinX, bb.MaxY, b, dir, denom), Nat(bb.MaxX, bb.MaxY, b, dir, denom)));
                double n1 = Math.Max(Math.Max(Nat(bb.MinX, bb.MinY, b, dir, denom), Nat(bb.MaxX, bb.MinY, b, dir, denom)),
                                     Math.Max(Nat(bb.MinX, bb.MaxY, b, dir, denom), Nat(bb.MaxX, bb.MaxY, b, dir, denom)));
                if (!(n1 >= n0) || !Fin(n0, n1) || n1 - n0 > MaxLines) { segs.Clear(); return segs; }
                for (long n = (long)Math.Ceiling(n0); n <= (long)Math.Floor(n1); n++)
                {
                    EmitLine(segs, h.Style, rings, bb, Add(b, Mul(o, (double)n)), dir, n * stagger, dashes, scale);
                    if (segs.Count > MaxSegments) { segs.Clear(); return segs; }
                }
            }
        }
        return segs;
    }


    private static void EmitLine(List<(Vec2 A, Vec2 B)> segs, long style, List<Vec2[]> rings,
        Rect bb, Vec2 origin, Vec2 dir, double phase, List<double> dashes, double scale)
    {
        var (sLo, sHi) = SpanOnLine(bb, origin, dir);
        var xs = new List<double> { sLo, sHi };
        foreach (var ring in rings)
            for (int i = 0, j = ring.Length - 1; i < ring.Length; j = i++)
            {
                var e = Sub(ring[i], ring[j]);
                double den = Cross(dir, e);
                if (Math.Abs(den) < Eps) continue;
                var w = Sub(ring[j], origin);
                double t = Cross(w, dir) / den;
                if (t < 0 || t > 1) continue;
                double s = Cross(w, e) / den;
                if (s > sLo && s < sHi) xs.Add(s);
            }
        xs.Sort();
        var spans = new List<(double A, double B)>();
        for (int i = 0; i + 1 < xs.Count; i++)
        {
            double a = xs[i], b2 = xs[i + 1];
            if (b2 - a <= Eps) continue;
            double mid = (a + b2) / 2;
            var p = Add(origin, Mul(dir, mid));
            if (!Inside(style, rings, p)) continue;
            if (spans.Count > 0 && spans[^1].B >= a - Eps) spans[^1] = (spans[^1].A, Math.Max(spans[^1].B, b2));
            else spans.Add((a, b2));
        }
        foreach (var (a, b2) in spans)
            foreach (var (da, db) in Dash(a + phase, b2 + phase, dashes, scale))
                segs.Add((Add(origin, Mul(dir, da)), Add(origin, Mul(dir, db))));
    }

    private static IEnumerable<(double A, double B)> Dash(double from, double to, List<double> dashes, double scale)
    {
        double cycle = 0;
        foreach (var d in dashes) cycle += Math.Abs(d) * scale;
        if (dashes.Count == 0 || !(cycle > Eps)) { yield return (from, to); yield break; }

        double pos = Math.Floor(from / cycle) * cycle;
        int idx = 0;
        for (int guard = 0; pos < to && guard < 200000; guard++, idx = (idx + 1) % dashes.Count)
        {
            double raw = dashes[idx], len = Math.Abs(raw) * scale, next = pos + len;
            if (raw > 0)
            {
                double a = Math.Max(pos, from), b2 = Math.Min(next, to);
                if (b2 - a > Eps) yield return (a, b2);
            }
            if (len <= Eps) continue;
            pos = next;
        }
    }


    private static bool Inside(long style, List<Vec2[]> rings, Vec2 p)
    {
        int parity = 0;
        foreach (var ring in rings)
            if (InRing(ring, p))
            {
                if (style == 2) return true;
                parity++;
            }
        return (parity & 1) == 1;
    }

    private static bool InRing(Vec2[] ring, Vec2 p)
    {
        bool inside = false;
        for (int i = 0, j = ring.Length - 1; i < ring.Length; j = i++)
        {
            var a = ring[i]; var b = ring[j];
            if ((a.Y > p.Y) != (b.Y > p.Y)
                && p.X < (b.X - a.X) * (p.Y - a.Y) / (b.Y - a.Y) + a.X) inside = !inside;
        }
        return inside;
    }

    private static List<Vec2[]> OutermostOnly(List<Vec2[]> rings)
    {
        var area = new double[rings.Count];
        for (int i = 0; i < rings.Count; i++) area[i] = Math.Abs(SignedArea(rings[i]));
        var keep = new List<Vec2[]>();
        for (int i = 0; i < rings.Count; i++)
        {
            bool nested = false;
            for (int j = 0; j < rings.Count && !nested; j++)
                if (j != i && area[j] > area[i] && InRing(rings[j], rings[i][0])) nested = true;
            if (!nested) keep.Add(rings[i]);
        }
        return keep;
    }

    private static double SignedArea(Vec2[] r)
    {
        double a = 0;
        for (int i = 0, j = r.Length - 1; i < r.Length; j = i++) a += (r[j].X + r[i].X) * (r[j].Y - r[i].Y);
        return a / 2;
    }

    private static void AddPolyline(List<Vec2> pts, HatchBoundarySegment s)
    {
        var poly = s.PolylinePoints;
        if (poly.Length == 0) return;
        if (!s.BulgesPresent || s.Bulges.Length != poly.Length) { pts.AddRange(poly); return; }
        int lim = s.IsClosed ? poly.Length : poly.Length - 1;
        for (int k = 0; k < lim; k++)
        {
            var arc = BulgeSampler.Sample(poly[k], poly[(k + 1) % poly.Length], s.Bulges[k]);
            for (int j = 0; j < arc.Count; j++) pts.Add(arc[j]);
        }
        if (!s.IsClosed) pts.Add(poly[^1]);
    }

    private static void SampleArc(List<Vec2> pts, Vec2 c, double r, double a1, double a2)
    {
        int n = Math.Max(4, (int)Math.Abs((a2 - a1) / 0.2));
        for (int k = 0; k <= n; k++)
        {
            double a = a1 + (a2 - a1) * k / n;
            pts.Add(new Vec2(c.X + r * Math.Cos(a), c.Y + r * Math.Sin(a)));
        }
    }

    private static void SampleEllipse(List<Vec2> pts, HatchBoundarySegment s)
    {
        var end = s.MajorAxis;
        double theta = Math.Atan2(end.Y, end.X);
        double ma = Math.Sqrt(end.X * end.X + end.Y * end.Y), mi = ma * s.Ratio;
        double cosT = Math.Cos(theta), sinT = Math.Sin(theta);
        int n = Math.Max(4, (int)Math.Abs((s.EndAngle - s.StartAngle) / 0.2));
        for (int k = 0; k <= n; k++)
        {
            double t = s.StartAngle + (s.EndAngle - s.StartAngle) * k / n;
            double ct = Math.Cos(t), st = Math.Sin(t);
            pts.Add(new Vec2(s.Center.X + ma * ct * cosT - mi * st * sinT,
                             s.Center.Y + ma * ct * sinT + mi * st * cosT));
        }
    }

    private static Vec2[] Close(List<Vec2> pts)
    {
        var r = new List<Vec2>(pts.Count);
        foreach (var p in pts)
        {
            if (!Fin(p.X, p.Y)) continue;
            if (r.Count > 0 && Math.Abs(r[^1].X - p.X) < Eps && Math.Abs(r[^1].Y - p.Y) < Eps) continue;
            r.Add(p);
        }
        if (r.Count > 1 && Math.Abs(r[0].X - r[^1].X) < Eps && Math.Abs(r[0].Y - r[^1].Y) < Eps)
            r.RemoveAt(r.Count - 1);
        return r.ToArray();
    }

    private static Vec2 Rot(Vec2 v, double cos, double sin) => new(v.X * cos - v.Y * sin, v.X * sin + v.Y * cos);
    private static Vec2 Mul(Vec2 v, double k) => new(v.X * k, v.Y * k);
    private static Vec2 Add(Vec2 a, Vec2 b) => new(a.X + b.X, a.Y + b.Y);
    private static Vec2 Sub(Vec2 a, Vec2 b) => new(a.X - b.X, a.Y - b.Y);
    private static double Cross(Vec2 a, Vec2 b) => a.X * b.Y - a.Y * b.X;

    private static double Nat(double qx, double qy, Vec2 b, Vec2 d, double denom)
        => ((qx - b.X) * d.Y - (qy - b.Y) * d.X) / denom;

    private static (double, double) SpanOnLine(Rect bb, Vec2 origin, Vec2 d)
    {
        double lo = double.PositiveInfinity, hi = double.NegativeInfinity;
        foreach (var c in new[] { new Vec2(bb.MinX, bb.MinY), new Vec2(bb.MaxX, bb.MinY),
                                  new Vec2(bb.MinX, bb.MaxY), new Vec2(bb.MaxX, bb.MaxY) })
        {
            double s = (c.X - origin.X) * d.X + (c.Y - origin.Y) * d.Y;
            if (s < lo) lo = s;
            if (s > hi) hi = s;
        }
        return (lo - 1, hi + 1);
    }

    private static Rect BoundsOf(List<Vec2[]> rings)
    {
        double mnx = double.PositiveInfinity, mny = double.PositiveInfinity,
               mxx = double.NegativeInfinity, mxy = double.NegativeInfinity;
        foreach (var r in rings)
            foreach (var p in r)
            {
                if (p.X < mnx) mnx = p.X;
                if (p.Y < mny) mny = p.Y;
                if (p.X > mxx) mxx = p.X;
                if (p.Y > mxy) mxy = p.Y;
            }
        return new Rect(mnx, mny, mxx, mxy);
    }

    private static bool Fin(params double[] vals)
    {
        for (int i = 0; i < vals.Length; i++) if (!double.IsFinite(vals[i])) return false;
        return true;
    }

    private readonly record struct Rect(double MinX, double MinY, double MaxX, double MaxY);
}
