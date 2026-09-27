namespace DwgParser.Core.Objects;

public interface IFace : IEntity
{
    int CornerCount { get; }

    int InvisFlags { get; }

    System.Collections.Generic.IReadOnlyList<double> CornerZs { get; }

    bool CornerZIsZero { get; }
}
