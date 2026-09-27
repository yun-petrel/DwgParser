namespace DwgParser.Core.Objects;

public interface ILeader : IEntity
{
    int VertexCount { get; }

    short LeaderAnnotType { get; }

    short LeaderPathType { get; }

    double LeaderBoxHeight { get; }

    double LeaderBoxWidth { get; }

    Vec3 LeaderOrigin { get; }

    Vec3 LeaderExtrusion { get; }

    Vec3 LeaderXDirection { get; }

    Vec3 LeaderInsertionOffset { get; }

    Vec3 LeaderEndPointProjection { get; }

    bool LeaderHooklineDir { get; }

    bool LeaderArrowheadOn { get; }

    short LeaderArrowheadType { get; }

    bool LeaderUnknownBit1 { get; }

    bool LeaderTailUnknownBit4 { get; }

    bool LeaderTailUnknownBit5 { get; }
}
