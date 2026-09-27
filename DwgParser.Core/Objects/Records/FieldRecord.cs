using DwgParser.Core.IO;

namespace DwgParser.Core.Objects.Records;

public abstract class FieldRecord : IField
{
    internal const long MaxChildSlots = 20_000;

    protected readonly DwgObjectRecord Rec;
    protected readonly int ObjStart;
    protected readonly ulong Handle;

    protected FieldRecord(ulong handle, DwgObjectRecord rec, int objStart)
    {
        Handle = handle;
        Rec = rec;
        ObjStart = objStart;
        Cap = rec.Field ??= new FieldCapture();
        Cap.AttachIdentity(rec);
    }

    public FieldCapture Cap { get; }

    public abstract string Family { get; }

    ulong IRecord.Handle => Handle;
    public short TypeCode => Rec.LogicalType;
    public DwgVersion SourceVersion => Rec.SourceVersion;

    public string Id => Cap.Id;
    public string Code => Cap.Code;
    public int NumChilds => Cap.NumChilds;
    public bool IsXdicMissing => Cap.IsXdicMissing;
    public int NumReactors => Cap.NumReactors;
    public string Format1 => Cap.Format1;
    public string Format2 => Cap.Format2;
    public int EvaluationOption => Cap.EvaluationOption;
    public int FilingOption => Cap.FilingOption;
    public int FieldState => Cap.FieldState;
    public int EvaluationStatus => Cap.EvaluationStatus;
    public int EvaluationErrorCode => Cap.EvaluationErrorCode;
    public string EvaluationErrorMsg => Cap.EvaluationErrorMsg;
    public int ValueDataType => Cap.ValueDataType;
    public long ValueLong => Cap.ValueLong;
    public double ValueDouble => Cap.ValueDouble;
    public string? ValueString => Cap.ValueString;
    public string ValueString301 => Cap.ValueString301;
    public int ValueStringLength98 => Cap.ValueStringLength98;
    public int NumChildValues => Cap.NumChildValues;
    public IReadOnlyList<FieldChildValue> ChildValues => Cap.ChildValues;
    public bool Captured => Cap.Captured;

    protected virtual bool HasXdicMissing => false;

    protected virtual bool HasDsData => false;

    protected virtual bool Central => false;

