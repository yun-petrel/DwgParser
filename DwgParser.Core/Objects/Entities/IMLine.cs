namespace DwgParser.Core.Objects;

public interface IMLine : IEntity
{
    double Scale { get; }

    int Justification { get; }

    int Flags { get; }

    int LineCount { get; }

    int VertexCount { get; }

    System.Collections.Generic.IReadOnlyList<MLineVertex> Vertices { get; }

    ulong MLineStyleHandle { get; }
}

public sealed class MLineLine
{
    public double[] SegmentParams { get; internal set; } = System.Array.Empty<double>();

    public double[] AreaFillParams { get; internal set; } = System.Array.Empty<double>();
}

public sealed class MLineVertex
{
    public double X { get; internal set; }
    public double Y { get; internal set; }
    public double Z { get; internal set; }
    public double DirX { get; internal set; }
    public double DirY { get; internal set; }
    public double DirZ { get; internal set; }
    public double MiterX { get; internal set; }
    public double MiterY { get; internal set; }
    public double MiterZ { get; internal set; }
    public MLineLine[] Lines { get; internal set; } = System.Array.Empty<MLineLine>();
}
