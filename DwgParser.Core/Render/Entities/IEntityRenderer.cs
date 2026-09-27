using DwgParser.Core.Objects;

namespace DwgParser.Core.Render;

internal interface IEntityRenderer
{
    void Render(DwgEntity entity, RenderContext ctx, Matrix toWorld, IDrawDevice device);
}
