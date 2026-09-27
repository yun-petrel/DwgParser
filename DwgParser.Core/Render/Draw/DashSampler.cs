using DwgParser.Core.Objects;

namespace DwgParser.Core.Render;

public static class DashSampler
{
    public static double CenteredPhase(double[]? pattern, double totalLen)
    {
        if (pattern is not { Length: > 0 } || totalLen <= 0) return 0;
        double period = 0;
        for (int i = 0; i < pattern.Length; i++) period += Math.Abs(pattern[i]);
        if (period <= 0) return 0;
        double mid = PositiveMod(totalLen / 2, period);
        double best = period, acc = 0;
        for (int i = 0; i < pattern.Length; i++)
        {
            double len = Math.Abs(pattern[i]);
            double d = PositiveMod(acc + len / 2 - mid, period);
            if (d < best) best = d;
            acc += len;
        }
        return best < period ? best : 0;
    }

    private static double PositiveMod(double v, double m)
    {
        double r = v % m;
        return r < 0 ? r + m : r;
    }

    public struct Phase
    {
        public int Index;
        public double Remaining;

        public static Phase Start(double[] pattern)
            => new() { Index = 0, Remaining = pattern.Length == 0 ? 0 : Math.Abs(pattern[0]) };

        public static Phase Start(double[] pattern, double offset)
        {
            var ph = Start(pattern);
            if (pattern.Length == 0 || offset <= 0) return ph;
            double d = offset;
            int guard = 0;
            while (d > 0 && guard++ < pattern.Length * 4 + 64)
            {
                if (ph.Remaining <= 1e-12)
                {
                    ph.Index++;
                    ph.Remaining = Math.Abs(pattern[ph.Index % pattern.Length]);
                    continue;
                }
                if (d < ph.Remaining) { ph.Remaining -= d; break; }
                d -= ph.Remaining;
                ph.Index++;
                ph.Remaining = Math.Abs(pattern[ph.Index % pattern.Length]);
            }
            return ph;
        }
    }

    public static void Split(Vec2 a, Vec2 b, double[] pattern, List<(Vec2 A, Vec2 B)> into)
    {
        var ph = Phase.Start(pattern);
        Split(a, b, pattern, ref ph, into);
    }

    public static void Split(Vec2 a, Vec2 b, double[] pattern, ref Phase phase, List<(Vec2 A, Vec2 B)> into)
    {
        double dx = b.X - a.X, dy = b.Y - a.Y;
        double len = Math.Sqrt(dx * dx + dy * dy);
        if (len <= 1e-12 || pattern.Length == 0) { into.Add((a, b)); return; }

        double ux = dx / len, uy = dy / len;
        double pos = 0;
        int guard = 0;
        while (pos < len - 1e-12 && guard++ < 100000)
        {
            if (phase.Remaining <= 1e-12)
            {
                phase.Index++;
                phase.Remaining = Math.Abs(pattern[phase.Index % pattern.Length]);
                continue;
            }
            double take = Math.Min(phase.Remaining, len - pos);
            if (pattern[phase.Index % pattern.Length] > 0)
            {
                var s = new Vec2(a.X + ux * pos, a.Y + uy * pos);
                var e = new Vec2(a.X + ux * (pos + take), a.Y + uy * (pos + take));
                into.Add((s, e));
            }
            pos += take;
            phase.Remaining -= take;
        }
    }

    public static double PolylineLength(ReadOnlySpan<Vec2> pts)
    {
        double sum = 0;
        for (int i = 1; i < pts.Length; i++)
        {
            double dx = pts[i].X - pts[i - 1].X, dy = pts[i].Y - pts[i - 1].Y;
            sum += Math.Sqrt(dx * dx + dy * dy);
        }
        return sum;
    }

    public static double PathLength(ReadOnlySpan<PathSeg> segs)
    {
        double sum = 0;
        foreach (var seg in segs)
        {
            double dx = seg.P2.X - seg.P1.X, dy = seg.P2.Y - seg.P1.Y;
            double chord = Math.Sqrt(dx * dx + dy * dy);
            if (chord <= 0) continue;
            if (seg.Kind == PathSegKind.Line) { sum += chord; continue; }
            if (seg.Kind != PathSegKind.Arc || seg.Bulge == 0) { sum += chord; continue; }
            double theta = 4 * Math.Atan(seg.Bulge);
            double r = chord / (2 * Math.Sin(theta / 2));
            sum += Math.Abs(r * theta);
        }
        return sum;
    }
}
