using DwgParser.Core.IO;

namespace DwgParser.Core.Objects.Records;

internal abstract class DimStyleAcR2010Plus : DimStyleRecord
{
    protected DimStyleAcR2010Plus(ulong handle, DwgObjectRecord rec,
        DwgObjectReader.WalkResult result, int objStart) : base(handle, rec, result, objStart) { }

    protected override bool ReadFrameHead(BitReader br) => true;

    internal override void Read(BitReader br)
    {
        if (SourceVersion < DwgVersion.AC1024) return;
        if (!Rec.HasHandleStream) return;
        long hs = Rec.HandleStreamStartBit;
        if (hs <= br.BitPosition || hs > (long)Rec.BodyEnd * 8) return;
        long ownStart = br.BitPosition;
        try
        {
            br.ReadHandle();
            if (!DwgObjectReader.TrySkipEed(br)) return;
            int numReactors = br.ReadBL();
            br.ReadB();
            if (HasDsData) br.ReadB();
            if (numReactors is < 0 or > 1024) return;
            int xrefResolved = br.ReadBS();
            long xrefEnd = br.BitPosition;
            ReadScaleRun(br);
            Dimfxl = br.ReadBD();
            Dimjogang = br.ReadBD();
            Dimtfill = br.ReadBS();
            Dimtfillclr = CmcRgb(br, out string fillName, out string fillBook);
            DimtfillclrName = fillName; DimtfillclrBookName = fillBook;
            long headEnd = br.BitPosition;
            ReadTolRun(br);
            Dimarcsym = br.ReadBS();
            long arcSymEnd = br.BitPosition;
            ReadTextRun(br);
            _ = ReadTailFlags(br, hs);
            Dimjust = br.ReadBS();
            Dimsd1 = br.ReadB();
            Dimsd2 = br.ReadB();
            Dimtolj = br.ReadBS();
            Dimtzin = br.ReadBS();
            Dimaltz = br.ReadBS();
            Dimalttz = br.ReadBS();
            Dimupt = br.ReadB();
            Dimatfit = br.ReadBS();
            long atFitEnd = br.BitPosition;
            Dimfxlon = br.ReadB();
            FxlonEnd = br.BitPosition;
            Dimtxtdirection = br.ReadB();
            Dimaltmzf = br.ReadBD();
            Dimmzf = br.ReadBD();
            long blockBits = br.BitPosition - FxlonEnd;
            Dimlwd = br.ReadBS();
            Dimlwe = br.ReadBS();
            Flag0 = br.ReadB();
            Flag0End = br.BitPosition;

            if (blockBits <= 0 || blockBits > 256) return;
            if (Rec.StringStreamStartBit != Flag0End) return;
            var saved = br.StringStream;
            br.StringStream = null;
            string? name = br.ReadTV();
            string? dimpost = br.ReadTV();
            string? dimapost = br.ReadTV();
            string? dimaltmzs = br.ReadTV();
            string? dimmzs = br.ReadTV();
            br.StringStream = saved;
            int ssSize = br.ReadRS();
            bool hasStrings = br.ReadB();
            if (!hasStrings || br.BitPosition != hs) return;
            if (string.IsNullOrEmpty(name) || name.Length > 255) return;
            if (ssSize <= 0 || Flag0End + ssSize + 17 != hs) return;

            Cap.DownCaptured = true;
            Cap.DownBlockBits = (int)blockBits;
            Cap.FxlonEndBit = FxlonEnd;
            Cap.Flag0EndBit = Flag0End;
            Cap.DownXrefEndBit = xrefEnd;
            Cap.XrefResolved = xrefResolved;
            Cap.DownNineBdEndBit = NineBdEnd;
            Cap.DownHeadEndBit = headEnd;
            Cap.DownThreeBsEndBit = ThreeBsEnd;
            Cap.DownArcSymEndBit = arcSymEnd;
            Cap.DownAtFitEndBit = atFitEnd;
            Cap.CmcStartBit = CmcStart;
            Cap.CmcEndBit = CmcEnd;
            Cap.Name = name;
            Cap.Dimpost = dimpost;
            Cap.Dimapost = dimapost;
            if (double.IsFinite(Dimaltf) && Dimaltf > 0 && Dimaltd is >= 0 and <= 8)
            {
                Name = name;
                Dimpost = dimpost;
                Dimapost = dimapost;
                Dimaltmzs = dimaltmzs ?? "";
                Dimmzs = dimmzs ?? "";
                Decoded = true;
                Cap.ValuesCommitted = true;
                Result.DimStyles[Handle] = Info;
            }
            if (System.Environment.GetEnvironmentVariable("DWGDBG") == "1")
                System.Console.Error.WriteLine(
                    $"DSDOWN h={Rec.Handle:X} t={Rec.TypeCode} own={ownStart} fxlonEnd={FxlonEnd} " +
                    $"block={blockBits} flag0End={Flag0End} ss={ssSize} hs={hs} name='{name}' " +
                    $"txtdir={Dimtxtdirection} altmzf={Dimaltmzf} mmzf={Dimmzf} " +
                    $"s4='{dimaltmzs}' s5='{dimmzs}'");
        }
        catch
        {
        }
    }
}

internal sealed class DimStyleAcR2010 : DimStyleAcR2010Plus
{
    public DimStyleAcR2010(ulong handle, DwgObjectRecord rec,
        DwgObjectReader.WalkResult result, int objStart) : base(handle, rec, result, objStart) { }

    public override string Family => "R2010";
}

internal sealed class DimStyleAcR2013 : DimStyleAcR2010Plus
{
    public DimStyleAcR2013(ulong handle, DwgObjectRecord rec,
        DwgObjectReader.WalkResult result, int objStart) : base(handle, rec, result, objStart) { }

    public override string Family => "R2013";

    protected override bool HasDsData => true;
}

internal sealed class DimStyleAcR2018 : DimStyleAcR2010Plus
{
    public DimStyleAcR2018(ulong handle, DwgObjectRecord rec,
        DwgObjectReader.WalkResult result, int objStart) : base(handle, rec, result, objStart) { }

    public override string Family => "R2018";

    protected override bool HasDsData => true;
}
