using System.Globalization;
using System.Text;

namespace DwgParser.Core.Render;

public static class MTextParser
{
    public sealed class Run
    {
        public string Text { get; set; } = "";
        public string? FontSpec { get; set; }
        public bool Bold { get; set; }
        public bool Italic { get; set; }
        public bool Underline { get; set; }
        public bool Overline { get; set; }
        public bool StrikeThrough { get; set; }
        public double HeightScale { get; set; } = 1.0;
        public double? AbsoluteHeight { get; set; }
        public double? WidthFactor { get; set; }
        public double? Oblique { get; set; }
        public int? AciColor { get; set; }
        public string StackTop { get; set; } = "";
        public string StackBottom { get; set; } = "";
        public char StackKind { get; set; } = '\0';
        public bool IsStack => StackKind != '\0';
    }

    public sealed class Line
    {
        public List<Run> Runs { get; } = new();

        public List<double>? TabStops { get; set; }

        public int? ParagraphAlign { get; set; }
    }

    public static bool HasFormatting(IReadOnlyList<Line> lines)
    {
        foreach (var line in lines)
            foreach (var r in line.Runs)
                if (r.FontSpec != null || r.AciColor.HasValue || r.Bold || r.Italic ||
                    r.Underline || r.Overline || r.StrikeThrough || r.IsStack ||
                    r.HeightScale != 1.0 || r.AbsoluteHeight.HasValue ||
                    r.WidthFactor.HasValue || r.Oblique.HasValue)
                    return true;
        return false;
    }

    public static bool IsNonShxRunFont(string? fontSpec)
        => fontSpec is { Length: > 0 } fs
           && !fs.EndsWith(".shx", StringComparison.OrdinalIgnoreCase);

    public static bool HasNonShxRunFont(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return false;
        foreach (var line in Parse(raw))
            foreach (var run in line.Runs)
                if (IsNonShxRunFont(run.FontSpec)) return true;
        return false;
    }

