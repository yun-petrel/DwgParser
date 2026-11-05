using System.Text;
using DwgParser.Core.Objects;

namespace DwgParser.Core.Render;

public static class SvgRenderer
{
    public static string Render(
        DwgObjectReader.WalkResult result,
        bool flipY = true,
        double margin = 10.0,
        bool darkBackground = false)
    {
        var ctx = new RenderContext(result) { DarkBackground = darkBackground };

        var sb = new StringBuilder();
        foreach (var e in ctx.ModelSpaceEntities)
            sb.Append(e.ToSvg(ctx, Matrix.Identity));

        if (ctx.ModelSpaceEntities.Count == 0)
        {
            var referenced = new HashSet<ulong>();
            foreach (var e in ctx.ModelSpaceEntities)
                if (e.Kind == DwgEntityType.Insert)
                    referenced.Add(e.OwnerHandle);
            foreach (var (blk, list) in ctx.BlockContents)
            {
                if (referenced.Contains(blk))
                    continue;
                foreach (var e in list)
                    sb.Append(e.ToSvg(ctx, Matrix.Identity));
            }
        }

        string body = sb.ToString();
        if (body.Length == 0)
            body = "<!-- no renderable entities -->";

        var (minX, minY, maxX, maxY) = ctx.BlueBounds ?? ctx.Bounds;
        if (!double.IsFinite(minX) || maxX - minX < 1e-9)
        {
            minX = -100; maxX = 100; minY = -100; maxY = 100;
        }
        double w = maxX - minX;
        double h = maxY - minY;
        double cx = (minX + maxX) / 2;
        double cy = (minY + maxY) / 2;
        double scale = Math.Min(1000.0 / w, 1000.0 / h);
        if (!double.IsFinite(scale) || scale <= 0) scale = 1;
        double vw = w * scale + 2 * margin;
        double vh = h * scale + 2 * margin;
        double vbMinX = cx * scale - vw / 2;
        double vbMinY = cy * scale - vh / 2;
        double flipY0 = 2 * cy * scale;
        string bodyTransform = flipY
            ? $"translate(0,{RenderContext.F(flipY0)}) scale({RenderContext.F(scale)},{RenderContext.F(-scale)})"
            : $"scale({RenderContext.F(scale)})";

        string bgColor = darkBackground ? "#1e1e1e" : "white";

        return $"<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
               $"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"{RenderContext.F(vbMinX)} {RenderContext.F(vbMinY)} {RenderContext.F(vw)} {RenderContext.F(vh)}\" " +
               $"width=\"{RenderContext.F(vw)}\" height=\"{RenderContext.F(vh)}\">\n" +
               $"  <rect x=\"{RenderContext.F(vbMinX)}\" y=\"{RenderContext.F(vbMinY)}\" width=\"{RenderContext.F(vw)}\" height=\"{RenderContext.F(vh)}\" fill=\"{bgColor}\"/>\n" +
               $"  <g transform=\"{bodyTransform}\">\n" +
               body +
               "  </g>\n" +
               "</svg>\n";
    }
}