namespace DwgParser.Core.Objects;

public sealed class LineGeometry
{
    public Vec2 Start { get; set; }
    public Vec2 End { get; set; }
    public double StartZ { get; set; }
    public double EndZ { get; set; }
}

public sealed class RadialGeometry
{
    public Vec2 Center { get; set; }
    public double Radius { get; set; }
    public double StartAngle { get; set; }
    public double EndAngle { get; set; }
}

public sealed class PolylineGeometry
{
    public List<Vec2> Points { get; } = new();
    public List<double> Bulges { get; } = new();
    public double ConstantWidth { get; set; }
    public bool Closed { get; set; }
    public List<double> VertexStartWidths { get; } = new();
    public List<double> VertexEndWidths { get; } = new();
    public bool PlineGen { get; set; }
}

public sealed class CornersGeometry
{
    public List<Vec2> Corners { get; } = new();
    public bool SolidFill { get; set; }
}
