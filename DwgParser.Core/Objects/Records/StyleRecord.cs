using DwgParser.Core.IO;

namespace DwgParser.Core.Objects.Records;

public abstract class StyleRecord : IStyle
{
    public const short TypeCodeValue = 0x35;

    protected readonly DwgObjectRecord Rec;

    protected readonly DwgObjectReader.WalkResult Result;

    protected readonly int ObjStart;

    protected readonly ulong Handle;

    protected uint Bitsize;

    protected long NameStart, NameEnd, FontStart, FontEnd, DataEnd;
    protected int XrefValue;

    protected StyleRecord(ulong handle, DwgObjectRecord rec,
        DwgObjectReader.WalkResult result, int objStart)
    {
        Handle = handle;
        Rec = rec;
        Result = result;
        ObjStart = objStart;
        Cap = rec.Style ??= new StyleCapture();
        Cap.AttachIdentity(rec);
    }

    public StyleCapture Cap { get; }

    public abstract string Family { get; }

    ulong IRecord.Handle => Handle;
    public short TypeCode => TypeCodeValue;
    public DwgVersion SourceVersion => Rec.SourceVersion;
    public string Name { get; protected set; } = "";
    public string FontFile { get; protected set; } = "";
    public string BigfontFile { get; protected set; } = "";
    public bool IsShape { get; protected set; }
    public bool IsVertical { get; protected set; }
    public double FixedHeight { get; protected set; }
    public double WidthFactor { get; protected set; } = 1.0;
    public double ObliqueAngle { get; protected set; }

    public int Generation { get; protected set; }

    public double LastHeight { get; protected set; }
    public int XrefResolved => XrefValue;
    public bool Captured => Cap.HasAnchors;

    protected void Fail(BitReader br, string why)
    {
        if (string.IsNullOrEmpty(Cap.FailReason))
        {
            Cap.FailReason = why;
            Cap.FailBit = br.BitPosition - Rec.CarryStartBit;
        }
    }

    internal virtual void Read(BitReader br)
    {
        if (!ReadFrameHead(br)) { Fail(br, "帧首(EED 不合法)"); return; }
        br.ReadBL();
        if (HasXdicMissing) br.ReadB();
        if (HasDsData) br.ReadB();

        NameStart = br.BitPosition;
        string name = br.ReadTV();
        NameEnd = br.BitPosition;
        bool named = !string.IsNullOrEmpty(name);
        if (named)
        {
            Name = name;
            Result.StyleNames[Handle] = name;
        }

        XrefValue = ReadXref(br);
        long xrefEnd = br.BitPosition;

        bool isShape = br.ReadB();
        bool isVertical = br.ReadB();

        double textSize = br.ReadBD();
        double widthFactor = br.ReadBD();
        double oblique = br.ReadBD();
        Generation = br.ReadRC();
        LastHeight = br.ReadBD();
        FontStart = br.BitPosition;
        string fontFile = br.ReadTV();
        FontEnd = br.BitPosition;
        string bigfontFile = br.ReadTV();
        DataEnd = br.BitPosition;

        IsShape = isShape;
        IsVertical = isVertical;
        FixedHeight = textSize;
        WidthFactor = widthFactor > 0 ? widthFactor : 1.0;
        ObliqueAngle = oblique;
        FontFile = fontFile;
        BigfontFile = bigfontFile;
        if (named)
            Result.StyleFonts[Handle] = new DwgObjectReader.TextStyleInfo
            {
                Handle = Handle,
                SourceVersion = Rec.SourceVersion,
                XrefResolved = XrefValue,
                Captured = true,
                Generation = Generation,
                LastHeight = LastHeight,
                Name = name,
                FontFile = fontFile,
                BigfontFile = bigfontFile,
                IsShape = isShape,
                IsVertical = isVertical,
                FixedHeight = textSize,
                WidthFactor = widthFactor > 0 ? widthFactor : 1.0,
                ObliqueAngle = oblique,
            };

        if (SubmitsCapture && Bitsize > 0 && DataEnd == (long)ObjStart * 8 + Bitsize)
        {
            Cap.NameStartBit = NameStart;
            Cap.NameEndBit = NameEnd;
            Cap.XrefResolved = XrefValue;
            Cap.XrefEndBit = xrefEnd;
            Cap.FontStartBit = FontStart;
            Cap.FontEndBit = FontEnd;
            Cap.DataEndBit = DataEnd;
            if (named)
                Rec.StringFields.Add(new DwgObjectRecord.StringField(
                    NameStart, (int)(NameEnd - NameStart), name));
            Rec.StringFields.Add(new DwgObjectRecord.StringField(
                FontStart, (int)(FontEnd - FontStart), fontFile));
            Rec.StringFields.Add(new DwgObjectRecord.StringField(
                FontEnd, (int)(DataEnd - FontEnd), bigfontFile));
            if (named) { CommitValues(); Cap.InlineValuesCommitted = true; }
            else
            {
                Cap.Name = ""; Cap.Font = fontFile; Cap.BigFont = bigfontFile;
                Cap.FailReason = "契约:匿名样式不提值轴(§170)";
            }
        }
        else if (SubmitsCentralValues)
        {
            CommitCentralValues(br, named);
        }
        else if (!named)
        {
            Fail(br, "契约:匿名样式不提值轴(§170)");
        }
        else
        {
            Fail(br, "两条提交闸均不适用(射程)");
        }
    }

