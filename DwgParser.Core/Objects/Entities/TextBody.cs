using DwgParser.Core.IO;

namespace DwgParser.Core.Objects;

internal readonly struct TextBody
{
    public readonly Vec2 Insert;
    public readonly Vec2 Alignment;
    public readonly double Height;
    public readonly double Rotation;
    public readonly double WidthFactor;
    public readonly double Oblique;
    public readonly string Value;
    public readonly short GenFlags;
    public readonly short HorizMode;
    public readonly short VertMode;
    public readonly double Elevation;
    public readonly double ExtrusionZ;
    public readonly double Thickness;

    public TextBody(Vec2 insert, Vec2 alignment, double height, double rotation, double widthFactor,
        double oblique, string value, short genFlags, short horizMode, short vertMode,
        double elevation = 0.0, double extrusionZ = 1.0, double thickness = 0.0)
    {
        Insert = insert;
        Alignment = alignment;
        Height = height;
        Rotation = rotation;
        WidthFactor = widthFactor;
        Oblique = oblique;
        Value = value;
        GenFlags = genFlags;
        HorizMode = horizMode;
        VertMode = vertMode;
        Elevation = elevation;
        ExtrusionZ = extrusionZ;
        Thickness = thickness;
    }

    public void AssignText(DwgEntity e)
    {
        e.Start = Insert;
        e.Height = Height;
        e.Rotation = Rotation;
        e.WidthFactor = WidthFactor;
        e.TextValue = Value;
        e.ObliqueAngle = Oblique;
        e.TextGenerationFlags = GenFlags;
        e.HorizMode = HorizMode;
        e.VertMode = VertMode;
        e.AlignmentPoint = Alignment;
        e.Elevation = Elevation;
        e.ExtrusionZ = ExtrusionZ;
        e.Thickness = Thickness;
    }
}

internal static class TextBodyReader
{
    public static TextBody ReadR2000Plus(BitReader br, DwgObjectRecord? record)
    {
        byte dataFlags = br.ReadRC();
        double elevation = 0.0;
        if ((dataFlags & 0x01) == 0)
            elevation = br.ReadRD();
        var ins = br.Read2RD();
        var align = ((dataFlags & 0x02) == 0)
            ? br.Read2DD(ins.X, ins.Y)
            : ins;
        var extrusion = br.ReadBE();
        double thickness = br.ReadBT();
        double oblique = 0.0;
        if ((dataFlags & 0x04) == 0) oblique = br.ReadRD();
        double rotation = 0.0;
        if ((dataFlags & 0x08) == 0) rotation = br.ReadRD();
        double height = br.ReadRD();
        double widthFactor = 1.0;
        if ((dataFlags & 0x10) == 0) widthFactor = br.ReadRD();
        long valStart = br.BitPosition;
        string value = br.ReadTV();
        record?.CaptureString(valStart, br.BitPosition, value);
        short gen = 0, hAlign = 0, vAlign = 0;
        if ((dataFlags & 0x20) == 0) gen = br.ReadBS();
        if ((dataFlags & 0x40) == 0) hAlign = br.ReadBS();
        if ((dataFlags & 0x80) == 0) vAlign = br.ReadBS();
        return new TextBody(new Vec2(ins.X, ins.Y), new Vec2(align.X, align.Y),
            height, rotation, widthFactor, oblique, value, gen, hAlign, vAlign,
            elevation, extrusion.Z, thickness);
    }
}