    internal virtual void Read(BitReader br)
    {
        if (Central) { ReadCentral(br); return; }
        uint rl = br.ReadRL();
        long hs = (long)ObjStart * 8 + rl;
        if (hs <= br.BitPosition || hs > (long)Rec.BodyEnd * 8) { Fail(br, "帧界"); return; }
        try
        {
            ulong ownHandle = br.ReadHandle().Value;
            int unknownBs = br.ReadBS();
            int numChilds = br.ReadBS();
            if (numChilds < 0 || numChilds > MaxChildSlots) { Fail(br, "num_childs 越界"); return; }
            bool xdicMissing = false;
            if (HasXdicMissing) xdicMissing = br.ReadB();
            string id = br.ReadTV();
            string code = br.ReadTV();
            int numReactors = br.ReadBS();
            if (numReactors is < 0 or > 100_000) { Fail(br, "num_reactors 越界"); return; }
            string format1 = br.ReadTV();
            string format2 = br.ReadTV();
            int evalOption = br.ReadBS();
            int filingOption = br.ReadBS();
            int fieldState = br.ReadBS();
            int evalStatus = br.ReadBS();
            int evalErrorCode = br.ReadBS();
            string evalErrorMsg = br.ReadTV();
            int valueDt = br.ReadBS();
            long valueLong = 0;
            double valueDouble = 0;
            string? valueString = null;
            switch (valueDt)
            {
                case 0: valueLong = br.ReadBS(); break;
                case 2: valueDouble = br.ReadBD(); break;
                case 4: valueString = br.ReadTV(); break;
                default: Fail(br, $"主值 dt={valueDt} 未定谱"); return;
            }
            string valueString301 = br.ReadTV();
            int valueStringLength98 = br.ReadBS();
            int numChildValues = br.ReadBS();
            if (numChildValues < 0 || numChildValues > MaxChildSlots) { Fail(br, "num_childval 越界"); return; }
            var childs = new List<FieldChildValue>(numChildValues);
            for (int i = 0; i < numChildValues; i++)
            {
                var cv = new FieldChildValue { Key = br.ReadTV() };
                cv.DataType = br.ReadBS();
                switch (cv.DataType)
                {
                    case 0: cv.LongValue = br.ReadBS(); break;
                    case 2: cv.DoubleValue = br.ReadBD(); break;
                    case 4: cv.StringValue = br.ReadTV(); break;
                    default: Fail(br, $"childval[{i}].dt={cv.DataType} 未定谱"); return;
                }
                childs.Add(cv);
            }
            if (br.BitPosition != hs) { Fail(br, $"停点 {br.BitPosition - hs}"); return; }

            Cap.OwnHandle = ownHandle;
            Cap.UnknownBs = unknownBs;
            Cap.NumChilds = numChilds;
            Cap.IsXdicMissing = xdicMissing;
            Cap.Id = id;
            Cap.Code = code;
            Cap.NumReactors = numReactors;
            Cap.Format1 = format1;
            Cap.Format2 = format2;
            Cap.EvaluationOption = evalOption;
            Cap.FilingOption = filingOption;
            Cap.FieldState = fieldState;
            Cap.EvaluationStatus = evalStatus;
            Cap.EvaluationErrorCode = evalErrorCode;
            Cap.EvaluationErrorMsg = evalErrorMsg;
            Cap.ValueDataType = valueDt;
            Cap.ValueLong = valueLong;
            Cap.ValueDouble = valueDouble;
            Cap.ValueString = valueString;
            Cap.ValueString301 = valueString301;
            Cap.ValueStringLength98 = valueStringLength98;
            Cap.NumChildValues = numChildValues;
            Cap.ChildValues.Clear();
            Cap.ChildValues.AddRange(childs);
            Cap.WalkEndBit = br.BitPosition;
            Cap.FailReason = "";
            Cap.FailBit = -1;
            Cap.Captured = true;
        }
        catch (Exception ex)
        {
            Fail(br, ex.GetType().Name);
        }
    }

