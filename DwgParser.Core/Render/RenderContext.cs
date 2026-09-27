using System.Globalization;
using DwgParser.Core.Objects;
using DwgParser.Core.Objects.Records;

namespace DwgParser.Core.Render;

public sealed class RenderContext
{
    public IReadOnlyDictionary<ulong, IReadOnlyList<IEntity>> BlockContents => _blockContents;
    private readonly Dictionary<ulong, IReadOnlyList<IEntity>> _blockContents = new();

    public IReadOnlyList<IEntity> ModelSpaceEntities => _modelSpace;
    private readonly List<IEntity> _modelSpace = new();

    public double MinX { get; private set; } = double.PositiveInfinity;
    public double MinY { get; private set; } = double.PositiveInfinity;
    public double MaxX { get; private set; } = double.NegativeInfinity;
    public double MaxY { get; private set; } = double.NegativeInfinity;

    public const int MaxDepth = 16;

    private readonly Dictionary<ulong, short> _layerColors;

    public IReadOnlyDictionary<ulong, DwgObjectReader.TextStyleInfo> StyleFonts { get; }

    public IReadOnlyDictionary<ulong, DwgObjectReader.DimStyleInfo> DimStyles { get; }

    public DwgObjectReader.DimStyleInfo? ResolveDimStyle(ulong handle)
        => handle != 0 && DimStyles.TryGetValue(handle, out var s) ? s : null;

    private readonly Dictionary<ulong, MLeaderStyleCapture.MLeaderStyleValues> _mleaderStyles = new();

    public MLeaderStyleCapture.MLeaderStyleValues? ResolveMLeaderStyle(ulong handle)
        => handle != 0 && _mleaderStyles.TryGetValue(handle, out var s) ? s : null;

    public IReadOnlyDictionary<ulong, DwgObjectReader.LinetypeInfo> LtypeStyles { get; }

    private readonly Dictionary<string, double[]> _patternByName = new(StringComparer.Ordinal);

    private readonly Dictionary<ulong, ulong> _entityLtype = new();

    private readonly Dictionary<ulong, string> _layerLinetype = new();

    private readonly double _linetypeGlobalScale = 1.0;

    private readonly Dictionary<ulong, double> _entityLinetypeScale = new();

    public double[]? ResolveDashPattern(DwgEntity e)
    {
        if (!string.IsNullOrEmpty(e.LtypeName) && _patternByName.TryGetValue(e.LtypeName, out var pat))
            return Scaled(pat, e);
        if (_entityLtype.TryGetValue(e.Handle, out ulong ltH) && ltH != 0)
            return Scaled(LtypeStyles.TryGetValue(ltH, out var li) ? DashPatternOf(li) : null, e);
        if (e.LayerHandle != 0 && _layerLinetype.TryGetValue(e.LayerHandle, out var layerName)
            && _patternByName.TryGetValue(layerName, out var layerPat))
            return Scaled(layerPat, e);
        return null;
    }

    private double[]? Scaled(double[]? raw, DwgEntity e)
    {
        if (raw is null) return null;
        double s = _linetypeGlobalScale;
        if (_entityLinetypeScale.TryGetValue(e.Handle, out double e48)) s *= e48;
        if (s <= 0 || Math.Abs(s - 1.0) < 1e-9) return raw;
        var r = new double[raw.Length];
        for (int i = 0; i < r.Length; i++) r[i] = raw[i] * s;
        return r;
    }

    public static double[]? DashPatternOf(ILtype li)
    {
        if (li.Dashes.Count == 0) return null;
        bool anyInk = false;
        foreach (var d in li.Dashes) if (d.Length > 0) { anyInk = true; break; }
        if (!anyInk) return null;
        var pat = new double[li.Dashes.Count];
        for (int i = 0; i < pat.Length; i++) pat[i] = li.Dashes[i].Length;
        return pat;
    }

    public string ResolveFontFamily(ulong styleHandle, string text)
        => FontResolver.Resolve(StyleFonts.TryGetValue(styleHandle, out var s) ? s : null, text).Family;

    public bool DarkBackground { get; set; }

