namespace DwgParser.Core.Objects;

public interface IViewport : IEntity
{
    Vec2 Center { get; }

    double VportWidth { get; }

    double LensLength { get; }

    long CircleZoom { get; }

    long GridMajor { get; }

    Vec2 SnapBase { get; }

    Vec2 SnapUnit { get; }

    Vec2 GridUnit { get; }

    long NumFrozenLayers { get; }

    long StatusFlag { get; }

    string StyleSheet { get; }

    int RenderMode { get; }

    bool UcsAtOrigin { get; }

    bool UcsVp { get; }

    Vec3 UcsOrigin { get; }

    Vec3 UcsXDir { get; }

    Vec3 UcsYDir { get; }

    double UcsElevation { get; }

    int UcsOrthoView { get; }

    int ShadePlotMode { get; }

    bool HasLighting { get; }

    bool UseDefaultLights { get; }

    int DefaultLightingType { get; }

    double Brightness { get; }

    double Contrast { get; }

    int AmbientColorIndex { get; }

    uint AmbientTrueColor { get; }

    string AmbientColorName { get; }

    string AmbientColorBookName { get; }
}
