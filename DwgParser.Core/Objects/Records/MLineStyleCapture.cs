namespace DwgParser.Core.Objects.Records;

public sealed class MLineStyleCapture : RecordIdentity, IMLineStyle
{
    public string Name = "";

    public string Description = "";

    public long NameStartBit;
    public int NameBitLen;

    public long DescStartBit;
    public int DescBitLen;

    public long EedStartBit;
    public int EedBitLen;

    public bool Captured;

    public bool SemanticCaptured;
    public List<(long Start, long End)> CmcSpans = new();

    public int Flag;

    public int FillIdx;
    public uint FillRgb;
    public byte FillFlag;

    public double StartAngle;

    public double EndAngle;

    public MLineStyleLine[]? Lines;

    public sealed class MLineStyleLine
    {
        public double Offset;
        public int ColorIdx;
        public uint ColorRgb;
        public byte ColorFlag;
        public int LtIndex;
    }

    string IMLineStyle.Name => Name;
    string IMLineStyle.Description => Description;
    bool IMLineStyle.Captured => Captured;
    bool IMLineStyle.SemanticCaptured => SemanticCaptured;
    int IMLineStyle.Flag => Flag;
    (int Idx, uint Rgb, byte Flag) IMLineStyle.FillColor => (FillIdx, FillRgb, FillFlag);
    double IMLineStyle.StartAngle => StartAngle;
    double IMLineStyle.EndAngle => EndAngle;
    MLineStyleCapture.MLineStyleLine[]? IMLineStyle.Lines => Lines;
}
