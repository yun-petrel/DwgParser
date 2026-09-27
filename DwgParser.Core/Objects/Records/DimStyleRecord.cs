using DwgParser.Core.IO;

namespace DwgParser.Core.Objects.Records;

public abstract class DimStyleRecord : IDimStyle
{
    public const short TypeCodeValue = 0x45;

    protected readonly DwgObjectRecord Rec;

    protected readonly DwgObjectReader.WalkResult Result;

    protected readonly int ObjStart;

    protected readonly ulong Handle;

    protected uint Bitsize;

    protected long NameStart, NameEnd, BodyStart, DimpostEnd, DimapostEnd;
    protected long CmcStart, CmcEnd, LwdStart, FxlonEnd, Flag0End, HsExpected;
    protected int XrefResolved;

    public abstract string Family { get; }

    protected DimStyleRecord(ulong handle, DwgObjectRecord rec,
        DwgObjectReader.WalkResult result, int objStart)
    {
        Handle = handle;
        Rec = rec;
        Result = result;
        ObjStart = objStart;
        Cap = rec.DimStyle ??= new DimStyleCapture();
        Cap.OwnHandle = handle;
        Info = new DwgObjectReader.DimStyleInfo
        {
            Handle = handle,
            TypeCode = TypeCodeValue,
            SourceVersion = rec.SourceVersion,
        };
    }

    public DimStyleCapture Cap { get; }

    public DwgObjectReader.DimStyleInfo Info { get; }

    ulong IRecord.Handle => Handle;
    public short TypeCode => TypeCodeValue;
    public DwgVersion SourceVersion => Rec.SourceVersion;
    public string Name { get => Info.Name; set => Info.Name = value; }
    public string Dimpost { get => Info.Dimpost; set => Info.Dimpost = value; }
    public string Dimapost { get => Info.Dimapost; set => Info.Dimapost = value; }
    public int Dimalt { get => Info.Dimalt; set => Info.Dimalt = value; }
    public double Dimaltf { get => Info.Dimaltf; set => Info.Dimaltf = value; }
    public int Dimaltd { get => Info.Dimaltd; set => Info.Dimaltd = value; }
    public int Dimadec { get => Info.Dimadec; set => Info.Dimadec = value; }
    public int Dimaltu { get => Info.Dimaltu; set => Info.Dimaltu = value; }
    public int Dimdec { get => Info.Dimdec; set => Info.Dimdec = value; }
    public int Dimzin { get => Info.Dimzin; set => Info.Dimzin = value; }
    public double Dimrnd { get => Info.Dimrnd; set => Info.Dimrnd = value; }
    public double Dimaltrnd { get => Info.Dimaltrnd; set => Info.Dimaltrnd = value; }
    public int Dimtad { get => Info.Dimtad; set => Info.Dimtad = value; }
    public int Dimtdec { get => Info.Dimtdec; set => Info.Dimtdec = value; }
    public int DimalttD { get => Info.DimalttD; set => Info.DimalttD = value; }
    public int Dimaunit { get => Info.Dimaunit; set => Info.Dimaunit = value; }
    public int Dimfrac { get => Info.Dimfrac; set => Info.Dimfrac = value; }
    public int Dimlunit { get => Info.Dimlunit; set => Info.Dimlunit = value; }
    public int Dimdsep { get => Info.Dimdsep; set => Info.Dimdsep = value; }
    public int Dimtmove { get => Info.Dimtmove; set => Info.Dimtmove = value; }
    public bool Decoded { get => Info.Decoded; set => Info.Decoded = value; }
    public bool TailDecoded { get => Info.TailDecoded; set => Info.TailDecoded = value; }