    public RenderContext(DwgObjectReader.WalkResult result)
    {
        _layerColors = result.LayerColors;
        StyleFonts = result.StyleFonts;
        DimStyles = result.DimStyles;
        LtypeStyles = result.LtypeStyles;
        foreach (var li in result.LtypeStyles.Values)
        {
            if (li.Name.Length == 0) continue;
            var p = DashPatternOf(li);
            if (p is not null) _patternByName.TryAdd(li.Name, p);
        }
        foreach (var r in result.Records)
            if (r.HasLtypeSlot) _entityLtype[r.Handle] = r.LtypeRef.Resolve(r.Handle);
        foreach (var kv in result.LayerLinetypes) _layerLinetype[kv.Key] = kv.Value;
        _linetypeGlobalScale = result.Ltscale * result.Celtscale;
        foreach (var r in result.Records)
            if (r.LinetypeScale > 0 && Math.Abs(r.LinetypeScale - 1.0) > 1e-9)
                _entityLinetypeScale[r.Handle] = r.LinetypeScale;

        foreach (var r in result.Records)
            if (r.TypeCode == MLeaderStyleRecord.TypeCodeValue && r.MLeader?.Values is { } mv)
                _mleaderStyles[r.Handle] = mv;

        var contentOwners = BlockOwnership.ContentOwners(result);
        foreach (var e in result.Entities)
        {
            if (e.IsErased) continue;
            if (result.Layers.IsOff(e.LayerHandle)) continue;
            if (e.OwnerHandle != 0 && contentOwners.Contains(e.OwnerHandle))
            {
                if (!_blockContents.TryGetValue(e.OwnerHandle, out var list))
                    _blockContents[e.OwnerHandle] = list = new List<IEntity>();
                ((List<IEntity>)list).Add(e);
            }
        }

        ulong modelSpace = result.ModelSpaceHandle != 0
            ? result.ModelSpaceHandle
            : FindModelSpaceHandle(result);
        foreach (var e in result.Entities)
        {
            if (result.Layers.IsOff(e.LayerHandle)) continue;
            bool isModel = e.OwnerHandle == modelSpace || e.EntMode == 2;
            if (e.EntMode == 1) isModel = false;
            if (isModel || (e.OwnerHandle != 0 && !_blockContents.ContainsKey(e.OwnerHandle)))
                _modelSpace.Add(e);
            else if (e.OwnerHandle == 0 && e.EntMode == 0)
                _modelSpace.Add(e);
        }
    }

    private static ulong FindModelSpaceHandle(DwgObjectReader.WalkResult result)
    {
        foreach (var b in result.BlockRegions)
            if (b.NameGuess.Contains("Model_Space", StringComparison.OrdinalIgnoreCase))
                return b.HeaderHandle;
        return 0;
    }

    internal void AddBlock(ulong owner, IEntity e)
    {
        if (!_blockContents.TryGetValue(owner, out var list))
            _blockContents[owner] = list = new List<IEntity>();
        ((List<IEntity>)list).Add(e);
    }

    public void Expand(double x, double y)
    {
        if (x < MinX) MinX = x;
        if (y < MinY) MinY = y;
        if (x > MaxX) MaxX = x;
        if (y > MaxY) MaxY = y;
        _xs.Add(x);
        _ys.Add(y);

        if (_currentColor != null &&
            _currentColor.Equals("#0000FF", StringComparison.OrdinalIgnoreCase))
        {
            _blueXs.Add(x);
            _blueYs.Add(y);
            _blueBounds = null;
        }
    }

    private readonly List<double> _xs = new();
    private readonly List<double> _ys = new();

    internal IReadOnlyList<double> ExpandedX => _xs;
    internal IReadOnlyList<double> ExpandedY => _ys;

    private (double MinX, double MinY, double MaxX, double MaxY)? _robustBounds;


    private string? _currentColor;

    private readonly List<double> _blueXs = new();
    private readonly List<double> _blueYs = new();
    private (double MinX, double MinY, double MaxX, double MaxY)? _blueBounds;

    public (double MinX, double MinY, double MaxX, double MaxY)? BlueBounds
    {
        get
        {
            if (_blueBounds != null) return _blueBounds;
            if (_blueXs.Count == 0) return null;
            var bx = _blueXs.ToArray();
            var by = _blueYs.ToArray();
            Array.Sort(bx);
            Array.Sort(by);
            _blueBounds = (bx[0], by[0], bx[^1], by[^1]);
            return _blueBounds;
        }
    }

