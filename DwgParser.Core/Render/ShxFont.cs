using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace DwgParser.Core.Render;

public sealed class ShxGlyph
{
    public List<List<double>> Strokes { get; } = new();

    public double Advance { get; set; }

    public double AdvanceY { get; set; }
}

public sealed class ShxFont
{
    public string Name { get; private set; } = "";
    public int Above { get; private set; }
    public int Below { get; private set; }
    public bool IsBigFont { get; private set; }

    private readonly Dictionary<int, byte[]> _codes = new();

    public static bool TryLoad(string path, out ShxFont? font)
    {
        font = null;
        try { return TryLoad(File.ReadAllBytes(path), out font); }
        catch { return false; }
    }

    public static bool TryLoad(byte[] data, out ShxFont? font)
    {
        font = null;
        if (data.Length < 0x20) return false;
        string header = Encoding.ASCII.GetString(data, 0, 22);
        if (header.StartsWith("AutoCAD-86 unifont 1.0")) return TryLoadUnifont(data, out font);
        if (header.StartsWith("AutoCAD-86 bigfont 1.0")) return TryLoadBigFont(data, out font);
        return false;
    }

    private static bool TryLoadUnifont(byte[] data, out ShxFont? font)
    {
        font = null;
        var f = new ShxFont();
        int p = 0x18;
        if (p >= data.Length || data[p] != 0x1A) return false;
        p += 1;
        p += 6;
        int nameStart = p;
        while (p < data.Length && data[p] != 0) p++;
        f.Name = System.Text.Encoding.ASCII.GetString(data, nameStart, p - nameStart);
        p++;
        if (p + 5 > data.Length) return false;
        f.Above = data[p]; p++;
        f.Below = data[p]; p++;
        p += 4;
        try
        {
            while (p + 4 <= data.Length)
            {
                int number = data[p] | (data[p + 1] << 8); p += 2;
                int byteCount = data[p] | (data[p + 1] << 8); p += 2;
                if (byteCount <= 0 || p + byteCount > data.Length) break;
                int recStart = p;
                int nStart = p;
                while (p < data.Length && data[p] != 0) p++;
                int nameLen = p - nStart + 1;
                p++;
                int codeLen = byteCount - nameLen;
                if (codeLen < 0) break;
                var codes = new byte[codeLen];
                System.Array.Copy(data, p, codes, 0, codeLen);
                p += codeLen;
                f._codes[number] = codes;
                _ = recStart;
            }
        }
        catch {  }

        if (f._codes.Count == 0) return false;
        font = f;
        return true;
    }

    private static bool TryLoadBigFont(byte[] data, out ShxFont? font)
    {
        font = null;
        if (data.Length < 0x2B || data[0x18] != 0x1A) return false;
        int itemSize = data[0x19] | (data[0x1A] << 8);
        int count = data[0x1B] | (data[0x1C] << 8);
        int changeCount = data[0x1D] | (data[0x1E] << 8);
        if (itemSize != 8 || count <= 0) return false;
        int p = 0x1F + changeCount * 4;
        if (p + 8 > data.Length) return false;

        var f = new ShxFont { IsBigFont = true, Above = 8, Below = 2 };
        for (int i = 0; i < count && p + 8 <= data.Length; i++, p += 8)
        {
            int code = data[p] | (data[p + 1] << 8);
            int length = data[p + 2] | (data[p + 3] << 8);
            int offset = data[p + 4] | (data[p + 5] << 8) | (data[p + 6] << 16) | (data[p + 7] << 24);
            if (code == 0 && length == 0 && offset == 0) continue;
            if (length <= 0 || offset <= 0 || offset + length > data.Length) continue;
            var record = new Span<byte>(data, offset, length);
            int nul = record.IndexOf((byte)0);
            var bytes = nul < 0 ? record : record[(nul + 1)..];
            if (bytes.Length == 0) continue;
            if (code == 0) { ReadBigFontMetrics(f, record); continue; }
            f._codes[code] = bytes.ToArray();
        }
        if (f._codes.Count == 0) return false;
        if (string.IsNullOrEmpty(f.Name)) f.Name = "bigfont";
        font = f;
        return true;
    }