    public double Dimscale { get => Info.Dimscale; set => Info.Dimscale = value; }
    public double Dimasz { get => Info.Dimasz; set => Info.Dimasz = value; }
    public double Dimexo { get => Info.Dimexo; set => Info.Dimexo = value; }
    public double Dimdli { get => Info.Dimdli; set => Info.Dimdli = value; }
    public double Dimexe { get => Info.Dimexe; set => Info.Dimexe = value; }
    public double Dimdle { get => Info.Dimdle; set => Info.Dimdle = value; }
    public double Dimtp { get => Info.Dimtp; set => Info.Dimtp = value; }
    public double Dimtm { get => Info.Dimtm; set => Info.Dimtm = value; }
    public bool Dimtol { get => Info.Dimtol; set => Info.Dimtol = value; }
    public bool Dimlim { get => Info.Dimlim; set => Info.Dimlim = value; }
    public bool Dimtih { get => Info.Dimtih; set => Info.Dimtih = value; }
    public bool Dimtoh { get => Info.Dimtoh; set => Info.Dimtoh = value; }
    public bool Dimse1 { get => Info.Dimse1; set => Info.Dimse1 = value; }
    public bool Dimse2 { get => Info.Dimse2; set => Info.Dimse2 = value; }
    public int Dimazin { get => Info.Dimazin; set => Info.Dimazin = value; }
    public double Dimtxt { get => Info.Dimtxt; set => Info.Dimtxt = value; }
    public double Dimcen { get => Info.Dimcen; set => Info.Dimcen = value; }
    public double Dimtsz { get => Info.Dimtsz; set => Info.Dimtsz = value; }
    public double Dimlfac { get => Info.Dimlfac; set => Info.Dimlfac = value; }
    public double Dimtvp { get => Info.Dimtvp; set => Info.Dimtvp = value; }
    public double Dimtfac { get => Info.Dimtfac; set => Info.Dimtfac = value; }
    public double Dimgap { get => Info.Dimgap; set => Info.Dimgap = value; }
    public bool Dimtofl { get => Info.Dimtofl; set => Info.Dimtofl = value; }
    public bool Dimsah { get => Info.Dimsah; set => Info.Dimsah = value; }
    public bool Dimtix { get => Info.Dimtix; set => Info.Dimtix = value; }
    public bool Dimsoxd { get => Info.Dimsoxd; set => Info.Dimsoxd = value; }
    public uint Dimclrd { get => Info.Dimclrd; set => Info.Dimclrd = value; }
    public uint Dimclre { get => Info.Dimclre; set => Info.Dimclre = value; }
    public uint Dimclrt { get => Info.Dimclrt; set => Info.Dimclrt = value; }
    public string DimclrdName { get => Info.DimclrdName; set => Info.DimclrdName = value; }
    public string DimclrdBookName { get => Info.DimclrdBookName; set => Info.DimclrdBookName = value; }
    public string DimclreName { get => Info.DimclreName; set => Info.DimclreName = value; }
    public string DimclreBookName { get => Info.DimclreBookName; set => Info.DimclreBookName = value; }
    public string DimclrtName { get => Info.DimclrtName; set => Info.DimclrtName = value; }
    public string DimclrtBookName { get => Info.DimclrtBookName; set => Info.DimclrtBookName = value; }
    public int Dimjust { get => Info.Dimjust; set => Info.Dimjust = value; }
    public bool Dimsd1 { get => Info.Dimsd1; set => Info.Dimsd1 = value; }
    public bool Dimsd2 { get => Info.Dimsd2; set => Info.Dimsd2 = value; }
    public int Dimtolj { get => Info.Dimtolj; set => Info.Dimtolj = value; }
    public int Dimtzin { get => Info.Dimtzin; set => Info.Dimtzin = value; }
    public int Dimaltz { get => Info.Dimaltz; set => Info.Dimaltz = value; }
    public int Dimalttz { get => Info.Dimalttz; set => Info.Dimalttz = value; }
    public bool Dimupt { get => Info.Dimupt; set => Info.Dimupt = value; }
    public int Dimatfit { get => Info.Dimatfit; set => Info.Dimatfit = value; }
    public bool Dimfxlon { get => Info.Dimfxlon; set => Info.Dimfxlon = value; }
    public bool Flag0 { get => Info.Flag0; set => Info.Flag0 = value; }
    public long Dimlwd { get => Info.Dimlwd; set => Info.Dimlwd = value; }
    public long Dimlwe { get => Info.Dimlwe; set => Info.Dimlwe = value; }
    public double Dimfxl { get => Info.Dimfxl; set => Info.Dimfxl = value; }
    public double Dimjogang { get => Info.Dimjogang; set => Info.Dimjogang = value; }
    public int Dimtfill { get => Info.Dimtfill; set => Info.Dimtfill = value; }
    public uint Dimtfillclr { get => Info.Dimtfillclr; set => Info.Dimtfillclr = value; }
    public string DimtfillclrName { get => Info.DimtfillclrName; set => Info.DimtfillclrName = value; }
    public string DimtfillclrBookName { get => Info.DimtfillclrBookName; set => Info.DimtfillclrBookName = value; }
    public int Dimarcsym { get => Info.Dimarcsym; set => Info.Dimarcsym = value; }

