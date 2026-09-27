namespace DwgParser.Core.Render;

internal static class ShxTextShapes
{
    internal const double EmFactor = DwgTextMetrics.EmFactor;


    internal const double PitchFactor = 5.0 / 3.0;

    internal const double ViewerAtLeastK = 1.1;

    internal static double LinePitch(double charHeightPx, double lineGapFactor)
        => charHeightPx * PitchFactor * ViewerAtLeastK * (lineGapFactor > 0 ? lineGapFactor : 1.0);

    internal static double CharHeightFromEm(double emPx) => emPx / EmFactor;

    internal static double PitchBase(double charHeightPx, double emPx)
        => charHeightPx > 0 ? charHeightPx : CharHeightFromEm(emPx);

    internal static double BlockHeight(double pitch, double em, int lineCount)
        => lineCount <= 1 ? em : (lineCount - 1) * pitch + em;

    internal sealed record Seg(string Text, ShxFont? Main, ShxFont? Big, double CapPx, double Wf);

    internal static ShxFont? Pick(ShxFont? main, ShxFont? big, char ch)
    {
        if (ch < 0x80)
        {
            if (main is { } m && m.HasChar(ch)) return m;
            if (big is { } g && g.HasChar(ch)) return g;
            return null;
        }
        if (big is not null && big.HasChar(ch)) return big;
        if (main is not null && main.HasChar(ch)) return main;
        return null;
    }

    internal static bool Covers(ShxFont? main, ShxFont? big, string text)
    {
        if (main is null && big is null) return false;
        int total = 0, hit = 0;
        foreach (char ch in text)
        {
            if (char.IsWhiteSpace(ch)) continue;
            total++;
            if (Pick(main, big, ch) is not null) hit++;
        }
        return total > 0 && hit * 10 >= total * 6;
    }

    internal static double MissingAdvance(ShxFont font) =>
        font.IsBigFont ? font.Above : Math.Max(font.Above * 0.5, 1);

    internal static double Below(ShxFont? main, ShxFont? big, double capPx)
    {
        double d = 0;
        bool any = false;
        if (main is not null) { any = true; d = Math.Max(d, main.Below * capPx / Math.Max(1, main.Above)); }
        if (big is not null) { any = true; d = Math.Max(d, big.Below * capPx / Math.Max(1, big.Above)); }
        return any ? d : capPx * 0.25;
    }

    internal static double Measure(Seg s)
    {
        double w = 0;
        foreach (char ch in s.Text)
        {
            ShxFont? font = Pick(s.Main, s.Big, ch);
            if (font is null) { w += s.CapPx * 0.5 * s.Wf; continue; }
            double fScale = s.CapPx / Math.Max(1, font.Above);
            w += (font.DecodeChar(ch)?.Advance ?? MissingAdvance(font)) * fScale * s.Wf;
        }
        return w;
    }

    internal static List<List<double>> Strokes(Seg s)
    {
        var all = new List<List<double>>();
        double cursor = 0;
        foreach (char ch in s.Text)
        {
            ShxFont? font = Pick(s.Main, s.Big, ch);
            if (font is null) { cursor += s.CapPx * 0.5 * s.Wf; continue; }
            double fScale = s.CapPx / Math.Max(1, font.Above);
            ShxGlyph? gly = font.DecodeChar(ch);
            if (gly is not null)
                foreach (var stroke in gly.Strokes)
                {
                    int n = stroke.Count / 2;
                    if (n < 2) continue;
                    var pts = new List<double>(stroke.Count);
                    for (int i = 0; i < n; i++)
                    {
                        pts.Add(cursor + stroke[2 * i] * fScale * s.Wf);
                        pts.Add(stroke[2 * i + 1] * fScale);
                    }
                    all.Add(pts);
                }
            cursor += (gly?.Advance ?? MissingAdvance(font)) * fScale * s.Wf;
        }
        return all;
    }
}