    private void CommitValues()
    {
        Cap.Name = Name;
        Cap.Font = FontFile;
        Cap.BigFont = BigfontFile;
        Cap.XrefResolved = XrefValue;
        Cap.IsShape = IsShape;
        Cap.IsVertical = IsVertical;
        Cap.FixedHeight = FixedHeight;
        Cap.WidthFactor = WidthFactor;
        Cap.ObliqueAngle = ObliqueAngle;
        Cap.Generation = Generation;
        Cap.LastHeight = LastHeight;
    }

    private void CommitCentralValues(BitReader br, bool named)
    {
        uint bits = Rec.SourceVersion.IsR2010Plus()
            ? (uint)(Rec.HandleStreamStartBit - (long)ObjStart * 8)
            : Bitsize;
        if (bits == 0) { Fail(br, "集中流 bits=0 未定位"); return; }
        long dataEnd = (long)ObjStart * 8 + bits;
        if (dataEnd <= NameStart || dataEnd > (long)Rec.BodyEnd * 8)
        { Fail(br, "集中流数据区界不合理"); return; }
        if (!CentralStrings.TryMeasure(Rec, br.UnderlyingData(), ObjStart, bits,
                out BitReader str, out _, out long areaEnd))
        { Fail(br, "集中流串区量不出"); return; }
        if (!CentralStrings.ReadAll(str, areaEnd, 12, out var strings))
        { Fail(br, "集中流 TU 逐枚读出越界"); return; }
        if (strings.Count < 3) { Fail(br, $"串区枚数={strings.Count}<3"); return; }
        if (strings[0] != Name) { Fail(br, "串区[0]≠走查 name"); return; }
        if (strings[1] != FontFile) { Fail(br, "串区[1]≠走查 font"); return; }
        if (strings[2] != BigfontFile) { Fail(br, "串区[2]≠走查 bigfont"); return; }
        if (double.IsNaN(FixedHeight) || double.IsNaN(WidthFactor)
            || double.IsNaN(ObliqueAngle) || double.IsNaN(LastHeight))
        { Fail(br, "语义值含 NaN"); return; }

        CommitValues();
        Cap.DownStringsAlive = true;
        if (named) Cap.ValuesCommitted = true;
        else Cap.FailReason = "契约:匿名样式不提值轴(§170)";
    }

    protected virtual bool SubmitsCentralValues => false;
    protected virtual bool ReadFrameHead(BitReader br)
    {
        Bitsize = br.ReadRL();
        br.ReadHandle();
        Rec.EedStartBit = br.BitPosition;
        if (!DwgObjectReader.TrySkipEed(br)) return false;
        Rec.EedBitLen = (int)(br.BitPosition - Rec.EedStartBit);
        Rec.HandleStreamStartBit = (long)ObjStart * 8 + Bitsize;
        Rec.HasHandleStream = true;
        if (AttachCentralStringStream)
            DwgObjectReader.AttachStringStream(br, ObjStart, Bitsize, Rec.SourceVersion, Rec);
        return true;
    }

    protected virtual bool AttachCentralStringStream => false;

    protected virtual bool HasXdicMissing => false;

    protected virtual bool HasDsData => false;

    protected virtual int ReadXref(BitReader br)
    {
        br.ReadB();
        int resolved = br.ReadBS();
        br.ReadB();
        return resolved;
    }

    protected virtual bool SubmitsCapture => false;
}
