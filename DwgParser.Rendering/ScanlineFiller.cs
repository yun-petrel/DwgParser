using DwgParser.Core.Render;
namespace DwgParser.Rendering;

internal sealed class ScanlineFiller
{
    private readonly PixelBuffer _buf;
    private readonly byte[]? _cov;
    private readonly List<(double X, double Y)> _points = new();
    private readonly List<int> _start = new();
    private byte _r, _g, _b;

    private const int Sub = 4;

    public ScanlineFiller(PixelBuffer buf, byte[]? coverage)
    {
        _buf = buf;
        _cov = coverage ?? buf.Coverage;
    }

    public void BeginPolygon(byte r, byte g, byte b)
    {
        _r = r; _g = g; _b = b;
        _points.Clear();
        _start.Clear();
    }

    public void AddPoint(double x, double y) => _points.Add((x, y));

    public void EndPolygon() => _start.Add(_points.Count);

    public void Flush()
    {
        if (_points.Count < 3) return;

        double minX = double.MaxValue, minY = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue;
        foreach (var (x, y) in _points)
        {
            if (x < minX) minX = x;
            if (y < minY) minY = y;
            if (x > maxX) maxX = x;
            if (y > maxY) maxY = y;
        }
        int x0 = Math.Max(0, (int)Math.Floor(minX));
        int y0 = Math.Max(0, (int)Math.Floor(minY));
        int x1 = Math.Min(_buf.Width - 1, (int)Math.Ceiling(maxX));
        int y1 = Math.Min(_buf.Height - 1, (int)Math.Ceiling(maxY));
        if (x1 < x0 || y1 < y0) return;

        int w = x1 - x0 + 1, h = y1 - y0 + 1;
        if ((long)w * h > 4_000_000) return;

        var hits = new ushort[w * h];
        var ptx = new double[_points.Count + 1];
        var dir = new int[_points.Count + 1];
        int start = 0;
        foreach (var end in _start)
        {
            int n = end - start;
            if (n < 3) { start = end; continue; }
            for (int row = 0; row < h; row++)
            for (int r = 0; r < Sub; r++)
            {
                double sy = y0 + row + 1.0 / (Sub * 2) + r / (double)Sub;
                Array.Fill(ptx, 0.0);
                int cnt = 0;
                for (int i = 0; i < n; i++)
                {
                    var a = _points[start + i];
                    var b = _points[start + (i + 1) % n];
                    if ((a.Y <= sy && b.Y > sy) || (b.Y <= sy && a.Y > sy))
                    {
                        int dirBit = a.Y <= sy ? 1 : -1;
                        dir[cnt] = dirBit;
                        ptx[cnt] = a.X + (sy - a.Y) / (b.Y - a.Y) * (b.X - a.X);
                        cnt++;
                    }
                }
                if (cnt < 2) continue;
                for (int i = 1; i < cnt; i++)
                    for (int j = i; j > 0 && ptx[j] < ptx[j - 1]; j--)
                    {
                        (ptx[j], ptx[j - 1]) = (ptx[j - 1], ptx[j]);
                        (dir[j], dir[j - 1]) = (dir[j - 1], dir[j]);
                    }
                int winding = 0;
                for (int i = 0; i + 1 < cnt; i++)
                {
                    winding += dir[i];
                    if (winding == 0) continue;
                    double spanA = ptx[i], spanB = ptx[i + 1];
                    if (spanB <= spanA) continue;
                    int colA = Math.Max(0, (int)Math.Floor(spanA - x0));
                    int colB = Math.Min(w - 1, (int)Math.Ceiling(spanB - x0) - 1);
                    int rowBase = row * w;
                    for (int col = colA; col <= colB; col++)
                    {
                        int inner = 0;
                        for (int c = 0; c < Sub; c++)
                        {
                            double sx = x0 + col + 1.0 / (Sub * 2) + c / (double)Sub;
                            if (sx >= spanA && sx < spanB) inner++;
                        }
                        if (inner > 0) hits[rowBase + col] += (ushort)inner;
                    }
                }
            }
            start = end;
        }

        const double norm = 1.0 / (Sub * Sub);
        for (int row = 0; row < h; row++)
            for (int col = 0; col < w; col++)
            {
                int n = hits[row * w + col];
                if (n == 0) continue;
                double cov = n * norm;
                if (cov > 1) cov = 1;
                _buf.BlendPixel(x0 + col, y0 + row, _r, _g, _b, cov);
            }
    }
}
