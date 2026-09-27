using DwgParser.Core.IO;
using DwgParser.Core.Render;

namespace DwgParser.Core.Objects;

public abstract class ViewportEntity : DwgEntity, IViewport
{

    protected void ReadHead(BitReader br)
    {
        var c = br.Read3BD();
        Center = new Vec2(c.X, c.Y);
        Elevation = c.Z;
        VportWidth = br.ReadBD();
        Height = br.ReadBD();
    }

    public abstract void ReadBody(BitReader br);


    public double LensLength { get; protected set; }

    public long CircleZoom { get; protected set; }

    public long GridMajor { get; protected set; } = -1;

    public Vec2 SnapBase { get; protected set; }

    public Vec2 SnapUnit { get; protected set; }

    public Vec2 GridUnit { get; protected set; }

    public long NumFrozenLayers { get; protected set; }

    public long StatusFlag { get; protected set; }

    public string StyleSheet { get; protected set; } = "";

    public int RenderMode { get; protected set; }


    public bool UcsAtOrigin { get; protected set; }

    public bool UcsVp { get; protected set; }

    public Vec3 UcsOrigin { get; protected set; }

    public Vec3 UcsXDir { get; protected set; }

    public Vec3 UcsYDir { get; protected set; }

    public double UcsElevation { get; protected set; }

    public int UcsOrthoView { get; protected set; }

    public int ShadePlotMode { get; protected set; } = -1;

    public bool HasLighting { get; protected set; }

    public bool UseDefaultLights { get; protected set; }

    public int DefaultLightingType { get; protected set; }

    public double Brightness { get; protected set; }

    public double Contrast { get; protected set; }

    public int AmbientColorIndex { get; protected set; }

    public uint AmbientTrueColor { get; protected set; }

    public string AmbientColorName { get; protected set; } = "";

    public string AmbientColorBookName { get; protected set; } = "";

    public long TailEndBit { get; protected set; }
}
