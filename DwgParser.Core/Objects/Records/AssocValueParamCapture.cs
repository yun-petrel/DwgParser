namespace DwgParser.Core.Objects.Records;

public sealed class AssocValueParamCapture
{
    public long ClassVersion;

    public string Name = "";

    public long UnitType;

    public IReadOnlyList<AssocEvalVarCapture> Vars = Array.Empty<AssocEvalVarCapture>();

    public ulong ControlledObjdep;
}

public sealed class AssocEvalVarCapture
{
    public int Code = -1;

    public AssocEvalVariantKind Kind;

    public double Real;

    public long Int32;

    public int Int16;

    public byte Int8;

    public string Text = "";

    public ulong ValueHandle;

    public ulong SlotHandle;
}

public enum AssocEvalVariantKind
{
    Unknown = 0,
    Null,
    Real,
    Int32,
    Int16,
    Int8,
    Text,
    Handle,
}
