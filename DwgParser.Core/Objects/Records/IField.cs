namespace DwgParser.Core.Objects.Records;

public interface IField : IRecord
{
    string Id { get; }

    string Code { get; }

    int NumChilds { get; }

    bool IsXdicMissing { get; }

    int NumReactors { get; }

    string Format1 { get; }

    string Format2 { get; }

    int EvaluationOption { get; }

    int FilingOption { get; }

    int FieldState { get; }

    int EvaluationStatus { get; }

    int EvaluationErrorCode { get; }

    string EvaluationErrorMsg { get; }

    int ValueDataType { get; }

    long ValueLong { get; }

    double ValueDouble { get; }

    string? ValueString { get; }

    string ValueString301 { get; }

    int ValueStringLength98 { get; }

    int NumChildValues { get; }

    IReadOnlyList<FieldChildValue> ChildValues { get; }

    bool Captured { get; }
}

public sealed class FieldChildValue
{
    public string Key { get; internal set; } = "";

    public int DataType { get; internal set; }

    public int FormatFlags { get; internal set; } = -1;

    public int UnitType { get; internal set; } = -1;

    public long LongValue { get; internal set; }

    public double DoubleValue { get; internal set; }

    public string? StringValue { get; internal set; }
}
