using DwgParser.Core.IO;

namespace DwgParser.Core.Objects;

public abstract class FaceEntity : DwgEntity, IFace
{
    public int CornerCount => Corners.Count;

    public int InvisFlags { get; protected set; }

    public System.Collections.Generic.IReadOnlyList<double> CornerZs => CornerZsList;

    protected readonly System.Collections.Generic.List<double> CornerZsList = new();

    public bool CornerZIsZero { get; protected set; }

    public abstract void ReadBody(BitReader br);
}
