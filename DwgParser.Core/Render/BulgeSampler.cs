using DwgParser.Core.Objects;

namespace DwgParser.Core.Render;

internal static class BulgeSampler
{
    public static List<Vec2> Sample(Vec2 p1, Vec2 p2, double bulge)
    {
        var pts = new List<Vec2>();
        if (Math.Abs(bulge) < 1e-9)
        {
            pts.Add(p1);
            pts.Add(p2);
            return pts;
        }

        double x1 = p1.X, y1 = -p1.Y;
        double x2 = p2.X, y2 = -p2.Y;
        double dx = (x1 - x2) / 2, dy = (y1 - y2) / 2;
        double chord = Math.Sqrt((x2 - x1) * (x2 - x1) + (y2 - y1) * (y2 - y1));
        if (chord < 1e-9)
        {
            pts.Add(p1);
            pts.Add(p2);
            return pts;
        }

        double theta = 4 * Math.Atan(bulge);
        double r = chord * (1 + bulge * bulge) / (4 * Math.Abs(bulge));
        bool large = Math.Abs(theta) > Math.PI;
        bool sweep = bulge < 0;

        double c2 = dx * dx + dy * dy;
        double lam = c2 > 0 ? Math.Sqrt(Math.Max(0, r * r - c2) / c2) : 0;
        int coef = sweep == large ? -1 : 1;
        double cx = (x1 + x2) / 2 + coef * lam * dy;
        double cy = (y1 + y2) / 2 - coef * lam * dx;

        double a1 = Math.Atan2(y1 - cy, x1 - cx);
        double a2 = Math.Atan2(y2 - cy, x2 - cx);
        double delta = a2 - a1;
        if (sweep)
        {
            while (delta < 0) delta += Math.PI * 2;
            while (delta > Math.PI * 2) delta -= Math.PI * 2;
        }
        else
        {
            while (delta > 0) delta -= Math.PI * 2;
            while (delta < -Math.PI * 2) delta += Math.PI * 2;
        }
        if (Math.Abs(delta) < 1e-9)
        {
            pts.Add(p1);
            pts.Add(p2);
            return pts;
        }

        int steps = Math.Max(8, (int)(Math.Abs(delta) / (Math.PI / 90)));
        for (int i = 0; i <= steps; i++)
        {
            double a = a1 + delta * i / steps;
            pts.Add(new Vec2(cx + r * Math.Cos(a), -(cy + r * Math.Sin(a))));
        }
        return pts;
    }
}
