using System.Collections.Generic;
using DwgParser.Core.Render;

namespace DwgParser.Core.Objects;

public interface IEntity
{
    DwgEntityType Kind { get; }

    ulong Handle { get; }

    ulong LayerHandle { get; }

    ulong OwnerHandle { get; }

    short ColorIndex { get; }

    byte EntMode { get; }

    string ToSvg(RenderContext ctx, Matrix transform);
}