    public static List<Line> Parse(string raw)
    {
        var lines = new List<Line>();
        if (string.IsNullOrEmpty(raw)) return lines;

        var state = new Run();
        var stack = new Stack<Run>();
        var cur = state;
        var line = new Line();
        Run? open = null;
        lines.Add(line);

        void Flush()
        {
            if (open != null && open.Text.Length > 0)
                line.Runs.Add(open);
            open = null;
        }
        Run Cur() => open ??= Clone(cur);

        int i = 0, n = raw.Length;

        static int ScanValueEnd(string s, int from, bool braceLegal)
        {
            for (int j = from; j < s.Length; j++)
            {
                char c = s[j];
                if (c == ';') return j;
                if (c == '\\' || c == '\n' || (!braceLegal && (c == '{' || c == '}'))) return -1;
            }
            return -1;
        }

        while (i < n)
        {
            char c = raw[i];

            if (c == '\n') { Flush(); line = new Line(); lines.Add(line); i++; continue; }
            if (c < 0x20 && c != '\t') { i++; continue; }

            if (c == '{')
            {
                Flush();
                stack.Push(Clone(cur));
                i++;
                continue;
            }
            if (c == '}')
            {
                Flush();
                if (stack.Count > 0) cur = stack.Pop();
                i++;
                continue;
            }

            if (c == '\\' && i + 1 < n)
            {
                char code = raw[i + 1];
                switch (code)
                {
                    case 'P':
                        Flush(); line = new Line(); lines.Add(line); i += 2; continue;
                    case 'f': case 'F':
                    {
                        int end = ScanValueEnd(raw, i + 2, false);
                        if (end < 0) { Cur().Text += code; i += 2; continue; }
                        ParseFont(raw[(i + 2)..end], cur);
                        Flush();
                        i = end + 1; continue;
                    }
                    case 'H':
                    {
                        int end = ScanValueEnd(raw, i + 2, false);
                        if (end < 0) { Cur().Text += code; i += 2; continue; }
                        ParseHeight(raw[(i + 2)..end], cur);
                        Flush();
                        i = end + 1; continue;
                    }
                    case 'W':
                    {
                        int end = ScanValueEnd(raw, i + 2, false);
                        if (end < 0) { Cur().Text += code; i += 2; continue; }
                        string v = raw[(i + 2)..end].TrimEnd('X', 'x');
                        if (double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out double w) && w > 0)
                        { cur.WidthFactor = w; Flush(); }
                        i = end + 1; continue;
                    }
                    case 'Q':
                    {
                        int end = ScanValueEnd(raw, i + 2, false);
                        if (end < 0) { Cur().Text += code; i += 2; continue; }
                        if (double.TryParse(raw[(i + 2)..end], NumberStyles.Float, CultureInfo.InvariantCulture, out double q))
                        { cur.Oblique = q * Math.PI / 180.0; Flush(); }
                        i = end + 1; continue;
                    }
                    case 'C':
                    {
                        int end = ScanValueEnd(raw, i + 2, false);
                        if (end < 0) { Cur().Text += code; i += 2; continue; }
                        if (int.TryParse(raw[(i + 2)..end], out int aci) && aci is >= 1 and <= 255)
                        { cur.AciColor = aci; Flush(); }
                        i = end + 1; continue;
                    }
                    case 'p':
                    {
                        int end = ScanValueEnd(raw, i + 2, false);
                        if (end < 0) { Cur().Text += code; i += 2; continue; }
                        string pv = raw[(i + 2)..end];
                        if (pv.Length > 0 && pv[0] == 't')
                        {
                            var stops = new List<double>();
                            foreach (var tok in pv[1..].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                                if (double.TryParse(tok, NumberStyles.Float, CultureInfo.InvariantCulture, out double st))
                                    stops.Add(st);
                            if (stops.Count > 0) line.TabStops = stops;
                        }
                        else if (pv.Length > 0 && pv[0] == 'a'
                                 && double.TryParse(pv[1..].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double paVal))
                        {
                            line.ParagraphAlign = (int)paVal;
                        }
                        i = end + 1; continue;
                    }
                    case 'c':
                    case 'A': case 'T': case 'a': case 't':
                    {
                        int end = ScanValueEnd(raw, i + 2, false);
                        if (end < 0) { Cur().Text += code; i += 2; continue; }
                        i = end + 1; continue;
                    }
                    case 'S':
                    {
                        int end = ScanValueEnd(raw, i + 2, true);

                        if (end < 0) { Cur().Text += code; i += 2; continue; }
                        string body = raw[(i + 2)..end];
                        int sep = body.IndexOfAny(new[] { '/', '^', '#' });
                        Flush();
                        if (sep < 0)
                        {
                            Cur().Text += body;
                        }
                        else
                        {
                            var sr = Clone(cur);
                            sr.Text = "";
                            sr.StackTop = body[..sep];
                            sr.StackBottom = body[(sep + 1)..];
                            sr.StackKind = body[sep];
                            line.Runs.Add(sr);
                        }
                        i = end + 1; continue;
                    }
                    case 'L': cur.Underline = true; Flush(); i += 2; continue;
                    case 'l': cur.Underline = false; Flush(); i += 2; continue;
                    case 'O': cur.Overline = true; Flush(); i += 2; continue;
                    case 'o': cur.Overline = false; Flush(); i += 2; continue;
                    case 'K': cur.StrikeThrough = true; Flush(); i += 2; continue;
                    case 'k': cur.StrikeThrough = false; Flush(); i += 2; continue;
                    case '~': Cur().Text += ' '; i += 2; continue;
                    case '\\': Cur().Text += '\\'; i += 2; continue;
                    case '{': Cur().Text += '{'; i += 2; continue;
                    case '}': Cur().Text += '}'; i += 2; continue;
                    case 'U' or 'u' when i + 2 < n && raw[i + 2] == '+':
                    {
                        static bool Hex(char ch) =>
                            (ch >= '0' && ch <= '9') || (ch >= 'a' && ch <= 'f') || (ch >= 'A' && ch <= 'F');
                        if (i + 6 < n && Hex(raw[i + 3]) && Hex(raw[i + 4]) && Hex(raw[i + 5]) && Hex(raw[i + 6])
                            && int.TryParse(raw.Substring(i + 3, 4), NumberStyles.HexNumber,
                                CultureInfo.InvariantCulture, out int cp) && cp > 0)
                        {
                            Cur().Text += (char)cp;
                            i += 7;
                            if (i < n && raw[i] == ';') i++;
                        }
                        else { Cur().Text += code; i += 2; }
                        continue;
                    }
                    case 'X' when i + 2 < n:
                        i += raw[i + 2] == ';' ? 3 : 2; continue;
                    default:
                        Cur().Text += code; i += 2; continue;
                }
            }

            if (c == '%' && i + 1 < n)
            {
                char p2 = raw[i + 1];
                if (p2 == '%' && i + 2 < n)
                {
                    switch (raw[i + 2])
                    {
                        case 'D' or 'd': Cur().Text += '°'; i += 3; continue;
                        case 'P' or 'p': Cur().Text += '±'; i += 3; continue;
                        case 'C' or 'c': Cur().Text += 'Ø'; i += 3; continue;
                        case '%': Cur().Text += '%'; i += 3; continue;
                        case 'U' or 'u': cur.Underline = !cur.Underline; Flush(); i += 3; continue;
                        case 'O' or 'o': cur.Overline = !cur.Overline; Flush(); i += 3; continue;
                    }
                }
                switch (p2)
                {
                    case 'D' or 'd': Cur().Text += '°'; i += 2; continue;
                    case 'P' or 'p': Cur().Text += '±'; i += 2; continue;
                    case 'C' or 'c': Cur().Text += 'Ø'; i += 2; continue;
                    case '%': Cur().Text += '%'; i += 2; continue;
                    case 'U' or 'u': cur.Underline = !cur.Underline; Flush(); i += 2; continue;
                    case 'O' or 'o': cur.Overline = !cur.Overline; Flush(); i += 2; continue;
                    default: Cur().Text += c; i++; continue;
                }
            }

            Cur().Text += c;
            i++;
        }
        Flush();
        while (lines.Count > 1 && lines[^1].Runs.Count == 0) lines.RemoveAt(lines.Count - 1);
        return lines;
    }

