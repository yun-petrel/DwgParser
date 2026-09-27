namespace DwgParser.Core.Objects;

public interface IDimension : IEntity
{
    string DimSubtype { get; }

    double Measurement { get; }

    string TextValue { get; }

    Vec2 DimTextMid { get; }

    double DimTextRotation { get; }

    Vec2 DimDefPoint { get; }

    Vec2 DimXLine1 { get; }

    Vec2 DimXLine2 { get; }

    double DimLineRotation { get; }

    Vec2 DimCenter { get; }

    List<Vec2> Points { get; }

    double TextHorizontalDir { get; }

    short TextAttachment { get; }

    byte DimensionTextFlags { get; }

    Vec2 DimensionCloneInsPt { get; }

    double LeaderLength { get; }

    int DimensionClassVersion { get; }

    int OrdinateAxisFlag { get; }

    long DimLspaceStyle { get; }

    double DimLspaceFactor { get; }

    (double X, double Y, double Z) DimInsScale { get; }

    bool DimFlipArrow1 { get; }

    bool DimFlipArrow2 { get; }
}
