namespace DwgParser.Core.Objects;

public interface IMText : IEntity
{
    string TextValue { get; }

    Vec2 Start { get; }

    double Height { get; }

    double RectWidth { get; }

    double RectHeight { get; }

    short Attachment { get; }

    double Rotation { get; }

    short LineSpacingStyle { get; }

    double LineSpacingFactor { get; }

    double ExtrusionZ { get; }

    long BgFillScale { get; }

    long BgFillTrans { get; }

    bool LineSpacingUnknownBit { get; }
}
