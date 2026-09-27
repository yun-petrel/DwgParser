namespace DwgParser.Core.Objects.Records;

public sealed class FieldCapture : RecordIdentity, IField
{
    public ulong OwnHandle;

    public int UnknownBs;

    public int NumChilds;

    public bool IsXdicMissing;

    public int NumReactors;

    public string Id = "";
    public string Code = "";
    public string Format1 = "";
    public string Format2 = "";

    public int EvaluationOption;
    public int FilingOption;
    public int FieldState;
    public int EvaluationStatus;
    public int EvaluationErrorCode;
    public string EvaluationErrorMsg = "";

    public int ValueDataType;
    public long ValueLong;
    public double ValueDouble;
    public string? ValueString;

    public string ValueString301 = "";
    public int ValueStringLength98;

    public int NumChildValues;

    public List<FieldChildValue> ChildValues { get; } = new();

    public bool HasDsData;

    public int MainFmt = -1;

    public int UnknownZ1;

    public int MainUnitType = -1;

    public string MainFormatString300 = "";

    public string MainValueString302 = "";

    public string ChildFormatString300 = "";

    public string ChildValueString302 = "";

    public List<string> CentralStrings { get; } = new();

    public long StringAreaStartBit = -1;
    public long StringAreaEndBit = -1;

    public bool Captured;

    public long WalkEndBit = -1;

    public string FailReason = "";

    public long FailBit = -1;

    string IField.Id => Id;
    string IField.Code => Code;
    int IField.NumChilds => NumChilds;
    bool IField.IsXdicMissing => IsXdicMissing;
    int IField.NumReactors => NumReactors;
    string IField.Format1 => Format1;
    string IField.Format2 => Format2;
    int IField.EvaluationOption => EvaluationOption;
    int IField.FilingOption => FilingOption;
    int IField.FieldState => FieldState;
    int IField.EvaluationStatus => EvaluationStatus;
    int IField.EvaluationErrorCode => EvaluationErrorCode;
    string IField.EvaluationErrorMsg => EvaluationErrorMsg;
    int IField.ValueDataType => ValueDataType;
    long IField.ValueLong => ValueLong;
    double IField.ValueDouble => ValueDouble;
    string? IField.ValueString => ValueString;
    string IField.ValueString301 => ValueString301;
    int IField.ValueStringLength98 => ValueStringLength98;
    int IField.NumChildValues => NumChildValues;
    IReadOnlyList<FieldChildValue> IField.ChildValues => ChildValues;
    bool IField.Captured => Captured;
}
