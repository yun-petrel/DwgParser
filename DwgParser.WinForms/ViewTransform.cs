using System.Globalization;

namespace DwgParser.WinForms;

internal sealed class ViewTransform
{
    private const double MinScale = 1e-9;
    private const double MaxScale = 1e12;

    public double MinX { get; private set; }

    public double MinY { get; private set; }

    public double Scale { get; private set; } = 1.0;

    public int WidthPx { get; private set; } = 1;
    public int HeightPx { get; private set; } = 1;

    public double WorldWidth => WidthPx / Scale;
    public double WorldHeight => HeightPx / Scale;
    public double MaxX => MinX + WorldWidth;
    public double MaxY => MinY + WorldHeight;
    public double CenterX => MinX + WorldWidth / 2;
    public double CenterY => MinY + WorldHeight / 2;

    public double ScreenX(double worldX) => (worldX - MinX) * Scale;

    public double ScreenY(double worldY) => HeightPx - (worldY - MinY) * Scale;

    public (double X, double Y) ToWorld(double screenX, double screenY)
        => (MinX + screenX / Scale, MinY + (HeightPx - screenY) / Scale);

    public void Resize(int widthPx, int heightPx)
    {
        double cx = CenterX, cy = CenterY;
        WidthPx = Math.Max(1, widthPx);
        HeightPx = Math.Max(1, heightPx);
        Center(cx, cy);
    }

    public void Center(double cx, double cy)
    {
        MinX = cx - WorldWidth / 2;
        MinY = cy - WorldHeight / 2;
    }

    public void SetScale(double scale)
    {
        scale = Math.Clamp(scale, MinScale, MaxScale);
        double cx = CenterX, cy = CenterY;
        Scale = scale;
        Center(cx, cy);
    }

    public void Fit(double minX, double minY, double maxX, double maxY,
                    int widthPx, int heightPx, double marginFraction = 0.04)
    {
        WidthPx = Math.Max(1, widthPx);
        HeightPx = Math.Max(1, heightPx);

        if (!double.IsFinite(minX) || !double.IsFinite(minY)
            || !double.IsFinite(maxX) || !double.IsFinite(maxY))
        {
            minX = minY = -1; maxX = maxY = 1;
        }
        if (maxX - minX < 1e-9) { double c = (minX + maxX) / 2; minX = c - 0.5; maxX = c + 0.5; }
        if (maxY - minY < 1e-9) { double c = (minY + maxY) / 2; minY = c - 0.5; maxY = c + 0.5; }

        double m = Math.Clamp(marginFraction, 0, 0.45);
        double sx = WidthPx * (1 - 2 * m) / (maxX - minX);
        double sy = HeightPx * (1 - 2 * m) / (maxY - minY);
        Scale = Math.Clamp(Math.Min(sx, sy), MinScale, MaxScale);
        Center((minX + maxX) / 2, (minY + maxY) / 2);
    }

    public void ZoomAt(double screenX, double screenY, double factor)
    {
        if (!double.IsFinite(factor) || factor <= 0) return;
        var (wx, wy) = ToWorld(screenX, screenY);
        Scale = Math.Clamp(Scale * factor, MinScale, MaxScale);
        MinX = wx - screenX / Scale;
        MinY = wy - (HeightPx - screenY) / Scale;
    }

    public void PanPixels(double dxPixels, double dyPixels)
    {
        MinX -= dxPixels / Scale;
        MinY += dyPixels / Scale;
    }

    public void CenterOn(double minX, double minY, double maxX, double maxY, double marginFraction = 0.15)
    {
        double w = Math.Max(maxX - minX, 1e-9), h = Math.Max(maxY - minY, 1e-9);
        double cx = (minX + maxX) / 2, cy = (minY + maxY) / 2;
        double needScale = Math.Min(WidthPx * (1 - 2 * marginFraction) / w,
                                    HeightPx * (1 - 2 * marginFraction) / h);
        Scale = Math.Clamp(Math.Min(Scale, needScale), MinScale, MaxScale);
        Center(cx, cy);
    }

    public string Describe() => string.Format(CultureInfo.InvariantCulture,
        "视窗 X[{0:0.###}, {1:0.###}] Y[{2:0.###}, {3:0.###}]  1 单位 = {4:0.####} px",
        MinX, MaxX, MinY, MaxY, Scale);
}
