namespace DwgParser.Core.Objects.Records;

public sealed class MaterialCapture : RecordIdentity, IMaterial
{
    public string Name = "";

    public string Description = "";

    public long NameStartBit;
    public int NameBitLen;

    public long DescStartBit;
    public int DescBitLen;

    public long HsBit;

    public bool Captured;

    public List<int> MapSources = new(6);

    public bool HasMapWalk;

    public int ChannelFlags;


    public MaterialColor AmbientColor;

    public MaterialColor DiffuseColor;

    public MaterialColor SpecularColor;

    public readonly MaterialMap[] Maps = new MaterialMap[6];

    public double SpecularGlossFactor;

    public double OpacityPercent;

    public double RefractionIndex;


    public double Translucence;

    public double SelfIllumination;

    public double Reflectivity;

    public long IlluminationModel;

    public long DiskChannelFlags;

    public long Mode;

    public bool SinceCaptured;

    public bool NumericCaptured;

    string IMaterial.Name => Name;
    string IMaterial.Description => Description;
    bool IMaterial.Captured => Captured;
}

public readonly record struct MaterialColor(int Flag, double Factor, long Rgb);

public sealed class MaterialMap
{
    public string Channel = "";

    public double BlendFactor;

    public int Projection;

    public int Tiling;

    public int Autotransform;

    public double[] Transmatrix = System.Array.Empty<double>();

    public int Source;

    public string Filename = "";

    public long FilenameStartBit = -1;
    public int FilenameBitLen;
}