    public bool Dimtxtdirection { get => Info.Dimtxtdirection; set => Info.Dimtxtdirection = value; }
    public double Dimaltmzf { get => Info.Dimaltmzf; set => Info.Dimaltmzf = value; }
    public double Dimmzf { get => Info.Dimmzf; set => Info.Dimmzf = value; }
    public string Dimaltmzs { get => Info.Dimaltmzs; set => Info.Dimaltmzs = value; }
    public string Dimmzs { get => Info.Dimmzs; set => Info.Dimmzs = value; }

    internal virtual void Read(BitReader br)
    {
        if (!ReadFrameHead(br)) return;
        ReadObjectFlags(br);

        NameStart = br.BitPosition;
        Name = br.ReadTV();
        NameEnd = br.BitPosition;
        XrefResolved = ReadXref(br);
        BodyStart = br.BitPosition;
        Dimpost = br.ReadTV();
        DimpostEnd = br.BitPosition;
        Dimapost = br.ReadTV();
        DimapostEnd = br.BitPosition;

        ReadScaleRun(br);
        ReadR2007aHead(br);
        ReadTolRun(br);
        ReadArcSym(br);
        ReadTextRun(br);

        if (!double.IsFinite(Dimaltf) || Dimaltf <= 0) return;
        if (Dimaltd is < 0 or > 8) return;
        Decoded = true;
        Cap.ValuesCommitted = true;
        Result.DimStyles[Handle] = Info;

        try { ReadTail(br); } catch {  }
    }

    protected abstract bool ReadFrameHead(BitReader br);

    protected virtual void ReadObjectFlags(BitReader br)
    {
        br.ReadBL();
        if (HasXdicMissing) br.ReadB();
        if (HasDsData) br.ReadB();
    }

    protected virtual bool HasXdicMissing => false;

    protected virtual bool HasDsData => false;

    protected virtual int ReadXref(BitReader br)
    {
        br.ReadB();
        int resolved = br.ReadBS();
        br.ReadB();
        return resolved;
    }

    protected virtual void ReadR2007aHead(BitReader br) { }

    protected virtual void ReadArcSym(BitReader br) { }

    protected void ReadScaleRun(BitReader br)
    {
        Dimscale = br.ReadBD();
        Dimasz = br.ReadBD();
        Dimexo = br.ReadBD();
        Dimdli = br.ReadBD();
        Dimexe = br.ReadBD();
        Dimrnd = br.ReadBD();
        Dimdle = br.ReadBD();
        Dimtp = br.ReadBD();
        Dimtm = br.ReadBD();
        NineBdEnd = br.BitPosition;
    }

    protected long NineBdEnd { get; private set; }

    protected void ReadTolRun(BitReader br)
    {
        Dimtol = br.ReadB();
        Dimlim = br.ReadB();
        Dimtih = br.ReadB();
        Dimtoh = br.ReadB();
        Dimse1 = br.ReadB();
        Dimse2 = br.ReadB();
        Dimtad = br.ReadBS();
        Dimzin = br.ReadBS();
        Dimazin = br.ReadBS();
        ThreeBsEnd = br.BitPosition;
    }

    protected long ThreeBsEnd { get; private set; }

