using DwgParser.Core.Objects;

namespace DwgParser.Core.Render;

public interface IDrawDevice
{
    void BeginPass(DrawViewport view);

    void EndPass();

    void DrawLine(Vec2 a, Vec2 b, StrokeStyle stroke);

    void DrawRect(Vec2 cornerA, Vec2 cornerB, StrokeStyle stroke);

    void DrawArc(Vec2 center, double radius, double a0, double a1, StrokeStyle stroke);

    void DrawCircle(Vec2 center, double radius, StrokeStyle stroke, FillStyle fill);

    void DrawPolyline(ReadOnlyMemory<Vec2> pts, bool closed, StrokeStyle stroke, FillStyle fill);

    void FillPolygon(ReadOnlyMemory<Vec2> ring, FillStyle fill);

    void DrawPath(ReadOnlyMemory<PathSeg> segs, bool closed, StrokeStyle stroke, FillStyle fill, PathLayout layout);

    void DrawText(TextRequest text);

    void PushState() { }

    void PopState() { }
}
