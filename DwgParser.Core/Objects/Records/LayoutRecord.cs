using DwgParser.Core.Format;
using DwgParser.Core.IO;

namespace DwgParser.Core.Objects.Records;

public abstract class LayoutRecord : ILayout
{
    public const short TypeCodeValue = 0x52;

    protected readonly DwgObjectRecord Rec;

    protected readonly int ObjStart;

    protected readonly ulong Handle;

    protected LayoutRecord(ulong handle, DwgObjectRecord rec, int objStart)
    {
        Handle = handle;
        Rec = rec;
        ObjStart = objStart;
        Cap = rec.Layout ??= new LayoutCapture();
        Cap.AttachIdentity(rec);
    }

    public LayoutCapture Cap { get; }

    public abstract string Family { get; }

    ulong IRecord.Handle => Handle;
    public short TypeCode => TypeCodeValue;
    public DwgVersion SourceVersion => Rec.SourceVersion;
    public string LayoutName { get; protected set; } = "";
    public bool Captured => Cap.Captured;

    internal virtual void Read(BitReader br)
    {
        DwgObjectRecord.SeekTo(br, Rec.CarryStartBit);
        uint bitsize = br.ReadRL();
        long hs = (long)ObjStart * 8 + bitsize;
        if (hs <= br.BitPosition || hs > (long)Rec.BodyEnd * 8) return;
        long ownStart = br.BitPosition;
        br.ReadHandle();
        long eedStart = br.BitPosition;
        if (!DwgObjectReader.TrySkipEed(br)) return;
        Rec.EedStartBit = eedStart;
        Rec.EedBitLen = (int)(br.BitPosition - eedStart);
        if (br.ReadBL() is < 0 or > 10000) return;
        if (HasXdicMissing) br.ReadB();

        bool bad = false;
        void BD() { if (double.IsNaN(br.ReadBD())) bad = true; }
        void RD() { if (double.IsNaN(br.ReadRD())) bad = true; }
        void BDs(int n) { for (int i = 0; i < n && !bad; i++) BD(); }
        void RDs(int n) { for (int i = 0; i < n && !bad; i++) RD(); }
        void BS() { br.ReadBS(); }

        var slots = new (long start, long end, string val)[5];
        int ns = 0;
        string TV()
        {
            long s = br.BitPosition; string v = br.ReadTV();
            slots[ns++] = (s, br.BitPosition, v);
            return v;
        }

        string printer = TV();
        string paper = TV();
        BS();
        BDs(6);
        string media = TV();
        BDs(2);
        BS(); BS(); BS();
        BDs(2);
        BDs(2);
        if (ReadsPlotViewName)
        {
            Cap.PlotViewStartBit = br.BitPosition;
            br.ReadBD();
            br.ReadBD();
            Cap.PlotViewEndBit = br.BitPosition;
        }
        BD();
        BD();
        string stylesheet = "";
        if (ReadsStylesheet)
            stylesheet = TV();
        BS();
        BD();
        BDs(2);
        if (ReadsShadeplot) { BS(); BS(); BS(); }
        string layoutName = TV();
        BS();
        BS();
        BDs(3);
        RDs(2);
        RDs(2);
        BDs(3);
        BDs(3);
        BDs(3);
        BD();
        BS();
        BDs(3);
        BDs(3);
        if (ReadsNumViewports) br.ReadBL();

        bool dbg = System.Environment.GetEnvironmentVariable("DWGDBG") == "1";
        if (dbg)
            System.Console.Error.WriteLine(
                $"LAYCAP h={Rec.Handle:X} t={Rec.TypeCode} hs={hs} end={br.BitPosition} Δ={(int)(br.BitPosition - hs)} " +
                $"bad={bad} ns={ns} name='{layoutName}' printer='{printer}' paper='{paper}' media='{media}' style='{stylesheet}' " +
                $"anchors=[{slots[0].start},{slots[0].end}][{slots[1].start},{slots[1].end}][{slots[2].start},{slots[2].end}]" +
                $"[{slots[3].start},{slots[3].end}][{slots[4].start},{slots[4].end}] pv=[{Cap.PlotViewStartBit},{Cap.PlotViewEndBit}]");

        if (bad || ns != ExpectedStringCount || br.BitPosition != hs) return;
        for (int i = 0; i < ns; i++)
            if (slots[i].start < ownStart || slots[i].end > hs) return;
        if (ReadsPlotViewName
            && (Cap.PlotViewStartBit < ownStart || Cap.PlotViewEndBit > hs
                || Cap.PlotViewEndBit <= Cap.PlotViewStartBit)) return;

        for (int i = 0; i < ns; i++)
        {
            Cap.StringStarts[i] = slots[i].start;
            Cap.StringEnds[i] = slots[i].end;
        }
        if (SplitHandleStream)
        {
            Rec.HandleStreamStartBit = hs;
            Rec.HasHandleStream = true;
            foreach (var (start, end, val) in slots)
                Rec.CaptureString(start, end, val);
        }
        Cap.PrinterCfgFile = printer;
        Cap.PaperSize = paper;
        Cap.CanonicalMediaName = media;
        Cap.Stylesheet = stylesheet;
        Cap.LayoutName = layoutName;
        Cap.Captured = true;
        LayoutName = layoutName;
    }

    protected virtual bool HasXdicMissing => true;

    protected virtual bool ReadsPlotViewName => false;
    protected virtual bool ReadsStylesheet => true;
    protected virtual int ExpectedStringCount => 5;
    protected virtual bool ReadsShadeplot => true;
    protected virtual bool ReadsNumViewports => true;
    protected virtual bool SplitHandleStream => true;
}
