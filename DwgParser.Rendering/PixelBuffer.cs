using DwgParser.Core.Render;
namespace DwgParser.Rendering;

public sealed class PixelBuffer
{
    public int Width { get; }
    public int Height { get; }
    public byte[] Pixels { get; }

    public byte[]? Coverage { get; private set; }

    public PixelBuffer(int width, int height)
    {
        Width = width;
        Height = height;
        Pixels = new byte[width * height * 4];
        Clear(255, 255, 255);
    }

    public void EnableCoverage()
        => Coverage ??= new byte[Width * Height];

    public void Clear(byte r, byte g, byte b)
    {
        for (int i = 0; i < Pixels.Length; i += 4)
        {
            Pixels[i] = b;
            Pixels[i + 1] = g;
            Pixels[i + 2] = r;
            Pixels[i + 3] = 255;
        }
        if (Coverage != null) Array.Clear(Coverage);
    }

    public void SetPixel(int x, int y, byte r, byte g, byte b)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height) return;
        int idx = (y * Width + x) * 4;
        Pixels[idx] = b;
        Pixels[idx + 1] = g;
        Pixels[idx + 2] = r;
        Pixels[idx + 3] = 255;
        if (Coverage != null) Coverage[idx / 4] = 1;
    }

    public void BlendPixel(int x, int y, byte r, byte g, byte b, double coverage)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height) return;
        if (coverage <= 0) return;
        if (coverage > 1) coverage = 1;
        int idx = (y * Width + x) * 4;
        if (Coverage != null) Coverage[idx / 4] = 1;
        double inv = 1 - coverage;
        Pixels[idx] = (byte)Math.Round(b * coverage + Pixels[idx] * inv);
        Pixels[idx + 1] = (byte)Math.Round(g * coverage + Pixels[idx + 1] * inv);
        Pixels[idx + 2] = (byte)Math.Round(r * coverage + Pixels[idx + 2] * inv);
        Pixels[idx + 3] = 255;
    }

    public void SetPixelAlpha(int x, int y, byte r, byte g, byte b, byte a)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height) return;
        if (a == 0) return;
        int idx = (y * Width + x) * 4;
        if (Coverage != null) Coverage[idx / 4] = 1;
        if (a == 255)
        {
            Pixels[idx] = b;
            Pixels[idx + 1] = g;
            Pixels[idx + 2] = r;
            Pixels[idx + 3] = 255;
        }
        else
        {
            float alpha = a / 255f;
            Pixels[idx] = (byte)(b * alpha + Pixels[idx] * (1 - alpha));
            Pixels[idx + 1] = (byte)(g * alpha + Pixels[idx + 1] * (1 - alpha));
            Pixels[idx + 2] = (byte)(r * alpha + Pixels[idx + 2] * (1 - alpha));
            Pixels[idx + 3] = 255;
        }
    }

    public void DrawLine(double x0, double y0, double x1, double y1, byte r, byte g, byte b, int thickness = 1)
    {
        x0 = ClampD(x0); y0 = ClampD(y0); x1 = ClampD(x1); y1 = ClampD(y1);
        Stroke(x0, y0, x1, y1, Math.Max(1, thickness), r, g, b);
    }

    private void Stroke(double ax, double ay, double bx, double by, double w, byte r, byte g, byte b)
    {
        double half = w / 2.0;
        double pad = half + 1.0;
        int x0 = (int)Math.Floor(Math.Min(ax, bx) - pad), x1 = (int)Math.Ceiling(Math.Max(ax, bx) + pad);
        int y0 = (int)Math.Floor(Math.Min(ay, by) - pad), y1 = (int)Math.Ceiling(Math.Max(ay, by) + pad);
        if (x1 < 0 || y1 < 0 || x0 > Width || y0 > Height) return;
        x0 = Math.Max(0, x0); y0 = Math.Max(0, y0);
        x1 = Math.Min(Width - 1, x1); y1 = Math.Min(Height - 1, y1);

        for (int y = y0; y <= y1; y++)
        {
            for (int x = x0; x <= x1; x++)
            {
                double c = PixelCoverage(x, y, ax, ay, bx, by, half);
                if (c > 0) BlendPixel(x, y, r, g, b, c);
            }
        }
    }

    private static double PixelCoverage(int px, int py, double ax, double ay, double bx, double by, double half)
    {
        const double diag = 0.7072;
        double d = DistanceToSegment(px, py, ax, ay, bx, by);
        if (d <= half - diag) return 1;
        if (d >= half + diag) return 0;
        int hit = 0;
        for (int sy = 0; sy < 4; sy++)
        {
            double qy = py - 0.5 + (sy + 0.5) / 4.0;
            for (int sx = 0; sx < 4; sx++)
            {
                double qx = px - 0.5 + (sx + 0.5) / 4.0;
                if (DistanceToSegment(qx, qy, ax, ay, bx, by) <= half) hit++;
            }
        }
        return hit / 16.0;
    }

    private static double DistanceToSegment(double px, double py, double ax, double ay, double bx, double by)
    {
        double dx = bx - ax, dy = by - ay;
        double len2 = dx * dx + dy * dy;
        double t = len2 > 0 ? ((px - ax) * dx + (py - ay) * dy) / len2 : 0;
        t = Math.Clamp(t, 0, 1);
        double ex = ax + t * dx - px, ey = ay + t * dy - py;
        return Math.Sqrt(ex * ex + ey * ey);
    }

    private static int Clamp(int v) => Math.Clamp(v, -1_000_000_000, 1_000_000_000);

    private static double ClampD(double v) =>
        double.IsNaN(v) || double.IsInfinity(v) ? 0 : Math.Clamp(v, -1e9, 1e9);

    public void FillCircle(int cx, int cy, int radius, byte r, byte g, byte b)
    {
        int r2 = radius * radius;
        for (int y = -radius; y <= radius; y++)
            for (int x = -radius; x <= radius; x++)
                if (x * x + y * y <= r2)
                    SetPixel(cx + x, cy + y, r, g, b);
    }

    public void DrawCircle(double cx, double cy, double radius, byte r, byte g, byte b, int thickness = 1)
    {
        if (radius <= 0) { SetPixel((int)Math.Round(cx), (int)Math.Round(cy), r, g, b); return; }
        double w = Math.Max(1, thickness);
        int steps = StepsFor(radius * 2 * Math.PI);
        double da = Math.PI * 2 / steps;
        double px = cx + radius, py = cy;
        for (int i = 1; i <= steps; i++)
        {
            double a = da * i;
            double nx = cx + radius * Math.Cos(a), ny = cy + radius * Math.Sin(a);
            Stroke(px, py, nx, ny, w, r, g, b);
            px = nx; py = ny;
        }
    }

    public void DrawArc(double cx, double cy, double radius, double startAngle, double endAngle,
        byte r, byte g, byte b, int thickness = 1)
    {
        if (radius <= 0) { SetPixel((int)Math.Round(cx), (int)Math.Round(cy), r, g, b); return; }
        if (endAngle < startAngle) endAngle += Math.PI * 2;
        double w = Math.Max(1, thickness);
        int steps = StepsFor(radius * Math.Abs(endAngle - startAngle));
        double da = (endAngle - startAngle) / steps;
        double px = cx + radius * Math.Cos(startAngle), py = cy - radius * Math.Sin(startAngle);
        for (int i = 1; i <= steps; i++)
        {
            double a = startAngle + da * i;
            double nx = cx + radius * Math.Cos(a), ny = cy - radius * Math.Sin(a);
            Stroke(px, py, nx, ny, w, r, g, b);
            px = nx; py = ny;
        }
    }

    private static int StepsFor(double arcLength) =>
        Math.Clamp((int)Math.Ceiling(arcLength / 0.25), 12, 4096);

    public void FillPolygon(System.Collections.Generic.List<(int X, int Y)> points,
        byte r, byte g, byte b, byte alpha = 255)
    {
        if (points.Count < 3) return;
        int minY = int.MaxValue, maxY = int.MinValue;
        foreach (var p in points) { minY = Math.Min(minY, p.Y); maxY = Math.Max(maxY, p.Y); }
        minY = Math.Max(0, minY); maxY = Math.Min(Height - 1, maxY);
        for (int y = minY; y <= maxY; y++)
        {
            var intersections = new System.Collections.Generic.List<int>();
            for (int i = 0, j = points.Count - 1; i < points.Count; j = i++)
            {
                if ((points[i].Y <= y && points[j].Y > y) || (points[j].Y <= y && points[i].Y > y))
                {
                    double t = (double)(y - points[i].Y) / (points[j].Y - points[i].Y);
                    intersections.Add(points[i].X + (int)(t * (points[j].X - points[i].X)));
                }
            }
            intersections.Sort();
            for (int k = 0; k + 1 < intersections.Count; k += 2)
            {
                int x0 = Math.Max(0, intersections[k]);
                int x1 = Math.Min(Width - 1, intersections[k + 1]);
                for (int x = x0; x <= x1; x++)
                    SetPixelAlpha(x, y, r, g, b, alpha);
            }
        }
    }

    public void DrawPath(System.Collections.Generic.List<PathCommand> commands,
        byte r, byte g, byte b, int thickness = 1)
    {
        int curX = 0, curY = 0;
        foreach (var cmd in commands)
        {
            switch (cmd.Type)
            {
                case PathCommandKind.MoveTo:
                    curX = (int)cmd.X0; curY = (int)cmd.Y0;
                    break;
                case PathCommandKind.LineTo:
                    DrawLine(curX, curY, (int)cmd.X0, (int)cmd.Y0, r, g, b, thickness);
                    curX = (int)cmd.X0; curY = (int)cmd.Y0;
                    break;
                case PathCommandKind.Arc:
                    DrawArc((int)cmd.Cx, (int)cmd.Cy, (int)cmd.Radius,
                        cmd.StartAngle, cmd.EndAngle, r, g, b, thickness);
                    curX = (int)(cmd.Cx + cmd.Radius * Math.Cos(cmd.EndAngle));
                    curY = (int)(cmd.Cy + cmd.Radius * Math.Sin(cmd.EndAngle));
                    break;
            }
        }
    }

    public void DrawText(int x, int y, string text, byte r, byte g, byte b, int scale = 1)
    {
        int cx = x;
        foreach (char c in text)
        {
            if (c == '\n') { cx = x; y += 8 * scale; continue; }
            if (c == '\r') continue;
            DrawChar(cx, y, c, r, g, b, scale);
            cx += 6 * scale;
        }
    }

    private void DrawChar(int x, int y, char c, byte r, byte g, byte b, int scale)
    {
        int idx = Math.Clamp(c - 32, 0, 94);
        var glyph = BasicFont[idx];
        for (int gy = 0; gy < 8; gy++)
        {
            byte row = (byte)(glyph[gy] >> 1);
            for (int gx = 0; gx < 6; gx++)
            {
                if ((row & (1 << gx)) != 0)
                {
                    for (int sy = 0; sy < scale; sy++)
                        for (int sx = 0; sx < scale; sx++)
                            SetPixel(x + gx * scale + sx, y + gy * scale + sy, r, g, b);
                }
            }
        }
    }

    private static readonly byte[][] BasicFont = GenerateBasicFont();
    private static byte[][] GenerateBasicFont()
    {
        var font = new byte[95][];
        for (int i = 0; i < 95; i++) font[i] = new byte[8];
        SetChar(font, 'A', new[] { 0x7C, 0xC6, 0xCE, 0xDE, 0xF6, 0xE6, 0x7C, 0x00 });
        SetChar(font, 'B', new[] { 0xFC, 0x66, 0x66, 0x7C, 0x66, 0x66, 0xFC, 0x00 });
        SetChar(font, 'C', new[] { 0x3C, 0x66, 0xC0, 0xC0, 0xC0, 0x66, 0x3C, 0x00 });
        SetChar(font, 'D', new[] { 0xF8, 0x6C, 0x66, 0x66, 0x66, 0x6C, 0xF8, 0x00 });
        SetChar(font, 'E', new[] { 0xFE, 0x62, 0x68, 0x78, 0x68, 0x62, 0xFE, 0x00 });
        SetChar(font, 'F', new[] { 0xFE, 0x62, 0x68, 0x78, 0x68, 0x60, 0xF0, 0x00 });
        SetChar(font, 'G', new[] { 0x3C, 0x66, 0xC0, 0xC0, 0xCE, 0x66, 0x3E, 0x00 });
        SetChar(font, 'H', new[] { 0xE6, 0xE6, 0xE6, 0xFE, 0xE6, 0xE6, 0xE6, 0x00 });
        SetChar(font, 'I', new[] { 0x3C, 0x18, 0x18, 0x18, 0x18, 0x18, 0x3C, 0x00 });
        SetChar(font, 'J', new[] { 0x1E, 0x0C, 0x0C, 0x0C, 0xCC, 0x6C, 0x38, 0x00 });
        SetChar(font, 'K', new[] { 0xE6, 0x6C, 0x78, 0x70, 0x78, 0x6C, 0xE6, 0x00 });
        SetChar(font, 'L', new[] { 0xF0, 0x60, 0x60, 0x60, 0x62, 0x66, 0xFE, 0x00 });
        SetChar(font, 'M', new[] { 0xC6, 0xEE, 0xFE, 0xFE, 0xD6, 0xC6, 0xC6, 0x00 });
        SetChar(font, 'N', new[] { 0xC6, 0xE6, 0xF6, 0xDE, 0xCE, 0xC6, 0xC6, 0x00 });
        SetChar(font, 'O', new[] { 0x38, 0x6C, 0xC6, 0xC6, 0xC6, 0x6C, 0x38, 0x00 });
        SetChar(font, 'P', new[] { 0xFC, 0x66, 0x66, 0x7C, 0x60, 0x60, 0xF0, 0x00 });
        SetChar(font, 'Q', new[] { 0x7C, 0xC6, 0xC6, 0xC6, 0xD6, 0xDE, 0x7C, 0x06 });
        SetChar(font, 'R', new[] { 0xFC, 0x66, 0x66, 0x7C, 0x6C, 0x66, 0xE6, 0x00 });
        SetChar(font, 'S', new[] { 0x3C, 0x66, 0x60, 0x38, 0x0C, 0x66, 0x3C, 0x00 });
        SetChar(font, 'T', new[] { 0x7E, 0x7E, 0x5A, 0x18, 0x18, 0x18, 0x3C, 0x00 });
        SetChar(font, 'U', new[] { 0xC6, 0xC6, 0xC6, 0xC6, 0xC6, 0x6C, 0x38, 0x00 });
        SetChar(font, 'V', new[] { 0xC6, 0xC6, 0xC6, 0xC6, 0x6C, 0x38, 0x18, 0x00 });
        SetChar(font, 'W', new[] { 0xC6, 0xC6, 0xD6, 0xFE, 0xFE, 0xEE, 0xC6, 0x00 });
        SetChar(font, 'X', new[] { 0xC6, 0xC6, 0x6C, 0x38, 0x6C, 0xC6, 0xC6, 0x00 });
        SetChar(font, 'Y', new[] { 0xC6, 0xC6, 0x6C, 0x38, 0x18, 0x18, 0x3C, 0x00 });
        SetChar(font, 'Z', new[] { 0xFE, 0xC6, 0x8C, 0x18, 0x32, 0x66, 0xFE, 0x00 });
        SetChar(font, 'a', new[] { 0x00, 0x00, 0x38, 0x6C, 0x6E, 0x3B, 0x6E, 0x00 });
        SetChar(font, 'b', new[] { 0xE0, 0x60, 0x7C, 0x66, 0x66, 0x66, 0xDC, 0x00 });
        SetChar(font, 'c', new[] { 0x00, 0x00, 0x3C, 0x66, 0x60, 0x66, 0x3C, 0x00 });
        SetChar(font, 'd', new[] { 0x1C, 0x0C, 0x3C, 0x6C, 0x6C, 0x6C, 0x3E, 0x00 });
        SetChar(font, 'e', new[] { 0x00, 0x00, 0x7C, 0xC6, 0xFE, 0xC0, 0x7C, 0x00 });
        SetChar(font, 'f', new[] { 0x1C, 0x36, 0x30, 0x78, 0x30, 0x30, 0x78, 0x00 });
        SetChar(font, 'g', new[] { 0x00, 0x00, 0x3E, 0x6C, 0x6C, 0x3C, 0x0C, 0x78 });
        SetChar(font, 'h', new[] { 0xE0, 0x60, 0x6C, 0x76, 0x66, 0x66, 0xE6, 0x00 });
        SetChar(font, 'i', new[] { 0x18, 0x00, 0x38, 0x18, 0x18, 0x18, 0x3C, 0x00 });
        SetChar(font, 'j', new[] { 0x06, 0x00, 0x06, 0x06, 0x06, 0x66, 0x66, 0x3C });
        SetChar(font, 'k', new[] { 0xE0, 0x60, 0x66, 0x6C, 0x78, 0x6C, 0xE6, 0x00 });
        SetChar(font, 'l', new[] { 0x38, 0x18, 0x18, 0x18, 0x18, 0x18, 0x3C, 0x00 });
        SetChar(font, 'm', new[] { 0x00, 0x00, 0xEC, 0xFE, 0xD6, 0xC6, 0xC6, 0x00 });
        SetChar(font, 'n', new[] { 0x00, 0x00, 0xDC, 0x66, 0x66, 0x66, 0x66, 0x00 });
        SetChar(font, 'o', new[] { 0x00, 0x00, 0x7C, 0xC6, 0xC6, 0xC6, 0x7C, 0x00 });
        SetChar(font, 'p', new[] { 0x00, 0x00, 0xDC, 0x66, 0x66, 0x7C, 0x60, 0xF0 });
        SetChar(font, 'q', new[] { 0x00, 0x00, 0x3E, 0x6C, 0x6C, 0x3C, 0x0C, 0x1E });
        SetChar(font, 'r', new[] { 0x00, 0x00, 0xDC, 0x76, 0x60, 0x60, 0xF0, 0x00 });
        SetChar(font, 's', new[] { 0x00, 0x00, 0x3C, 0x60, 0x38, 0x0C, 0x78, 0x00 });
        SetChar(font, 't', new[] { 0x30, 0x30, 0x7C, 0x30, 0x30, 0x36, 0x1C, 0x00 });
        SetChar(font, 'u', new[] { 0x00, 0x00, 0xCC, 0xCC, 0xCC, 0xCC, 0x6E, 0x00 });
        SetChar(font, 'v', new[] { 0x00, 0x00, 0xC6, 0xC6, 0xC6, 0x6C, 0x38, 0x00 });
        SetChar(font, 'w', new[] { 0x00, 0x00, 0xC6, 0xC6, 0xD6, 0xFE, 0x6C, 0x00 });
        SetChar(font, 'x', new[] { 0x00, 0x00, 0xC6, 0x6C, 0x38, 0x6C, 0xC6, 0x00 });
        SetChar(font, 'y', new[] { 0x00, 0x00, 0xC6, 0xC6, 0xCE, 0x3E, 0x06, 0xFC });
        SetChar(font, 'z', new[] { 0x00, 0x00, 0xFC, 0x98, 0x30, 0x64, 0xFC, 0x00 });
        SetChar(font, '0', new[] { 0x7C, 0xCE, 0xDE, 0xF6, 0xE6, 0xC6, 0x7C, 0x00 });
        SetChar(font, '1', new[] { 0x18, 0x38, 0x78, 0x18, 0x18, 0x18, 0x7E, 0x00 });
        SetChar(font, '2', new[] { 0x7C, 0xC6, 0x06, 0x0C, 0x30, 0x60, 0xFE, 0x00 });
        SetChar(font, '3', new[] { 0x7C, 0xC6, 0x06, 0x3C, 0x06, 0xC6, 0x7C, 0x00 });
        SetChar(font, '4', new[] { 0x0C, 0x1C, 0x3C, 0x6C, 0xFE, 0x0C, 0x1E, 0x00 });
        SetChar(font, '5', new[] { 0xFE, 0xC0, 0xFC, 0x06, 0x06, 0xC6, 0x7C, 0x00 });
        SetChar(font, '6', new[] { 0x38, 0x60, 0xC0, 0xFC, 0xC6, 0xC6, 0x7C, 0x00 });
        SetChar(font, '7', new[] { 0xFE, 0xC6, 0x0C, 0x18, 0x30, 0x30, 0x30, 0x00 });
        SetChar(font, '8', new[] { 0x7C, 0xC6, 0xC6, 0x7C, 0xC6, 0xC6, 0x7C, 0x00 });
        SetChar(font, '9', new[] { 0x7C, 0xC6, 0xC6, 0x7E, 0x06, 0x0C, 0x78, 0x00 });
        SetChar(font, '.', new[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x18, 0x18, 0x00 });
        SetChar(font, ',', new[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x18, 0x18, 0x30 });
        SetChar(font, ':', new[] { 0x00, 0x00, 0x18, 0x00, 0x00, 0x18, 0x00, 0x00 });
        SetChar(font, ';', new[] { 0x00, 0x00, 0x18, 0x00, 0x00, 0x18, 0x18, 0x30 });
        SetChar(font, '-', new[] { 0x00, 0x00, 0x00, 0x7E, 0x00, 0x00, 0x00, 0x00 });
        SetChar(font, '+', new[] { 0x00, 0x18, 0x18, 0x7E, 0x18, 0x18, 0x00, 0x00 });
        SetChar(font, '=', new[] { 0x00, 0x00, 0x7E, 0x00, 0x7E, 0x00, 0x00, 0x00 });
        SetChar(font, '_', new[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x7E, 0x00 });
        SetChar(font, '/', new[] { 0x02, 0x06, 0x0C, 0x18, 0x30, 0x60, 0x40, 0x00 });
        SetChar(font, '\\', new[] { 0x40, 0x60, 0x30, 0x18, 0x0C, 0x06, 0x02, 0x00 });
        SetChar(font, '(', new[] { 0x0C, 0x18, 0x30, 0x30, 0x30, 0x18, 0x0C, 0x00 });
        SetChar(font, ')', new[] { 0x30, 0x18, 0x0C, 0x0C, 0x0C, 0x18, 0x30, 0x00 });
        SetChar(font, '*', new[] { 0x00, 0x66, 0x3C, 0xFF, 0x3C, 0x66, 0x00, 0x00 });
        SetChar(font, ' ', new[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 });
        SetChar(font, '#', new[] { 0x36, 0x36, 0x7F, 0x36, 0x7F, 0x36, 0x36, 0x00 });
        SetChar(font, '@', new[] { 0x0C, 0x1A, 0x26, 0x6E, 0xCE, 0x9C, 0x70, 0x00 });
        SetChar(font, '<', new[] { 0x0C, 0x18, 0x30, 0x60, 0x30, 0x18, 0x0C, 0x00 });
        SetChar(font, '>', new[] { 0x30, 0x18, 0x0C, 0x06, 0x0C, 0x18, 0x30, 0x00 });
        SetChar(font, '?', new[] { 0x38, 0x6C, 0x0C, 0x18, 0x18, 0x00, 0x18, 0x00 });
        SetChar(font, '!', new[] { 0x18, 0x18, 0x18, 0x18, 0x18, 0x00, 0x18, 0x00 });
        SetChar(font, '\'', new[] { 0x18, 0x18, 0x18, 0x00, 0x00, 0x00, 0x00, 0x00 });
        SetChar(font, '"', new[] { 0x6C, 0x6C, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 });
        return font;
    }

    private static void SetChar(byte[][] font, char c, int[] data)
    {
        int idx = c - 32;
        if (idx >= 0 && idx < 95 && data.Length == 8)
        {
            font[idx] = new byte[8];
            for (int i = 0; i < 8; i++) font[idx][i] = (byte)data[i];
        }
    }

    public enum PathCommandKind { MoveTo, LineTo, Arc }
    public record struct PathCommand(PathCommandKind Type, double X0, double Y0,
        double Cx, double Cy, double Radius,
        double StartAngle, double EndAngle);
}