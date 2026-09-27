using DwgParser.Core.Objects;

namespace DwgParser.Core.Render;

internal sealed class TextEntityRenderer : IEntityRenderer
{
    public static readonly TextEntityRenderer Instance = new();

    public void Render(DwgEntity e, RenderContext ctx, Matrix m, IDrawDevice device)
    {
        if (string.IsNullOrEmpty(e.TextValue)) return;

        var anchor = LineFamily.World(e.Start, m);
        ctx.Expand(anchor.X, anchor.Y);
        bool mt = e.Kind == DwgEntityType.MText;
        double height = e.Height * m.YAxisScale;
        int hAlign, vAlign;
        double lineGap = 1.0;
        if (mt)
        {
            int att = e.TextAttachment is >= 1 and <= 9 ? e.TextAttachment : 1;
            hAlign = ((att - 1) % 3) switch { 0 => 0, 1 => 1, _ => 2 };
            vAlign = ((att - 1) / 3) switch { 0 => 3, 1 => 2, _ => 1 };
            lineGap = e.LineSpacingFactor > 0 ? e.LineSpacingFactor : 1.0;
        }
        else
        {
            hAlign = e.HorizMode switch { 1 => 1, 2 => 2, 4 => 1, _ => 0 };
            vAlign = e.VertMode switch { 1 => 1, 2 => 2, 3 => 3, _ => 0 };
        }
        device.DrawText(new TextRequest(e.TextValue, anchor,
            new TextStyle(ctx.ResolveFontFamily(e.TextStyleHandle, e.TextValue),
                          height, e.Rotation, TextAlignment.BaseLeft,
                          Rgba.FromHex(ctx.EntityColor(e))),
            new RasterTextSpec(hAlign, vAlign, e.HorizMode, e.VertMode,
                LineFamily.World(e.AlignmentPoint, m), e.WidthFactor, lineGap, mt,
                ctx.StyleFonts.TryGetValue(e.TextStyleHandle, out var st) ? st : null)));
    }
}
