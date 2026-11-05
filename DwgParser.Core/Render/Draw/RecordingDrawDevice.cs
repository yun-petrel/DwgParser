using System.Globalization;
using DwgParser.Core.Objects;

namespace DwgParser.Core.Render;

internal sealed class RecordingDrawDevice : IDrawDevice
{
    public List<string> Log { get; } = new();
    public DrawViewport View { get; private set; }

    public List<TextRequest> Texts { get; } = new();

    private static string F(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);
    private static string P(Vec2 p) => $"({F(p.X)},{F(p.Y)})";
    private static string Hex(uint rgba) => Rgba.ToHex(rgba);

    private static string Sk(StrokeStyle s)
        => $"{Hex(s.Rgba)}{(s.NonScaling ? "" : " scaling")}{(s.Dashed ? " dashed" : "")}"
         + (s.HasDashPattern
            ? "[" + string.Join(",", s.DashPattern!.Select(v => F(v))) + "]"
            : "");

    private static string Fk(FillStyle f)
        => f.IsFill ? $" fill={Hex(f.Rgba)} o{F(f.Opacity)}{(f.Stroked ? " stroked" : "")}" : "";

    public void BeginPass(DrawViewport view) { View = view; Log.Add("BeginPass"); }
    public void EndPass() => Log.Add("EndPass");

    public void DrawLine(Vec2 a, Vec2 b, StrokeStyle stroke)
        => Log.Add($"DrawLine {P(a)}-{P(b)} {Sk(stroke)}");
    public void DrawRect(Vec2 a, Vec2 b, StrokeStyle stroke)
        => Log.Add($"DrawRect {P(a)}-{P(b)} {Sk(stroke)}");
    public void DrawArc(Vec2 c, double r, double a0, double a1, StrokeStyle stroke)
        => Log.Add($"DrawArc {P(c)} r{F(r)} {F(a0)}..{F(a1)} {Sk(stroke)}");
    public void DrawCircle(Vec2 c, double r, StrokeStyle stroke, FillStyle fill)
        => Log.Add($"DrawCircle {P(c)} r{F(r)} {Sk(stroke)}{Fk(fill)}");
    public void DrawDot(Vec2 c, int radiusPx, FillStyle fill)
        => Log.Add($"DrawDot {P(c)} rpx={radiusPx}{Fk(fill)}");
    public void DrawText(TextRequest t)
    {
        Texts.Add(t);
        Log.Add($"DrawText {P(t.Position)} \"{t.Content}\" {t.Style.FontFamily} "
            + $"h{F(t.Style.Height)} {t.Style.Align}");
    }
    public void DrawPolyline(ReadOnlyMemory<Vec2> pts, bool closed, StrokeStyle stroke, FillStyle fill)
    {
        var span = pts.Span;
        Log.Add($"DrawPolyline n={span.Length} closed={(closed ? 1 : 0)}"
            + (span.Length > 0 ? $" first={P(span[0])}" : "") + $" {Sk(stroke)}{Fk(fill)}");
    }
    public void FillPolygon(ReadOnlyMemory<Vec2> ring, FillStyle fill)
        => Log.Add($"FillPolygon n={ring.Length}{Fk(fill)}");

    public void DrawPath(ReadOnlyMemory<PathSeg> segs, bool closed, StrokeStyle stroke, FillStyle fill, PathLayout layout)
    {
        var s = segs.Span;
        Log.Add($"DrawPath n={s.Length} closed={(closed ? 1 : 0)} {layout} {Sk(stroke)}{Fk(fill)}");
        for (int i = 0; i < s.Length; i++)
            if (s[i].Kind == PathSegKind.Arc)
                Log.Add($"  seg[{i}] Arc {P(s[i].P1)}->{P(s[i].P2)} bulge={F(s[i].Bulge)}");
    }
}