    public (double MinX, double MinY, double MaxX, double MaxY) Bounds
        => _robustBounds ??= ComputeRobustBounds();

    private (double MinX, double MinY, double MaxX, double MaxY) ComputeRobustBounds()
    {
        int n = _xs.Count;
        if (n == 0)
            return (MinX, MinY, MaxX, MaxY);

        var xs = _xs.ToArray();
        var ys = _ys.ToArray();
        Array.Sort(xs);
        Array.Sort(ys);

        if (n < 24)
            return (xs[0], ys[0], xs[n - 1], ys[n - 1]);

        double minX = Percentile(xs, 0.01);
        double maxX = Percentile(xs, 0.99);
        double minY = Percentile(ys, 0.01);
        double maxY = Percentile(ys, 0.99);

        if (maxX - minX < 1e-9 || maxY - minY < 1e-9)
            return (xs[0], ys[0], xs[n - 1], ys[n - 1]);

        return (minX, minY, maxX, maxY);
    }

    private static double Percentile(double[] sorted, double p)
    {
        int n = sorted.Length;
        double idx = p * (n - 1);
        int lo = (int)idx;
        int hi = Math.Min(lo + 1, n - 1);
        double t = idx - lo;
        return sorted[lo] + (sorted[hi] - sorted[lo]) * t;
    }


    public string Stroke(IEntity e) => $"stroke=\"{Color(e, _layerColors)}\" stroke-width=\"1\" vector-effect=\"non-scaling-stroke\"";
    public string Fill(IEntity e) => $"fill=\"{Color(e, _layerColors)}\"";

    public static string Color(IEntity e) => Color(e, null);

    public static string Color(IEntity e, Dictionary<ulong, short>? layerColors)
    {
        int idx = e.ColorIndex;
        if (idx == 256 && layerColors != null && e.LayerHandle != 0 &&
            layerColors.TryGetValue(e.LayerHandle, out short layerIdx))
        {
            return AciColor(NormalizeLayerColor(layerIdx));
        }
        if (idx is 0 or 256) return AciColor(7);
        if (idx < 0 || idx > 257) return FromTrueColor(idx);
        return AciColor(idx);
    }

    public static string ColorForSvg(IEntity e, Dictionary<ulong, short>? layerColors)
    {
        string color = Color(e, layerColors);
        if (color.Equals("#FFFFFF", StringComparison.OrdinalIgnoreCase))
            return "#000000";
        return color;
    }

    public string EntityColor(IEntity e)
    {
        string c = Color(e, _layerColors);
        _currentColor = c;
        if (DarkBackground)
            return c.Equals("#000000", StringComparison.OrdinalIgnoreCase) ? "#FFFFFF" : c;
        return c.Equals("#FFFFFF", StringComparison.OrdinalIgnoreCase) ? "#000000" : c;
    }

    internal static short NormalizeLayerColor(short c) => c is 0 or 256 ? (short)7 : c;

    private static string FromTrueColor(int raw)
    {
        int rgb = raw & 0x3FFFFFF;
        if (rgb < 0) rgb = raw & 0xFFFFFF;
        int r = rgb & 0xFF;
        int g = (rgb >> 8) & 0xFF;
        int b = (rgb >> 16) & 0xFF;
        return $"#{r:X2}{g:X2}{b:X2}";
    }

    public static string AciColor(int idx) => AciPalette.ToHex(idx);


    public static string F(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);

    public static string Escape(string s) => s
        .Replace("&", "&amp;")
        .Replace("<", "&lt;")
        .Replace(">", "&gt;")
        .Replace("\"", "&quot;");

