using DwgParser.Core.Format;
using DwgParser.Core.IO;

namespace DwgParser.Core.Objects.Records;

public abstract class MaterialRecord : IMaterial
{
    public const short TypeCodeValue = 508;

    protected readonly DwgObjectRecord Rec;

    protected readonly int ObjStart;

    protected readonly ulong Handle;

    protected MaterialRecord(ulong handle, DwgObjectRecord rec, int objStart)
    {
        Handle = handle;
        Rec = rec;
        ObjStart = objStart;
        Cap = rec.Material ??= new MaterialCapture();
        Cap.AttachIdentity(rec);
    }

    public MaterialCapture Cap { get; }

    public abstract string Family { get; }

    ulong IRecord.Handle => Handle;
    public short TypeCode => TypeCodeValue;
    public DwgVersion SourceVersion => Rec.SourceVersion;
    public string Name { get; protected set; } = "";
    public string Description { get; protected set; } = "";
    public bool Captured => Cap.Captured;

    internal virtual void Read(BitReader br)
    {
        DwgObjectRecord.SeekTo(br, Rec.CarryStartBit);
        uint bitsize = br.ReadRL();
        long hs = (long)ObjStart * 8 + bitsize;
        if (hs <= br.BitPosition || hs > (long)Rec.BodyEnd * 8) return;
        long ownStart = br.BitPosition;
        br.ReadHandle();
        if (!DwgObjectReader.TrySkipEed(br)) return;
        if (br.ReadBL() is < 0 or > 8) return;
        if (HasXdicMissing) br.ReadB();

        long nameStart = br.BitPosition;
        string name = br.ReadTV();
        long nameEnd = br.BitPosition;
        long descStart = nameEnd;
        string desc = br.ReadTV();
        long descEnd = br.BitPosition;

        bool dbg = System.Environment.GetEnvironmentVariable("DWGDBG") == "1";
        if (dbg)
            System.Console.Error.WriteLine(
                $"MATREC h={Rec.Handle:X} t={Rec.TypeCode} hs={hs} name='{name}'" +
                $"[{nameStart - ownStart}..{nameEnd - ownStart}] desc='{desc}'" +
                $"[{descStart - ownStart}..{descEnd - ownStart}]");

        if (name.Length == 0 || nameStart < ownStart || descEnd > hs
            || descEnd - descStart < 2 || desc.Length > 255) return;

        var reg = new MaterialRegion { EndBit = hs };
        bool mapsOk = TryWalkRegion(br, reg);

        if (Rec.SourceVersion >= DwgVersion.AC1018)
        {
            Rec.HandleStreamStartBit = hs;
            Rec.HasHandleStream = true;
        }
        Cap.HsBit = hs;
        Rec.StringFields.Add(new DwgObjectRecord.StringField(nameStart, (int)(nameEnd - nameStart), name));
        Rec.StringFields.Add(new DwgObjectRecord.StringField(descStart, (int)(descEnd - descStart), desc));
        if (mapsOk)
        {
            var sources = new List<int>(6);
            foreach (var slot in reg.Maps)
            {
                var m = slot.Value;
                sources.Add(m.Source);
                if (m.Source == 1)
                    Rec.StringFields.Add(new DwgObjectRecord.StringField(
                        m.FilenameStartBit, m.FilenameBitLen, m.Filename));
            }
            Cap.MapSources = sources;
            Cap.HasMapWalk = true;
            Cap.ChannelFlags = (sources.Contains(1) || sources.Contains(2)) ? 0x3F : 0x7F;
            CommitRegion(Cap, reg);
        }
        Cap.Name = name;
        Cap.Description = desc;
        Cap.NameStartBit = nameStart;
        Cap.NameBitLen = (int)(nameEnd - nameStart);
        Cap.DescStartBit = descStart;
        Cap.DescBitLen = (int)(descEnd - descStart);
        Cap.Captured = true;
        Name = name;
        Description = desc;
    }

    private static bool TryWalkRegion(BitReader br, MaterialRegion reg)
    {
        try
        {
            WalkRegion(br, reg);
        }
        catch (Exception)
        {
            return false;
        }
        return reg.Closed && br.BitPosition == reg.EndBit;
    }