    internal void ReadCentral(BitReader br)
    {
        if (!CentralStrings.TryDataBits(Rec, ObjStart, br, out uint bitsize, out long dataEnd))
        { Fail(br, "集中流 bitsize 未定位"); return; }
        if (!CentralStrings.TryMeasure(Rec, br.UnderlyingData(), ObjStart, bitsize,
                out BitReader strings, out long areaStart, out long areaEnd))
        { Fail(br, "集中流串区量不出"); return; }

        var central = new List<string>();
        for (int i = 0; i < 9; i++) central.Add(strings.ReadTV());
        if (strings.BitPosition != areaEnd)
        { Fail(br, "串区不闭合(游标 " + strings.BitPosition + " vs 区末 " + areaEnd + ")"); return; }

        try
        {
            ulong ownHandle = br.ReadHandle().Value;
            int unknownBs = br.ReadBS();
            int numChilds = br.ReadBS();
            if (numChilds < 0 || numChilds > MaxChildSlots) { Fail(br, "num_childs 越界"); return; }
            bool xdicMissing = br.ReadB();
            bool dsData = HasDsData && br.ReadB();
            int numReactors = br.ReadBS();
            int unknownZ1 = br.ReadBS();
            int evalOption = br.ReadBS();
            int filingOption = br.ReadBS();
            int fieldState = br.ReadBS();
            int evalStatus = br.ReadBS();
            int evalErrorCode = br.ReadBS();
            int mainFmt = br.ReadBS();
            int mainDt = br.ReadBS();
            long mainLong = 0;
            double mainDouble = 0;
            string? mainString = null;
            switch (mainDt)
            {
                case 0: mainLong = br.ReadBL(); break;
                case 2: mainDouble = br.ReadBD(); break;
                case 4: mainString = ReadTuPayload(br); break;
                default: Fail(br, "主值 dt=" + mainDt + " 未定谱"); return;
            }
            int mainUnit = br.ReadBS();
            int vsl98 = central[5].TrimEnd('\0').Length > 0 ? (int)br.ReadBS() : 0;
            int numChildValues = br.ReadBS();
            if (numChildValues < 0 || numChildValues > MaxChildSlots) { Fail(br, "num_childval 越界"); return; }
            if (central.Count != 6 + 3 * numChildValues)
            { Fail(br, "串区枚数 " + central.Count + " ≠ 6+3×" + numChildValues); return; }

            var childs = new List<FieldChildValue>(numChildValues);
            for (int i = 0; i < numChildValues; i++)
            {
                var cv = new FieldChildValue { Key = central[6 + 3 * i], FormatFlags = (int)br.ReadBS() };
                cv.DataType = br.ReadBS();
                switch (cv.DataType)
                {
                    case 0: cv.LongValue = br.ReadBL(); break;
                    case 2: cv.DoubleValue = br.ReadBD(); break;
                    case 4: cv.StringValue = ReadTuPayload(br); break;
                    default: Fail(br, "childval[" + i + "].dt=" + cv.DataType + " 未定谱"); return;
                }
                cv.UnitType = (int)br.ReadBS();
                childs.Add(cv);
            }
            if (br.BitPosition != areaStart)
            { Fail(br, "标量段不闭合(游标 " + br.BitPosition + " vs 串区首 " + areaStart + ")"); return; }

            Cap.OwnHandle = ownHandle;
            Cap.UnknownBs = unknownBs;
            Cap.NumChilds = numChilds;
            Cap.IsXdicMissing = xdicMissing;
            Cap.HasDsData = dsData;
            Cap.Id = central[0].TrimEnd('\0');
            Cap.Code = central[1].TrimEnd('\0');
            Cap.NumReactors = numReactors;
            Cap.UnknownZ1 = unknownZ1;
            Cap.Format1 = "";
            Cap.Format2 = "";
            Cap.EvaluationOption = evalOption;
            Cap.FilingOption = filingOption;
            Cap.FieldState = fieldState;
            Cap.EvaluationStatus = evalStatus;
            Cap.EvaluationErrorCode = evalErrorCode;
            Cap.EvaluationErrorMsg = central[2].TrimEnd('\0');
            Cap.MainFmt = mainFmt;
            Cap.ValueDataType = mainDt;
            Cap.ValueLong = mainLong;
            Cap.ValueDouble = mainDouble;
            Cap.ValueString = mainString;
            Cap.MainUnitType = mainUnit;
            Cap.ValueString301 = central[5].TrimEnd('\0');
            Cap.ValueStringLength98 = vsl98;
            Cap.MainFormatString300 = central[3].TrimEnd('\0');
            Cap.MainValueString302 = central[4].TrimEnd('\0');
            Cap.NumChildValues = numChildValues;
            Cap.ChildValues.Clear();
            Cap.ChildValues.AddRange(childs);
            Cap.ChildFormatString300 = central[7].TrimEnd('\0');
            Cap.ChildValueString302 = central[8].TrimEnd('\0');
            Cap.CentralStrings.Clear();
            Cap.CentralStrings.AddRange(central);
            Cap.StringAreaStartBit = areaStart;
            Cap.StringAreaEndBit = areaEnd;
            Cap.WalkEndBit = br.BitPosition;
            Cap.FailReason = "";
            Cap.FailBit = -1;
            Cap.Captured = true;
        }
        catch (Exception ex)
        {
            Fail(br, ex.GetType().Name);
        }
    }

    private static string ReadTuPayload(BitReader br)
    {
        int n = (int)br.ReadBS();
        var bytes = new byte[n];
        for (int i = 0; i < n; i++) bytes[i] = (byte)br.ReadRC();
        return System.Text.Encoding.Unicode.GetString(bytes).TrimEnd('\0');
    }

    private void Fail(BitReader br, string why)
    {
        Cap.FailReason = why;
        Cap.FailBit = br.BitPosition - Rec.CarryStartBit;
        Cap.WalkEndBit = -1;
    }
}
