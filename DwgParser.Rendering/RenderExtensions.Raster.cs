using System.Text;
using DwgParser.Core.Format;
using DwgParser.Core.Objects;
using DwgParser.Core.Render;

namespace DwgParser.Rendering;

public static class RenderExtensions
{
    public static void RenderToPng(this IDwgFile file, string outputPath,
        int width = 2048, int height = 2048, int lineThickness = 1)
    {
        var walk = file.GetObjects();
        var rasterizer = new SoftwareRasterizer(width, height, lineThickness: lineThickness);
        rasterizer.Render(walk);
        PngEncoder.Save(rasterizer.Buffer, outputPath);
    }

    public static byte[] RenderToPngBytes(this IDwgFile file,
        int width = 2048, int height = 2048, int lineThickness = 1)
    {
        var walk = file.GetObjects();
        var rasterizer = new SoftwareRasterizer(width, height, lineThickness: lineThickness);
        rasterizer.Render(walk);
        return PngEncoder.Encode(rasterizer.Buffer);
    }

    public static void RenderToPng(this IEntity entity, string outputPath,
        int width = 512, int height = 512, int lineThickness = 1)
    {
        var rasterizer = new SoftwareRasterizer(width, height, lineThickness: lineThickness);
        rasterizer.Render(new[] { entity });
        PngEncoder.Save(rasterizer.Buffer, outputPath);
    }

    public static void RenderToPngByFrame(this IDwgFile file, string outputPath,
        int width = 2048, int height = 2048, int lineThickness = 2)
    {
        var walk = file.GetObjects();
        var renderer = new FrameRenderer();
        var buf = renderer.Render(walk, width, height, lineThickness);
        PngEncoder.Save(buf, outputPath);
    }

    public static byte[] RenderToPngByFrameBytes(this IDwgFile file,
        int width = 2048, int height = 2048, int lineThickness = 2)
    {
        var walk = file.GetObjects();
        var renderer = new FrameRenderer();
        var buf = renderer.Render(walk, width, height, lineThickness);
        return PngEncoder.Encode(buf);
    }

    public static byte[] RenderToPngLayeredBytes(this IDwgFile file,
        int width = 2048, int height = 2048, int lineThickness = 2)
    {
        var walk = file.GetObjects();
        var renderer = new LayeredFrameRenderer();
        return PngEncoder.Encode(renderer.Render(walk, width, height, lineThickness));
    }

    public static (double MinX, double MinY, double MaxX, double MaxY)?
        DetectFrame(this IDwgFile file)
    {
        var walk = file.GetObjects();
        var renderer = new FrameRenderer();
        renderer.Render(walk, 1, 1);
        return renderer.FrameBounds;
    }
}