    internal static void WalkRegion(BitReader br, MaterialRegion reg, bool inlineFilenames = true)
    {
        Color(br, reg.Ambient); Color(br, reg.Diffuse); Map(br, reg.Maps[0], inlineFilenames);
        Color(br, reg.Specular); Map(br, reg.Maps[1], inlineFilenames);
        reg.Scalars[0] = br.ReadBD();
        Map(br, reg.Maps[2], inlineFilenames);
        reg.Scalars[1] = br.ReadBD();
        Map(br, reg.Maps[3], inlineFilenames); Map(br, reg.Maps[4], inlineFilenames);
        reg.Scalars[2] = br.ReadBD();
        Map(br, reg.Maps[5], inlineFilenames);
        reg.Closed = true;

        static void Color(BitReader r, MaterialColorSlot slot)
        {
            byte flag = r.ReadRC();
            double factor = r.ReadBD();
            long rgb = flag == 1 ? r.ReadBL() : 0;
            slot.Value = new MaterialColor(flag, factor, rgb);
        }

        static void Map(BitReader r, MaterialMapSlot slot, bool inlineFilename)
        {
            var m = new MaterialMap { Channel = slot.Channel };
            m.BlendFactor = r.ReadBD();
            m.Projection = r.ReadRC();
            m.Tiling = r.ReadRC();
            m.Autotransform = r.ReadRC();
            var matrix = new double[16];
            for (int i = 0; i < 16; i++) matrix[i] = r.ReadBD();
            m.Transmatrix = matrix;
            m.Source = r.ReadRC();
            if (inlineFilename && m.Source == 1)
            {
                m.FilenameStartBit = r.BitPosition;
                m.Filename = r.ReadTV();
                m.FilenameBitLen = (int)(r.BitPosition - m.FilenameStartBit);
            }
            slot.Value = m;
        }
    }

    internal static void WalkSinceR2007(BitReader br, MaterialSince reg)
    {
        reg.Translucence = br.ReadBD();
        reg.SelfIllumination = br.ReadBD();
        reg.Reflectivity = br.ReadBD();
        reg.IlluminationModel = br.ReadBL();
        reg.ChannelFlags = br.ReadBL();
        reg.Mode = br.ReadBL();
    }

    internal static void CommitRegion(MaterialCapture cap, MaterialRegion reg)
    {
        cap.AmbientColor = reg.Ambient.Value;
        cap.DiffuseColor = reg.Diffuse.Value;
        cap.SpecularColor = reg.Specular.Value;
        for (int i = 0; i < reg.Maps.Length; i++) cap.Maps[i] = reg.Maps[i].Value;
        cap.SpecularGlossFactor = reg.Scalars[0];
        cap.OpacityPercent = reg.Scalars[1];
        cap.RefractionIndex = reg.Scalars[2];
        cap.NumericCaptured = true;
    }

    protected virtual bool HasXdicMissing => true;
}

internal sealed class MaterialColorSlot
{
    public MaterialColor Value;
}

internal sealed class MaterialMapSlot
{
    public MaterialMapSlot(string channel) => Channel = channel;
    public string Channel { get; }
    public MaterialMap Value = new();
}

internal sealed class MaterialRegion
{
    public static readonly string[] Channels =
        { "diffuse", "specular", "reflection", "opacity", "bump", "refraction" };

    public readonly MaterialColorSlot Ambient = new();
    public readonly MaterialColorSlot Diffuse = new();
    public readonly MaterialColorSlot Specular = new();
    public readonly MaterialMapSlot[] Maps =
    {
        new(Channels[0]), new(Channels[1]), new(Channels[2]),
        new(Channels[3]), new(Channels[4]), new(Channels[5]),
    };

    public readonly double[] Scalars = new double[3];

    public long EndBit;

    public bool Closed;
}

internal sealed class MaterialSince
{
    public double Translucence, SelfIllumination, Reflectivity;
    public long IlluminationModel, ChannelFlags, Mode;
}
