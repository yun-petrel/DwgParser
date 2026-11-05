namespace DwgParser.Core.Objects;

public sealed class InsertGeometry
{
    public ulong BlockHeaderHandle { get; set; }
    public bool HasAttributes { get; set; }
    public double ScaleX { get; set; } = 1.0;
    public double ScaleY { get; set; } = 1.0;
    public double ScaleZ { get; set; } = 1.0;
}

public sealed class LeaderGeometry
{
    public Vec2 Dir { get; set; }
    public bool ArrowheadOn { get; set; }
    public short ArrowheadType { get; set; }
}

public sealed class ViewportGeometry
{
    public double VportWidth { get; set; }
    public Vec2 VportViewCenter { get; set; }
    public double VportViewSize { get; set; }
    public double VportTwist { get; set; }
    public double VportSnapAngle { get; set; }
    public Vec2 VportViewDirection { get; set; }
    public double VportFrontClipZ { get; set; }
    public double VportBackClipZ { get; set; }
    public Vec2 VportViewTarget { get; set; }
}
