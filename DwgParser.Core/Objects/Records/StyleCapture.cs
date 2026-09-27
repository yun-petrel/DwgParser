namespace DwgParser.Core.Objects.Records;

public sealed class StyleCapture : RecordIdentity, IStyle
{
    public long NameStartBit;

    public long NameEndBit;

    public int XrefResolved;

    public long XrefEndBit;

    public long FontStartBit;

    public long FontEndBit;

    public long DataEndBit;

    public string Name = "";

    public string Font = "";

    public string BigFont = "";


    public int Generation;

    public double LastHeight;

    public bool IsShape;

    public bool IsVertical;

    public double FixedHeight;

    public double WidthFactor = 1.0;

    public double ObliqueAngle;

    public bool HasAnchors => DataEndBit > 0;

    public string FailReason = "";
    public long FailBit = -1;

    public bool ValuesCommitted;

    public bool DownStringsAlive;

    public bool InlineValuesCommitted;

    string IStyle.Name => Name;
    string IStyle.FontFile => Font;
    string IStyle.BigfontFile => BigFont;
    bool IStyle.IsShape => IsShape;
    bool IStyle.IsVertical => IsVertical;
    double IStyle.FixedHeight => FixedHeight;
    double IStyle.WidthFactor => WidthFactor;
    double IStyle.ObliqueAngle => ObliqueAngle;
    int IStyle.XrefResolved => XrefResolved;
    bool IStyle.Captured => HasAnchors;
    int IStyle.Generation => Generation;
    double IStyle.LastHeight => LastHeight;
}