    private static void ReadBigFontMetrics(ShxFont f, ReadOnlySpan<byte> record)
    {
        int nul = record.IndexOf((byte)0);
        if (nul < 0) return;
        f.Name = Encoding.Latin1.GetString(record[..nul]);
        int i = nul + 1;
        while (i < record.Length && record[i] == 0) i++;
        int rem = record.Length - i;
        if (rem >= 5 && record[i + 1] == 0)
        {
            f.Above = record[i];
            f._extended = true;
        }
        else if (rem is 3 or 4)
        {
            f.Above = record[i];
            f.Below = record[i + 1];
        }
        if (f.Above <= 0) f.Above = 8;
    }

    private static readonly ConcurrentDictionary<char, int> GbCodeCache = new();

    public static int BigFontCode(char ch)
    {
        if (ch < 0x80) return 0;
        return GbCodeCache.GetOrAdd(ch, MapToGbCode);
    }

    private static int MapToGbCode(char ch)
    {
        try
        {
            var bytes = Gbk.GetBytes(ch.ToString());
            if (bytes.Length == 2 && bytes[0] >= 0x81)
                return (bytes[0] << 8) | bytes[1];
        }
        catch {  }
        return 0;
    }

    private static readonly Encoding Gbk = GetGbk();
    private static Encoding GetGbk()
    {
        try
        {
            Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
            return Encoding.GetEncoding("GBK");
        }
        catch { return Encoding.Latin1; }
    }

    public bool HasChar(char ch) => HasGlyph(IsBigFont ? BigFontCode(ch) : ch);

    public ShxGlyph? DecodeChar(char ch, bool vertical = false) => Decode(IsBigFont ? BigFontCode(ch) : ch, vertical);

    public bool HasGlyph(int codepoint) => _codes.ContainsKey(codepoint);

    private static readonly double[] VecX = { 1, 1, 1, 0.5, 0, -0.5, -1, -1, -1, -1, -1, -0.5, 0, 0.5, 1, 1 };
    private static readonly double[] VecY = { 0, 0.5, 1, 1, 1, 1, 1, 0.5, 0, -0.5, -1, -1, -1, -1, -1, -0.5 };

    public ShxGlyph? Decode(int codepoint, bool vertical = false)
    {
        if (!_codes.TryGetValue(codepoint, out var codes)) return null;
        var r = new Renderer(codes, _codes) { BigFont = IsBigFont, Extended = _extended, Vertical = vertical };
        r.Run();
        return r.Glyph;
    }

    private bool _extended;

    private sealed class Renderer
    {
        private readonly byte[] _codes;
        private readonly Dictionary<int, byte[]> _shapes;
        private readonly Stack<(double X, double Y)> _stack = new();
        public ShxGlyph Glyph { get; } = new();
        public bool BigFont { get; init; }
        public bool Extended { get; init; }
        public bool Vertical { get; init; }

        private double _x, _y;
        private double _vl = 1.0;
        private bool _penDown = true;
        private List<double>? _cur;

        public Renderer(byte[] codes, Dictionary<int, byte[]> shapes)
        {
            _codes = codes; _shapes = shapes;
        }

        private void MoveTo(double x, double y)
        {
            _x = x; _y = y;
            _cur = null;
        }

        private void LineTo(double x, double y)
        {
            if (_cur == null) { _cur = new List<double> { _x, _y }; Glyph.Strokes.Add(_cur); }
            _cur.Add(x); _cur.Add(y);
            _x = x; _y = y;
        }

        private void PenTo(double x, double y)
        {
            if (_penDown) LineTo(x, y); else MoveTo(x, y);
        }

        public void Run() => Interpret(_codes, 0);

