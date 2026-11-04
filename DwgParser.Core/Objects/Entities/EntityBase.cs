namespace DwgParser.Core.Objects;

public abstract class EntityBase
{
    public string LtypeName { get; set; } = "";
    public DwgEntityType Kind { get; set; }
    public ulong Handle { get; set; }
    public short ColorIndex { get; set; } = 256;
    public uint TrueColor { get; set; }
    public bool HasTrueColor { get; set; }
    public uint ColorAlpha { get; set; }
    public bool HasColorAlpha { get; set; }
    public string ColorName { get; set; } = "";
    public string ColorBookName { get; set; } = "";
    public byte EntMode { get; set; }

    public byte[]? AcisData { get; set; }

    public Vec3[]? AcisVertices { get; set; }

    public ulong OwnerHandle { get; set; }

    public ulong LayerHandle { get; set; }

    public double ExtrusionZ { get; set; } = 1.0;
    public double Thickness { get; set; }
    public double Elevation { get; set; }

    public bool IsErased { get; set; }
}
