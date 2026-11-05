namespace DwgParser.Core.Render;

internal static class WorldTransform
{
    internal static (Func<double, double> Tx, Func<double, double> Ty, double scale)
        Make(double minX, double minY, double maxX, double maxY, int width, int height, int marginPx)
    {
        int margin = marginPx;
        double worldW = maxX - minX;
        double worldH = maxY - minY;
        double drawW = width - 2 * margin;
        double drawH = height - 2 * margin;
        double scale = Math.Min(drawW / worldW, drawH / worldH);
        double offsetX = margin + (drawW - worldW * scale) / 2;
        double offsetY = margin + (drawH - worldH * scale) / 2;
        double ox = minX, oy = minY;
        int h = height;
        Func<double, double> Tx = (wx) => offsetX + (wx - ox) * scale;
        Func<double, double> Ty = (wy) => h - (offsetY + (wy - oy) * scale);
        return (Tx, Ty, scale);
    }
}

internal static class PxMath
{
    internal static int ClampPx(double v)
    {
        if (double.IsNaN(v) || double.IsInfinity(v)) return 0;
        if (v > 100_000_000) return 100_000_000;
        if (v < -100_000_000) return -100_000_000;
        return (int)Math.Round(v, MidpointRounding.AwayFromZero);
    }

    internal static double ClampPxKeep(double v)
    {
        if (double.IsNaN(v) || double.IsInfinity(v)) return 0;
        if (v > 100_000_000) return 100_000_000;
        if (v < -100_000_000) return -100_000_000;
        return v;
    }

    internal static (byte r, byte g, byte b) ParseColor(string hex)
    {
        if (string.IsNullOrEmpty(hex) || hex.Length < 7) return (0, 0, 0);
        return ((byte)Convert.ToInt32(hex.Substring(1, 2), 16),
                (byte)Convert.ToInt32(hex.Substring(3, 2), 16),
                (byte)Convert.ToInt32(hex.Substring(5, 2), 16));
    }
}
