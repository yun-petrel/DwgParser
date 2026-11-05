namespace DwgParser.Core.Objects;

public sealed class TextGeometry
{
    public string TextValue { get; set; } = "";
    public string Tag { get; set; } = "";
    public string Prompt { get; set; } = "";
    public byte AttFlags { get; set; }
    public short AttFieldLength { get; set; }
    public int AttIsLockedInBlock { get; set; } = -1;
    public int AttKeepDuplicateRecords { get; set; } = -1;
    public int AttMTextType { get; set; } = -1;
    public short TextAttachment { get; set; }
    public double RectWidth { get; set; }
    public double RectHeight { get; set; }
    public short LineSpacingStyle { get; set; }
    public double LineSpacingFactor { get; set; } = 1.0;
    public Vec2 AlignmentPoint { get; set; }
    public double ObliqueAngle { get; set; }
    public short TextGenerationFlags { get; set; }
    public short HorizMode { get; set; }
    public short VertMode { get; set; }
    public ulong TextStyleHandle { get; set; }
}

public sealed class DimensionGeometry
{
    public double Measurement { get; set; }
    public Vec2 DimTextMid { get; set; }
    public Vec2 DimXLine1 { get; set; }
    public Vec2 DimXLine2 { get; set; }
    public Vec2 DimDefPoint { get; set; }
    public Vec2 DimDefPoint2 { get; set; }
    public Vec2 DimLinePoint { get; set; }
    public Vec2 DimCenter { get; set; }
    public ulong DimBlockHandle { get; set; }
    public ulong DimStyleHandle { get; set; }
    public string DimSubtype { get; set; } = "";
    public double DimTextRotation { get; set; }
    public double DimLineRotation { get; set; }
}

public sealed class TableGeometry
{
    public short TableRows { get; set; }
    public short TableColumns { get; set; }
}
