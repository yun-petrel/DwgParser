using DwgParser.Core.Objects;

namespace DwgParser.Core.Render;

internal sealed class DimensionEntityRenderer : IEntityRenderer
{
    public static readonly DimensionEntityRenderer Instance = new();

    public void Render(DwgEntity e, RenderContext ctx, Matrix m, IDrawDevice device)
    {
        var rgba = Rgba.FromHex(ctx.EntityColor(e));
        var blockDisplay = ctx.BlockContents.TryGetValue(e.DimBlockHandle, out var bd) ? bd : null;
        var shape = DimensionRenderer.Plan(e, ctx.ResolveDimStyle(e.DimStyleHandle), blockDisplay);

        if (shape is not null)
        {
            var segs = new List<PathSeg>(shape.Lines.Count * 2);
            foreach (var (a, b) in shape.Lines)
            {
                segs.Add(PathSeg.Move(World(a, m)));
                segs.Add(PathSeg.Line(World(a, m), World(b, m)));
            }
            if (segs.Count > 0)
                device.DrawPath(segs.ToArray(), false, new StrokeStyle(rgba, true, false),
                                FillStyle.NoFill, PathLayout.StrokeFirst);

            foreach (var (tip, dir) in shape.Arrows)
                device.FillPolygon(ArrowRing(tip, dir, shape.ArrowLength, m), new FillStyle(rgba, 1, Stroked: false));

            if (shape.HasText)
            {
                double rot = e.DimTextRotation != 0 ? e.DimTextRotation : shape.TextRotation;
                double h = shape.TextHeight > 0 ? shape.TextHeight : e.Height;
                string stacked = MTextParser.InlineStacks(shape.Text);
                var style = ctx.StyleFonts.TryGetValue(e.TextStyleHandle, out var st) ? st : null;
                int att = shape.TextAttachmentPoint is >= 1 and <= 9 ? shape.TextAttachmentPoint : 5;
                int hAlign = ((att - 1) % 3) switch { 0 => 0, 1 => 1, _ => 2 };
                int vAlign = ((att - 1) / 3) switch { 0 => 3, 1 => 2, _ => 1 };
                device.DrawText(new TextRequest(shape.Text, World(shape.TextPos, m),
                    new TextStyle(ctx.ResolveFontFamily(e.TextStyleHandle, stacked), h, rot,
                                  TextAlignment.BaseLeft, rgba),
                    Raster: new RasterTextSpec(hAlign, vAlign, 0, 0, World(shape.TextPos, m), 0, 1.0,
                        IsMText: true, Style: style, EmFactor: 1.0, MinFontPx: 5,
                        StyleWidthFactor: false, BaselineNudge: 0.33)));
            }
            return;
        }

        if (e.Points.Count >= 2)
        {
            var segs = new List<PathSeg>(e.Points.Count) { PathSeg.Move(World(e.Points[0], m)) };
            for (int i = 1; i < e.Points.Count; i++)
                segs.Add(PathSeg.Line(World(e.Points[i - 1], m), World(e.Points[i], m)));
            device.DrawPath(segs.ToArray(), false, new StrokeStyle(rgba, false, false),
                            FillStyle.NoFill, PathLayout.StrokeFirst);
        }

        string dimText = e.TextValue is { Length: > 0 }
            ? e.TextValue.Replace("<>", DimensionRenderer.FormatMeasurement(e.Measurement))
            : DimensionRenderer.FormatMeasurement(e.Measurement);
        if (dimText.Length > 0)
            device.DrawText(new TextRequest(dimText, World(e.Start, m),
                new TextStyle(ctx.ResolveFontFamily(e.TextStyleHandle, dimText), e.Height,
                              e.DimTextRotation != 0 ? e.DimTextRotation : e.Rotation,
                              TextAlignment.BaseLeft, rgba),
                Raster: new RasterTextSpec(0, 0, 0, 0, World(e.Start, m), 0, 1.0,
                    IsMText: false,
                    Style: ctx.StyleFonts.TryGetValue(e.TextStyleHandle, out var fst) ? fst : null,
                    EmFactor: 1.0, MinFontPx: 5, StyleWidthFactor: false,
                    FamilyFromStyle: false)));
    }

    private static Vec2[] ArrowRing(Vec2 tip, Vec2 dir, double len, Matrix m)
    {
        double w = len * DimensionRenderer.ArrowWidthRatio;
        var (bx, by) = (tip.X - dir.X * len, tip.Y - dir.Y * len);
        var (px, py) = (-dir.Y * w / 2, dir.X * w / 2);
        return new[]
        {
            World(tip, m),
            World(new Vec2(bx + px, by + py), m),
            World(new Vec2(bx - px, by - py), m),
        };
    }

    private static Vec2 World(Vec2 p, Matrix m) => LineFamily.World(p, m);
}