        private void Interpret(byte[] codes, int depth)
        {
            if (depth > 32) return;
            int i = 0;
            bool skipNext = false;
            while (i < codes.Length)
            {
                int code = codes[i++];
                if (code > 15 && !skipNext)
                {
                    int angle = code & 0xF;
                    int len = (code >> 4) & 0xF;
                    Displace(VecX[angle] * len, VecY[angle] * len);
                }
                else if (code == 0) break;
                else if (code == 1 && !skipNext) _penDown = true;
                else if (code == 2 && !skipNext) _penDown = false;
                else if (code == 3 || code == 4)
                {
                    int factor = i < codes.Length ? codes[i++] : 0;
                    if (!skipNext && factor != 0)
                        _vl = code == 3 ? _vl / factor : _vl * factor;
                }
                else if (code == 5 && !skipNext) _stack.Push((_x, _y));
                else if (code == 6 && !skipNext)
                {
                    if (_stack.Count > 0) { var (sx, sy) = _stack.Pop(); MoveTo(sx, sy); }
                }
                else if (code == 7)
                {
                    if (BigFont)
                    {
                        int sub = i < codes.Length ? codes[i++] : 0;
                        if (sub == 0)
                            i += Extended ? 5 : 4;
                        else if (!skipNext && _shapes.TryGetValue(sub, out var sc))
                            Interpret(sc, depth + 1);
                    }
                    else
                    {
                        int sub = i + 1 < codes.Length ? (codes[i] | (codes[i + 1] << 8)) : 0; i += 2;
                        if (!skipNext && _shapes.TryGetValue(sub, out var sc))
                            Interpret(sc, depth + 1);
                    }
                }
                else if (code == 8)
                {
                    int dx = i < codes.Length ? Signed(codes[i++]) : 0;
                    int dy = i < codes.Length ? Signed(codes[i++]) : 0;
                    if (!skipNext) Displace(dx, dy);
                }
                else if (code == 9)
                {
                    while (i + 1 < codes.Length)
                    {
                        int dx = Signed(codes[i++]); int dy = Signed(codes[i++]);
                        if (dx == 0 && dy == 0) break;
                        if (!skipNext) Displace(dx, dy);
                    }
                }
                else if (code == 10)
                {
                    int radius = i < codes.Length ? codes[i++] : 0;
                    int specs = i < codes.Length ? Octant(codes[i++]) : 0;
                    if (!skipNext) DrawOctantArc(radius, specs);
                }
                else if (code == 11)
                {
                    if (i + 4 < codes.Length)
                    {
                        int startOff = codes[i++]; int endOff = codes[i++];
                        int rHi = codes[i++]; int rLo = codes[i++];
                        int specs = Octant(codes[i++]);
                        if (!skipNext) DrawFractionalArc((rHi << 8) | rLo, startOff, endOff, specs);
                    }
                    else i = codes.Length;
                }
                else if (code == 12)
                {
                    int dx = i < codes.Length ? Signed(codes[i++]) : 0;
                    int dy = i < codes.Length ? Signed(codes[i++]) : 0;
                    int bulge = i < codes.Length ? Signed(codes[i++]) : 0;
                    if (!skipNext) DrawBulge(dx, dy, bulge);
                }
                else if (code == 13)
                {
                    while (i + 2 < codes.Length)
                    {
                        int dx = Signed(codes[i]); int dy = Signed(codes[i + 1]);
                        if (dx == 0 && dy == 0) { i += 2; break; }
                        int bulge = Signed(codes[i + 2]); i += 3;
                        if (!skipNext) DrawBulge(dx, dy, bulge);
                    }
                }
                else if (code == 14)
                {
                    skipNext = !Vertical;
                    continue;
                }
                skipNext = false;
            }
            Glyph.Advance = _x;
            if (Vertical) Glyph.AdvanceY = _y;
        }

        private void Displace(double dx, double dy) => PenTo(_x + dx * _vl, _y + dy * _vl);

        private static int Signed(byte b) => b > 127 ? (b & 127) - 128 : b;
        private static int Octant(byte b) => (b & 128) != 0 ? -(b & 127) : b;

