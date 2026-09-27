using DwgParser.Core.Format;
using DwgParser.Core.Objects;

namespace DwgParser.Core.Render;

public static class RenderExtensions
{
    public static string RenderToSvg(this IDwgFile file)
    {
        var walk = file.GetObjects();
        return SvgRenderer.Render(walk);
    }

    public static void RenderToSvgFile(this IDwgFile file, string outputPath)
    {
        var svg = RenderToSvg(file);
        File.WriteAllText(outputPath, svg);
    }
}