    public static double? NextTabStopPx(IReadOnlyList<double>? stops, double worldToPxScale, double currentPx)
    {
        if (stops is not { Count: > 0 }) return null;
        double best = double.NaN;
        foreach (var s in stops)
        {
            double px = s * worldToPxScale;
            if (px > currentPx + 1e-6 && (double.IsNaN(best) || px < best)) best = px;
        }
        return double.IsNaN(best) ? null : best;
    }

    private static Run Clone(Run r) => new()
    {
        FontSpec = r.FontSpec,
        Bold = r.Bold,
        Italic = r.Italic,
        Underline = r.Underline,
        Overline = r.Overline,
        StrikeThrough = r.StrikeThrough,
        HeightScale = r.HeightScale,
        AbsoluteHeight = r.AbsoluteHeight,
        WidthFactor = r.WidthFactor,
        Oblique = r.Oblique,
        AciColor = r.AciColor,
    };

    private static void ParseFont(string body, Run st)
    {
        var parts = body.Split('|');
        string name = parts[0].Trim();
        if (name.Length > 0)
            st.FontSpec = name;
        foreach (var p in parts.Skip(1))
        {
            var v = p.Trim();
            if (v.Length >= 2 && (v[0] == 'b' || v[0] == 'B') && (v[1] == '0' || v[1] == '1'))
                st.Bold = v[1] == '1';
            else if (v.Length >= 2 && (v[0] == 'i' || v[0] == 'I') && (v[1] == '0' || v[1] == '1'))
                st.Italic = v[1] == '1';
        }
    }

    public static string InlineStacks(string raw)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var ln in Parse(raw))
        {
            foreach (var run in ln.Runs)
                sb.Append(run.IsStack ? run.StackTop + "/" + run.StackBottom : run.Text);
            sb.Append('\n');
        }
        return sb.ToString().TrimEnd('\n');
    }

    private static void ParseHeight(string body, Run st)
    {
        body = body.Trim();
        if (body.Length == 0) return;
        bool relative = body[^1] is 'x' or 'X';
        string num = relative ? body[..^1] : body;
        if (!double.TryParse(num, NumberStyles.Float, CultureInfo.InvariantCulture, out double v) || v <= 0)
            return;
        if (relative) st.HeightScale *= v;
        else st.AbsoluteHeight = v;
    }
}
