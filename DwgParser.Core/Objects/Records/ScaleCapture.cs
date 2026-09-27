namespace DwgParser.Core.Objects.Records;

public sealed class ScaleCapture : RecordIdentity, IScale
{
    public string Name { get; internal set; } = "";

    public int Flag { get; internal set; }

    public double PaperUnits { get; internal set; }

    public double DrawingUnits { get; internal set; }

    public bool IsUnitScale { get; internal set; }

    public bool Captured { get; internal set; }

    public long DownNameInsertBit;
    public long DownTailEndBit;
}
