namespace DwgParser.Core.Objects.Records;


internal sealed class UnderlayDefinitionAcR2000 : UnderlayDefinitionRecord
{
    public UnderlayDefinitionAcR2000(ulong handle, DwgObjectRecord rec, int objStart)
        : base(handle, rec, objStart) { }

    public override string Family => "R2000";
}

internal sealed class UnderlayDefinitionAcR2004 : UnderlayDefinitionRecord
{
    public UnderlayDefinitionAcR2004(ulong handle, DwgObjectRecord rec, int objStart)
        : base(handle, rec, objStart) { }

    public override string Family => "R2004";

    protected override bool HasXdicMissing => true;
}
