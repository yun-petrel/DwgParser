namespace DwgParser.Core.Objects.Records;

public sealed class LtypeCapture
{
    public long PatStartBit;

    public long SaStartBit;

    public int XrefResolved;


    public string Name = "";

    public string Description = "";

    public double PatternLen;

    public int Alignment;

    public int NumDashes;

    public List<LtypeRecord.LtypeDash> Dashes { get; } = new();

    public bool ValuesCommitted;


    public long DownNameInsBit;

    public long DownDescInsBit;

    public long DownPatEndBit;

    public bool HasDownAnchors => DownPatEndBit > 0;

    public bool HasAnchors => SaStartBit > 0;
}