    public static string SanitizeText(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        if (s.Contains("\\S", StringComparison.Ordinal)) s = MTextParser.InlineStacks(s);
        var sb = new System.Text.StringBuilder(s.Length);
        int i = 0;
        while (i < s.Length)
        {
            char c = s[i];
            if (c < 0x20 && c != '\n' && c != '\t') { i++; continue; }
            if (c == '{' || c == '}') { i++; continue; }

            if (c == '\\' && i + 1 < s.Length)
            {
                char n = s[i + 1];
                if (n == 'P') { sb.Append('\n'); i += 2; continue; }
                if (n == '~') { sb.Append(' '); i += 2; continue; }
                if ((n == 'U' || n == 'u') && i + 2 < s.Length && s[i + 2] == '+')
                {
                    static bool Hex(char ch) =>
                        (ch >= '0' && ch <= '9') || (ch >= 'a' && ch <= 'f') || (ch >= 'A' && ch <= 'F');
                    if (i + 6 < s.Length && Hex(s[i + 3]) && Hex(s[i + 4]) && Hex(s[i + 5]) && Hex(s[i + 6])
                        && int.TryParse(s.Substring(i + 3, 4), System.Globalization.NumberStyles.HexNumber,
                            System.Globalization.CultureInfo.InvariantCulture, out int code) && code > 0)
                    {
                        sb.Append((char)code);
                        i += 7;
                        if (i < s.Length && s[i] == ';') i++;
                        continue;
                    }
                    sb.Append(n);
                    i += 2;
                    continue;
                }
                if ("KkLlOo".IndexOf(n) >= 0) { i += 2; continue; }
                if ("AHWSfFCcQpTat".IndexOf(n) >= 0)
                {
                    int j = i + 2;
                    while (j < s.Length && s[j] != ';' && s[j] != '\\' && s[j] != '\n' && s[j] != '{' && s[j] != '}') j++;
                    if (j < s.Length && s[j] == ';') { i = j + 1; continue; }
                    sb.Append(n);
                    i += 2;
                    continue;
                }
                if (n == '\\') { sb.Append('\\'); i += 2; continue; }
                if (n == '{' || n == '}') { sb.Append(n); i += 2; continue; }
                sb.Append(n);
                i += 2; continue;
            }

            if (c == '%' && i + 1 < s.Length)
            {
                char p = s[i + 1];
                if (p == '%' && i + 2 < s.Length)
                {
                    switch (s[i + 2])
                    {
                        case 'D': case 'd': sb.Append('°'); i += 3; continue;
                        case 'P': case 'p': sb.Append('±'); i += 3; continue;
                        case 'C': case 'c': sb.Append('Ø'); i += 3; continue;
                        case '%': sb.Append('%'); i += 3; continue;
                        case 'U': case 'u': case 'O': case 'o': i += 3; continue;
                    }
                }
                switch (p)
                {
                    case 'D': case 'd': sb.Append('°'); i += 2; continue;
                    case 'P': case 'p': sb.Append('±'); i += 2; continue;
                    case 'C': case 'c': sb.Append('Ø'); i += 2; continue;
                    case '%': sb.Append('%'); i += 2; continue;
                    case 'U': case 'u': case 'O': case 'o': i += 2; continue;
                    default: sb.Append(c); i++; continue;
                }
            }

            sb.Append(c);
            i++;
        }
        return sb.ToString().TrimEnd('\n');
    }
}

public readonly record struct Matrix(double M11, double M12, double M21, double M22, double Ox, double Oy)
{
    public static Matrix Identity => new(1, 0, 0, 1, 0, 0);
    public static Matrix Translation(double x, double y) => new(1, 0, 0, 1, x, y);
    public static Matrix Rotation(double rad) => new(Math.Cos(rad), -Math.Sin(rad), Math.Sin(rad), Math.Cos(rad), 0, 0);
    public static Matrix Scale(double sx, double sy) => new(sx, 0, 0, sy, 0, 0);
    public (double X, double Y) Apply(double x, double y) =>
        (M11 * x + M12 * y + Ox, M21 * x + M22 * y + Oy);
    public double YAxisScale => Math.Sqrt(M12 * M12 + M22 * M22);
    public static Matrix operator *(Matrix a, Matrix b) => new(
        a.M11 * b.M11 + a.M12 * b.M21,
        a.M11 * b.M12 + a.M12 * b.M22,
        a.M21 * b.M11 + a.M22 * b.M21,
        a.M21 * b.M12 + a.M22 * b.M22,
        a.M11 * b.Ox + a.M12 * b.Oy + a.Ox,
        a.M21 * b.Ox + a.M22 * b.Oy + a.Oy);
}