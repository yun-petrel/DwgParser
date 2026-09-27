namespace DwgParser.Core.Objects.Records;

public sealed class LayoutCapture : RecordIdentity, ILayout
{
    public string PrinterCfgFile = "";
    public string PaperSize = "";
    public string CanonicalMediaName = "";
    public string Stylesheet = "";
    public string LayoutName = "";

    public bool Captured;

    public readonly long[] StringStarts = new long[5];
    public readonly long[] StringEnds = new long[5];
    public long PlotViewStartBit, PlotViewEndBit;

    public bool DowngradeCaptured;

    public readonly List<LayoutDowngradeString> Downgrade = new();

    public long XdicEndBit;
    public long LastRefEndBit;
    public ulong LayoutBlockHandle;
    public List<ulong>? ViewportHandles;

    string ILayout.LayoutName => LayoutName;
    bool ILayout.Captured => Captured;
}

public readonly record struct LayoutDowngradeString(long Pos, string Value);