    protected void ReadTextRun(BitReader br)
    {
        Dimtxt = br.ReadBD();
        Dimcen = br.ReadBD();
        Dimtsz = br.ReadBD();
        Dimaltf = br.ReadBD();
        Dimlfac = br.ReadBD();
        Dimtvp = br.ReadBD();
        Dimtfac = br.ReadBD();
        Dimgap = br.ReadBD();
        Dimaltrnd = br.ReadBD();
        Dimalt = br.ReadB() ? 1 : 0;
        Dimaltd = br.ReadBS();
    }

    protected virtual void ReadTail(BitReader br) { }

    protected bool ReadTailFlags(BitReader br, long frameEnd)
    {
        Dimtofl = br.ReadB();
        Dimsah = br.ReadB();
        Dimtix = br.ReadB();
        Dimsoxd = br.ReadB();
        CmcStart = br.BitPosition;
        Dimclrd = CmcRgb(br, out string clrdName, out string clrdBook);
        DimclrdName = clrdName; DimclrdBookName = clrdBook;
        Dimclre = CmcRgb(br, out string clreName, out string clreBook);
        DimclreName = clreName; DimclreBookName = clreBook;
        Dimclrt = CmcRgb(br, out string clrtName, out string clrtBook);
        DimclrtName = clrtName; DimclrtBookName = clrtBook;
        CmcEnd = br.BitPosition;
        int dimadec = br.ReadBS();
        int dimdec = br.ReadBS();
        int dimtdec = br.ReadBS();
        int dimaltu = br.ReadBS();
        int dimalttD = br.ReadBS();
        int dimaunit = br.ReadBS();
        int dimfrac = br.ReadBS();
        int dimlunit = br.ReadBS();
        int dimdsep = br.ReadBS();
        int dimtmove = br.ReadBS();
        if (br.BitPosition > frameEnd) return false;
        if (dimadec is < 0 or > 8) return false;
        if (dimdec is < 0 or > 8) return false;
        if (dimtdec is < 0 or > 8) return false;
        if (dimaltu is < 0 or > 8) return false;
        if (dimalttD is < 0 or > 8) return false;
        if (dimaunit is < 0 or > 7) return false;
        if (dimfrac is < 0 or > 4) return false;
        if (dimlunit is < 0 or > 7) return false;
        int[] sepOk = { 0, 44, 46, 32, 59, 47, 45, 37, 43, 42, 61, 126, 95, 35, 36, 64, 96, 33, 38, 40, 41, 58 };
        if (Array.IndexOf(sepOk, dimdsep) < 0) return false;
        if (dimtmove is < 0 or > 2) return false;
        Dimadec = dimadec; Dimdec = dimdec; Dimtdec = dimtdec;
        Dimaltu = dimaltu; DimalttD = dimalttD; Dimaunit = dimaunit;
        Dimfrac = dimfrac; Dimlunit = dimlunit; Dimdsep = dimdsep; Dimtmove = dimtmove;
        TailDecoded = true;
        return true;
    }

    protected virtual uint CmcRgb(BitReader br, out string colorName, out string colorBookName)
    {
        _ = DwgObjectReader.ReadCmc(br, out uint rgb, out colorName, out colorBookName);
        return rgb;
    }

    internal void CommitHandleRefs()
    {
        Cap.RecHandleStreamStart = Rec.HandleStreamStartBit;
        DimStyleHandleWalk.Capture(Cap, Result.SourceData, Cap.HandleStreamStart, (long)Rec.BodyEnd * 8);
    }

    protected void ReadTailGeometry(BitReader br)
    {
        Dimjust = br.ReadBS();
        Dimsd1 = br.ReadB();
        Dimsd2 = br.ReadB();
        Dimtolj = br.ReadBS();
        Dimtzin = br.ReadBS();
        Dimaltz = br.ReadBS();
        Dimalttz = br.ReadBS();
        Dimupt = br.ReadB();
        Dimatfit = br.ReadBS();
        LwdStart = br.BitPosition;
        if (SourceVersion >= DwgVersion.AC1021)
        {
            Dimfxlon = br.ReadB();
        }
        FxlonEnd = br.BitPosition;
        Dimlwd = br.ReadBS();
        Dimlwe = br.ReadBS();
        Flag0 = br.ReadB();
        Flag0End = br.BitPosition;
        HsExpected = (long)ObjStart * 8 + Bitsize;
    }
}