        private void DrawOctantArc(int radius, int specs)
        {
            bool ccw = specs >= 0;
            int s = ccw ? specs : -specs;
            int startOct = (s >> 4) & 0xF;
            int span = s & 0xF; if (span == 0) span = 8;
            double r = radius * _vl;
            double a0 = startOct * System.Math.PI / 4.0;
            double a1 = a0 + (ccw ? 1 : -1) * span * System.Math.PI / 4.0;
            ArcFromAngles(r, a0, a1, ccw);
        }

        private void DrawFractionalArc(int radius, int startOff, int endOff, int specs)
        {
            bool ccw = specs >= 0;
            int s = ccw ? specs : -specs;
            int startOct = (s >> 4) & 0xF;
            int span = s & 0xF; if (span == 0) span = 8;
            if (endOff == 0) endOff = 256;
            double bin = 45.0 / 256.0 * System.Math.PI / 180.0;
            double r = radius * _vl;
            double a0, a1;
            if (ccw)
            {
                int endOct = startOct + span - 1;
                a0 = startOct * System.Math.PI / 4.0 + startOff * bin;
                a1 = endOct * System.Math.PI / 4.0 + endOff * bin;
            }
            else
            {
                int endOct = startOct - span + 1;
                a0 = startOct * System.Math.PI / 4.0 - startOff * bin;
                a1 = endOct * System.Math.PI / 4.0 - endOff * bin;
            }
            ArcFromAngles(r, a0, a1, ccw);
        }

        private void ArcFromAngles(double r, double a0, double a1, bool ccw)
        {
            if (r <= 0) return;
            double cx = _x - r * System.Math.Cos(a0);
            double cy = _y - r * System.Math.Sin(a0);
            double sweep = a1 - a0;
            if (!ccw && sweep > 0) sweep -= 2 * System.Math.PI;
            if (ccw && sweep < 0) sweep += 2 * System.Math.PI;
            int steps = (int)System.Math.Ceiling(System.Math.Abs(sweep) / (System.Math.PI / 24.0));
            if (steps < 2) steps = 2;
            for (int k = 1; k <= steps; k++)
            {
                double a = a0 + sweep * k / steps;
                PenTo(cx + r * System.Math.Cos(a), cy + r * System.Math.Sin(a));
            }
        }

        private void DrawBulge(int dx, int dy, int bulge)
        {
            if (bulge == 0 || !_penDown) { Displace(dx, dy); return; }
            double ex = _x + dx * _vl, ey = _y + dy * _vl;
            double b = System.Math.Abs(bulge) / 127.0;
            bool ccw = bulge > 0;
            double sx = _x, sy = _y;
            if (!ccw) { (sx, sy) = (ex, ey); (ex, ey) = (_x, _y); }
            double theta = 4 * System.Math.Atan(b);
            double chordX = ex - sx, chordY = ey - sy;
            double chord = System.Math.Sqrt(chordX * chordX + chordY * chordY);
            if (chord < 1e-9) { PenTo(ex, ey); return; }
            double radius = chord / (2 * System.Math.Sin(theta / 2));
            double mx = (sx + ex) / 2, my = (sy + ey) / 2;
            double h = radius * System.Math.Cos(theta / 2);
            double nx = -chordY / chord, ny = chordX / chord;
            double cx = mx + (ccw ? -nx : nx) * h;
            double cy = my + (ccw ? -ny : ny) * h;
            double a0 = System.Math.Atan2(sy - cy, sx - cx);
            double a1 = System.Math.Atan2(ey - cy, ex - cx);
            double sweep = a1 - a0;
            if (ccw && sweep < 0) sweep += 2 * System.Math.PI;
            if (!ccw && sweep > 0) sweep -= 2 * System.Math.PI;
            int steps = (int)System.Math.Ceiling(System.Math.Abs(sweep) / (System.Math.PI / 24.0));
            if (steps < 2) steps = 2;
            for (int k = 1; k <= steps; k++)
            {
                double a = a0 + sweep * k / steps;
                PenTo(cx + radius * System.Math.Cos(a), cy + radius * System.Math.Sin(a));
            }
        }
    }
}
