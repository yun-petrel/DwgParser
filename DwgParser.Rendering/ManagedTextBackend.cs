using System.Collections.Concurrent;
using System.Numerics;
using System.Reflection;

using DwgParser.Core.Render;
using SixLabors.Fonts;
namespace DwgParser.Rendering;

public sealed class ManagedTextBackend : ITextRendererBackend, ITextShapeBackend
{
    public static readonly ManagedTextBackend Instance = new();

    public string Name => "managed";


    private static readonly FontCollection Local = new();
    private static readonly ConcurrentDictionary<string, FontFamily?> Cache = new();
    private static readonly Lazy<IReadOnlyList<string>> EmbeddedNames = new(ProbeEmbedded);

    private static string _embeddedDefault = "Noto Sans CJK SC";

    private const string EmbeddedPrefix = "DwgParser.Rendering.Fonts.";

    private static readonly (string Family, string Resource)[] EmbeddedPlan =
    [
        ("SimSun", "simsun-subset.ttf"),
        ("SimHei", "simhei-subset.ttf"),
        ("FangSong", "fangsong-subset.ttf"),
        ("KaiTi", "kaiti-subset.ttf"),
        ("Arial", "segoe-subset.ttf"),
    ];

    public void InstallFontCatalog()
    {
        FontCatalog.Register(() => Families());
        FontCatalog.RegisterExactFamilies(ExactFamilies());
        FontCatalog.RegisterMatcher(ResolveFamilyFromFontFile);
        FontCatalog.RegisterCjkProbe(CoversCjk);
    }

    private static bool CoversCjk(string family)
    {
        try
        {
            var font = ResolveFont(family, 12f);
            if (font.Name.Length == 0) return false;
            var shaper = new GlyphShaper("中", font, 12f);
            return shaper.Points.Count > 0;
        }
        catch { return false; }
    }

    public static IReadOnlyList<string> ExactFamilies()
    {
        _ = EmbeddedNames.Value;
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var f in SystemFonts.Collection.Families)
                set.Add(f.Name);
        }
        catch { }
        foreach (var (family, _) in EmbeddedPlan)
            if (_families.ContainsKey(family)) set.Add(family);
        foreach (var name in _families.Keys) set.Add(name);
        return set.ToList();
    }

    public static IReadOnlyList<string> Families()
    {
        _ = EmbeddedNames.Value;
        var set = new List<string>();
        try
        {
            foreach (var f in SystemFonts.Collection.Families)
                set.Add(f.Name);
        }
        catch {  }
        foreach (var (family, _) in EmbeddedPlan)
            if (_families.ContainsKey(family)) set.Add(family);
        return set;
    }

    private static readonly Dictionary<string, FontFamily> _families = new(StringComparer.OrdinalIgnoreCase);

    private static string? ResolveFamilyFromFontFile(string stem, string fileName)
    {
        foreach (var (family, resource) in EmbeddedPlan)
        {
            string rs = Path.GetFileNameWithoutExtension(resource).Replace("-subset", "");
            if (stem.Equals(rs, StringComparison.OrdinalIgnoreCase)) return family;
        }
        foreach (var (alias, family) in FontAliases)
            if (stem.Equals(alias, StringComparison.OrdinalIgnoreCase)) return family;
        return null;
    }

    private static readonly (string Alias, string Family)[] FontAliases =
    [
        ("simsun", "SimSun"), ("nsimsun", "SimSun"), ("simsunb", "SimSun"),
        ("simhei", "SimHei"), ("simfang", "FangSong"), ("simkai", "KaiTi"),
        ("msyh", "Microsoft YaHei"), ("msyhbd", "Microsoft YaHei"),
        ("arial", "Arial"), ("arialbd", "Arial"), ("ariali", "Arial"),
        ("segoeui", "Segoe UI"), ("tahoma", "Tahoma"), ("calibri", "Calibri"),
        ("times", "Times New Roman"), ("timesbd", "Times New Roman"),
        ("cour", "Courier New"), ("courier", "Courier New"),
        ("isocpeur", "Arial"), ("isocp", "Arial"), ("isoct", "Arial"),
        ("romans", "Arial"), ("romand", "Arial"), ("simplex", "Arial"),
        ("txt", "Arial"), ("monotxt", "Courier New"), ("gbenor", "Arial"),
        ("gbcbig", "FangSong"), ("hztxt", "SimSun"), ("hzfs", "FangSong"),
        ("hzhei", "SimHei"), ("hzkai", "KaiTi"), ("st64s", "SimHei"),
    ];

    private static IReadOnlyList<string> ProbeEmbedded()
    {
        var list = new List<string>();
        var asm = typeof(ManagedTextBackend).Assembly;
        foreach (var name in asm.GetManifestResourceNames())
        {
            if (!name.StartsWith(EmbeddedPrefix, StringComparison.Ordinal) ||
                !name.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase)) continue;
            try
            {
                using var s = asm.GetManifestResourceStream(name);
                if (s == null) continue;
                var fam = Local.Add(s);
                list.Add(fam.Name);
                string file = name[EmbeddedPrefix.Length..];
                foreach (var (windowsName, resource) in EmbeddedPlan)
                    if (string.Equals(resource, file, StringComparison.OrdinalIgnoreCase))
                    {
                        _families[windowsName] = fam;
                        if (windowsName == "SimSun") _embeddedDefault = fam.Name;
                    }
                _families[fam.Name] = fam;
            }
            catch {  }
        }
        return list;
    }

    private static double AscentOf(string? family, double sizePx)
    {
        try
        {
            var font = ResolveFont(family, (float)sizePx);
            var m = font.FontMetrics;
            short asc = m.HorizontalMetrics.Ascender;
            if (m.UnitsPerEm > 0 && asc > 0)
                return sizePx * asc / m.UnitsPerEm;
        }
        catch {  }
        return 0.8 * sizePx;
    }

    private static Font ResolveFont(string? family, float size, FontStyle style = FontStyle.Regular)
    {
        foreach (var candidate in CandidateFamilies(family))
        {
            var font = TryCreate(candidate, size, style);
            if (font != null) return font;
        }
        foreach (var fallback in new[] { "SimSun", "Noto Sans CJK SC", "Microsoft YaHei",
                                         "Source Han Sans SC", "SimHei" })
        {
            var font = TryCreate(fallback, size, style);
            if (font != null) return font;
        }
        try
        {
            var first = SystemFonts.Collection.Families.FirstOrDefault();
            if (first.Name != null)
                return first.CreateFont(size, style);
        }
        catch { }
        return _families.TryGetValue(_embeddedDefault, out var embedded)
            ? embedded.CreateFont(size, style)
            : throw new InvalidOperationException("无可用的字体源（本机字体服务与内嵌子集都不可用）");
    }

    private static IEnumerable<string> CandidateFamilies(string? family)
    {
        if (!string.IsNullOrWhiteSpace(family)) yield return family!;
        yield break;
    }

    private static Font? TryCreate(string family, float size, FontStyle style)
    {
        var cached = Cache.GetOrAdd(family, static name =>
        {
            try
            {
                return SystemFonts.Collection.TryGet(name, out var fam) ? fam
                     : _families.TryGetValue(name, out var emb) ? emb : null;
            }
            catch { return null; }
        });
        if (cached == null) return null;
        try { return cached.Value.CreateFont(size, style); }
        catch { return null; }
    }


    public void DrawText(PixelBuffer buf, int x, int y,
        string text, byte red, byte grn, byte blu, int fontSizePx, double rotationRad = 0,
        string? fontFamily = null, double widthFactor = 1.0,
        int hAlign = 0, int vAlign = 0, double lineGapFactor = 1.0,
        double charHeightPx = 0)
    {
        if (string.IsNullOrEmpty(text)) return;
        fontSizePx = Math.Clamp(fontSizePx, TextRasterLimits.MinFontPx,
                                Math.Max(TextRasterLimits.MinFontPx, buf.Height));
        var lines = text.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
        var runs = new List<MTextParser.Line>(lines.Length);
        foreach (var line in lines)
        {
            var l = new MTextParser.Line();
            if (line.Length > 0)
                l.Runs.Add(new MTextParser.Run
                {
                    Text = line,
                    AbsoluteHeight = fontSizePx,
                    WidthFactor = widthFactor > 0 ? widthFactor : 1.0,
                });
            runs.Add(l);
        }
        string family = string.IsNullOrWhiteSpace(fontFamily)
            ? FontResolver.Resolve(null, text).Family
            : fontFamily!;
        DrawRunsCore(buf, x, y, runs, red, grn, blu, fontSizePx, rotationRad,
            family, hAlign, vAlign, lineGapFactor, 1.0, 1.0, charHeightPx);
    }

    public void DrawMTextRuns(PixelBuffer buf, int x, int y,
        IReadOnlyList<MTextParser.Line> lines, byte red, byte grn, byte blu,
        int baseFontPx, double rotationRad, string defaultFamily,
        double widthFactor, int hAlign, int vAlign, double lineGapFactor,
        double worldToPxScale = 1.0, double charHeightPx = 0)
    {
        if (lines.Count == 0) return;
        baseFontPx = Math.Clamp(baseFontPx, TextRasterLimits.MinFontPx,
                                Math.Max(TextRasterLimits.MinFontPx, buf.Height));
        DrawRunsCore(buf, x, y, lines, red, grn, blu, baseFontPx, rotationRad,
            defaultFamily, hAlign, vAlign, lineGapFactor, widthFactor, worldToPxScale,
            charHeightPx);
    }

    private void DrawRunsCore(PixelBuffer buf, int x, int y,
        IReadOnlyList<MTextParser.Line> lines, byte red, byte grn, byte blu,
        int baseFontPx, double rotationRad, string defaultFamily,
        int hAlign, int vAlign, double lineGapFactor, double widthFactor = 1.0,
        double worldToPxScale = 1.0, double charHeightPx = 0)
    {
        var cov = buf.Coverage;
        double lineGap = ShxTextShapes.LinePitch(
            ShxTextShapes.PitchBase(charHeightPx, baseFontPx), lineGapFactor);
        double totalH = ShxTextShapes.BlockHeight(lineGap, baseFontPx, lines.Count);
        double ascent = AscentOf(defaultFamily, baseFontPx);
        double gdiTop = vAlign switch
        {
            1 => -totalH,
            2 => -totalH / 2,
            3 => 0,
            _ => -ascent,
        };
        double topOffset = -gdiTop - ascent;

        double cos = Math.Cos(rotationRad), sin = Math.Sin(rotationRad);
        double Lx(double lx, double ly) => x + lx * cos - ly * sin;
        double Ly(double lx, double ly) => y - (lx * sin + ly * cos);

        for (int li = 0; li < lines.Count; li++)
        {
            var runs = lines[li].Runs;
            float lineTop = (float)(topOffset - li * lineGap);
            if (runs.Count == 0)
            {
                if (cov != null) {  }
                continue;
            }

            var stops = lines[li].TabStops;
            bool useTabs = stops is { Count: > 0 };
            var segs = new List<(MTextParser.Run Run, string Text, bool Tab)>();
            foreach (var run in runs)
            {
                if (run.IsStack) { segs.Add((run, "", false)); continue; }
                if (run.Text.Length == 0) continue;
                if (!useTabs) { segs.Add((run, run.Text, false)); continue; }
                var parts = run.Text.Split('\t');
                for (int k = 0; k < parts.Length; k++)
                {
                    if (k == 0) { if (parts[0].Length > 0) segs.Add((run, parts[0], false)); }
                    else segs.Add((run, parts[k], true));
                }
            }

            var shaped = new List<(MTextParser.Run Run, string Text, Font Font, double Size, double Wf, double X, double Width)>();
            double lineWidth = 0;
            foreach (var (run, text, tab) in segs)
            {
                if (tab)
                {
                    var nx = MTextParser.NextTabStopPx(stops, worldToPxScale, lineWidth);
                    if (nx.HasValue) lineWidth = Math.Max(lineWidth, nx.Value);
                }
                double size = run.AbsoluteHeight.HasValue
                    ? Math.Max(TextRasterLimits.MinFontPx, run.AbsoluteHeight.Value * worldToPxScale)
                    : Math.Max(TextRasterLimits.MinFontPx, baseFontPx * run.HeightScale);
                double wf = run.WidthFactor ?? widthFactor;
                if (wf <= 0) wf = 1.0;
                var font = ResolveFont(FontResolver.ResolveRunFamily(run.FontSpec, defaultFamily),
                    (float)size, RunStyle(run));
                double w = run.IsStack
                    ? Math.Max(MeasureCore(font, run.StackTop, size), MeasureCore(font, run.StackBottom, size)) * wf
                    : MeasureCore(font, text, size) * wf;
                shaped.Add((run, text, font, size, wf, lineWidth, w));
                lineWidth += w;
            }

            double cx0 = hAlign switch { 1 or 4 => -lineWidth / 2, 2 => -lineWidth, _ => 0 };
            foreach (var (run, text, font, size, wf, segX, w) in shaped)
            {
                double cx = cx0 + segX;
                (byte r, byte g, byte b) = run.AciColor.HasValue
                    ? FromHex(RenderContext.AciColor(run.AciColor.Value))
                    : (red, grn, blu);
                (r, g, b) = RenderInk.Map(r, g, b);
                if (run.IsStack)
                {
                    double centerY = lineTop + baseFontPx / 2.0;
                    DrawGlyphRun(buf, font, run.StackTop, (float)size,
                        Lx(cx, centerY), Ly(cx, centerY), cos, sin, wf, r, g, b, cov, false, false);
                    DrawGlyphRun(buf, font, run.StackBottom, (float)size,
                        Lx(cx, centerY - size), Ly(cx, centerY - size), cos, sin, wf, r, g, b, cov, false, false);
                    if (run.StackKind == '/' || run.StackKind == '#')
                    {
                        double bx0 = cx, bx1 = cx + w;
                        double y0 = run.StackKind == '#' ? centerY - size * 0.5 : centerY;
                        double y1 = run.StackKind == '#' ? centerY + size * 0.5 : centerY;
                        buf.DrawLine((int)Lx(bx0, y0), (int)Ly(bx0, y0),
                            (int)Lx(bx1, y1), (int)Ly(bx1, y1), r, g, b, Math.Max(1, (int)(size * 0.06)));
                    }
                    continue;
                }
                DrawGlyphRun(buf, font, text, (float)size,
                    Lx(cx, lineTop), Ly(cx, lineTop),
                    cos, sin, wf, r, g, b, cov,
                    run.Underline, run.StrikeThrough);
            }
        }
    }

    private static FontStyle RunStyle(MTextParser.Run run)
    {
        var style = FontStyle.Regular;
        if (run.Bold) style |= FontStyle.Bold;
        if (run.Italic) style |= FontStyle.Italic;
        return style;
    }

    private static void DrawGlyphRun(PixelBuffer buf, Font font, string text, float size,
        double originX, double originY, double cos, double sin, double widthFactor,
        byte r, byte g, byte b, byte[]? cov, bool underline, bool strike)
    {
        if (string.IsNullOrEmpty(text)) return;
        double x = 0;
        foreach (var grapheme in Graphemes(text))
        {
            double adv = MeasureCore(font, grapheme, size);
            var gm = new GlyphShaper(grapheme, font, size);
            FillGlyph(buf, gm, originX + x * cos, originY - x * sin, cos, sin, widthFactor,
                r, g, b, cov);
            x += adv;
        }
        if (underline || strike)
            DrawDecoration(buf, font, size, originX, originY, cos, sin, widthFactor,
                x * widthFactor, underline ? (font.FontMetrics.UnderlinePosition * size / font.FontMetrics.UnitsPerEm) : -0.3f * size,
                Math.Max(1.0, font.FontMetrics.UnderlineThickness * size / font.FontMetrics.UnitsPerEm),
                r, g, b, cov);
    }

    private static void DrawDecoration(PixelBuffer buf, Font font, float size,
        double ox, double oy, double cos, double sin, double widthFactor, double widthPx,
        double yOffset, double thickness, byte r, byte g, byte b, byte[]? cov)
    {
        double[] ys = { yOffset, yOffset - thickness };
        double x0 = 0, x1 = widthPx;
        var fill = new ScanlineFiller(buf, cov);
        fill.BeginPolygon(r, g, b);
        foreach (var y in new[] { ys[0], ys[0], ys[1], ys[1] })
        {
        }
        fill.AddPoint(ox + (x0 * cos - ys[0] * sin), oy - (x0 * sin + ys[0] * cos));
        fill.AddPoint(ox + (x1 * cos - ys[0] * sin), oy - (x1 * sin + ys[0] * cos));
        fill.AddPoint(ox + (x1 * cos - ys[1] * sin), oy - (x1 * sin + ys[1] * cos));
        fill.AddPoint(ox + (x0 * cos - ys[1] * sin), oy - (x0 * sin + ys[1] * cos));
        fill.EndPolygon();
        fill.Flush();
    }

    private static IEnumerable<string> Graphemes(string text)
    {
        int i = 0;
        while (i < text.Length)
        {
            int len = 1;
            if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length &&
                char.IsLowSurrogate(text[i + 1])) len = 2;
            int j = i + len;
            while (j < text.Length && IsCombining(text[j])) j++;
            yield return text.Substring(i, j - i);
            i = j;
        }
    }

    private static bool IsCombining(char c)
        => c is >= '\u0300' and <= '\u036F' or >= '\u1AB0' and <= '\u1AFF'
            or >= '\u20D0' and <= '\u20FF' or >= '\uFE20' and <= '\uFE2F';

    public double MeasureTextWidth(string text, string? fontFamily, int fontSizePx,
        double widthFactor, double worldToPxScale = 1.0)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        fontSizePx = Math.Clamp(fontSizePx, TextRasterLimits.MinFontPx, 200);
        string family = string.IsNullOrWhiteSpace(fontFamily)
            ? FontResolver.Resolve(null, text).Family
            : fontFamily!;
        var font = ResolveFont(family, fontSizePx);
        return MeasureCore(font, text, fontSizePx) * (widthFactor > 0 ? widthFactor : 1.0);
    }

    private static double MeasureCore(Font font, string text, double size)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        try
        {
            var options = new TextOptions(font) { Dpi = 72, KerningMode = KerningMode.Standard };
            return TextMeasurer.MeasureAdvance(text, options).Width;
        }
        catch { return text.Length * size * 0.5; }
    }


    public double GetTextOutline(string text, string? fontFamily, int fontSizePx,
        double widthFactor, IGlyphOutlineSink sink, out string resolved)
    {
        if (string.IsNullOrEmpty(text)) { resolved = fontFamily ?? "Arial"; return 0; }
        fontSizePx = Math.Clamp(fontSizePx, TextRasterLimits.MinFontPx, 200);
        var font = ResolveFont(fontFamily, fontSizePx);
        resolved = font.Name;
        var shaper = new GlyphShaper(text, font, fontSizePx);
        shaper.Emit(sink);
        return MeasureCore(font, text, fontSizePx) * (widthFactor > 0 ? widthFactor : 1.0);
    }

    private sealed class GlyphShaper : IGlyphRenderer
    {
        private readonly string _text;
        private readonly Font _font;
        private readonly float _size;
        private readonly List<Vector2> _pts = new();
        private readonly List<bool> _starts = new();
        private const double Flat = 0.22;

        public GlyphShaper(string text, Font font, float size)
        {
            _text = text; _font = font; _size = size;
            var options = new TextOptions(font) { Dpi = 72 };
            new TextRenderer(this).RenderText(text, options);
        }

        public void Emit(IGlyphOutlineSink sink)
        {
            for (int i = 0; i < _pts.Count; i++)
            {
                if (_starts[i]) { sink.BeginFigure(); sink.MoveTo(_pts[i].X, _pts[i].Y); }
                else sink.LineTo(_pts[i].X, _pts[i].Y);
            }
            if (_pts.Count > 0) sink.EndFigure();
        }

        public IReadOnlyList<Vector2> Points => _pts;

        private void Add(float x, float y, bool figureStart)
        {
            _pts.Add(new Vector2((float)x, y));
            _starts.Add(figureStart);
        }

        public bool BeginGlyph(in FontRectangle bounds, in GlyphRendererParameters p)
        {
            _origin = bounds.X;
            _advance = bounds.Width;
            return true;
        }

        private double _origin;
        private double _advance;

        public void BeginText(in FontRectangle rect) { }
        public void EndText() { }
        public void BeginFigure() { }

        public void MoveTo(Vector2 p) { Add(p.X, p.Y, true); }
        public void LineTo(Vector2 p) { Add(p.X, p.Y, false); }

        public void QuadraticBezierTo(Vector2 c, Vector2 p)
        {
            var a = _pts.Count > 0 ? _pts[^1] : p;
            int steps = BezierSteps(a, c, c, p);
            for (int i = 1; i <= steps; i++)
            {
                double t = (double)i / steps, u = 1 - t;
                float x = (float)(u * u * a.X + 2 * u * t * c.X + t * t * p.X);
                float y = (float)(u * u * a.Y + 2 * u * t * c.Y + t * t * p.Y);
                Add(x, y, false);
            }
        }

        public void CubicBezierTo(Vector2 c1, Vector2 c2, Vector2 p)
        {
            var a = _pts.Count > 0 ? _pts[^1] : p;
            int steps = BezierSteps(a, c1, c2, p);
            for (int i = 1; i <= steps; i++)
            {
                double t = (double)i / steps, u = 1 - t;
                float x = (float)(u * u * u * a.X + 3 * u * u * t * c1.X + 3 * u * t * t * c2.X + t * t * t * p.X);
                float y = (float)(u * u * u * a.Y + 3 * u * u * t * c1.Y + 3 * u * t * t * c2.Y + t * t * t * p.Y);
                Add(x, y, false);
            }
        }

        public void EndFigure() { }
        public void EndGlyph() { }
        public TextDecorations EnabledDecorations() => TextDecorations.None;
        public void SetDecoration(TextDecorations d, Vector2 a, Vector2 b, float t) { }

        private static int BezierSteps(Vector2 a, Vector2 c1, Vector2 c2, Vector2 p)
        {
            double chord = Math.Max(Dist(a, p), Math.Max(Dist(a, c1), Dist(c2, p)));
            int steps = (int)Math.Ceiling(Math.Sqrt(chord / Flat) * 2);
            return Math.Clamp(steps, 1, 32);
        }

        private static double Dist(Vector2 a, Vector2 b)
            => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
    }

    private static void FillGlyph(PixelBuffer buf, GlyphShaper shaper,
        double anchorX, double anchorY, double cos, double sin, double widthFactor,
        byte r, byte g, byte b, byte[]? cov)
    {
        var pts = shaper.Points;
        if (pts.Count < 3) return;
        var fill = new ScanlineFiller(buf, cov);
        fill.BeginPolygon(r, g, b);
        foreach (var p in pts)
        {
            double lx = p.X * widthFactor;
            double ly = p.Y;
            fill.AddPoint(anchorX + lx * cos - ly * sin,
                          anchorY - (lx * sin + ly * cos));
        }
        fill.EndPolygon();
        fill.Flush();
    }

    private static (byte R, byte G, byte B) FromHex(string hex)
        => hex.Length < 7
            ? ((byte)0, (byte)0, (byte)0)
            : ((byte)Convert.ToInt32(hex.Substring(1, 2), 16),
               (byte)Convert.ToInt32(hex.Substring(3, 2), 16),
               (byte)Convert.ToInt32(hex.Substring(5, 2), 16));
}
