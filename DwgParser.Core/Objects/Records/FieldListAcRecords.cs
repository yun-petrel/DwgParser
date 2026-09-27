namespace DwgParser.Core.Objects.Records;


internal sealed class FieldListAcR2000 : FieldListRecord
{
    public FieldListAcR2000(ulong handle, DwgObjectRecord rec, int objStart) : base(handle, rec, objStart) { }

    public override string Family => "R2000";
}

internal sealed class FieldListAcR2004 : FieldListRecord
{
    public FieldListAcR2004(ulong handle, DwgObjectRecord rec, int objStart) : base(handle, rec, objStart) { }

    public override string Family => "R2004";

    protected override bool HasXdicMissing => true;
}

internal sealed class FieldListAcR2007 : FieldListRecord
{
    public FieldListAcR2007(ulong handle, DwgObjectRecord rec, int objStart) : base(handle, rec, objStart) { }

    public override string Family => "R2007";

    protected override bool HasXdicMissing => true;

    protected override bool HasTrailingStrings => true;
}

internal sealed class FieldListAcR2010 : FieldListRecord
{
    public FieldListAcR2010(ulong handle, DwgObjectRecord rec, int objStart) : base(handle, rec, objStart) { }

    public override string Family => "R2010";

    protected override bool HasInlineRl => false;

    protected override bool HasXdicMissing => true;

    protected override bool HasTrailingStrings => true;
}

internal sealed class FieldListAcR2013 : FieldListRecord
{
    public FieldListAcR2013(ulong handle, DwgObjectRecord rec, int objStart) : base(handle, rec, objStart) { }

    public override string Family => "R2013";

    protected override bool HasInlineRl => false;

    protected override bool HasXdicMissing => true;

    protected override bool HasDsData => true;

    protected override bool HasTrailingStrings => true;
}

internal sealed class FieldListAcR2018 : FieldListRecord
{
    public FieldListAcR2018(ulong handle, DwgObjectRecord rec, int objStart) : base(handle, rec, objStart) { }

    public override string Family => "R2018";

    protected override bool HasInlineRl => false;

    protected override bool HasXdicMissing => true;

    protected override bool HasDsData => true;

    protected override bool HasTrailingStrings => true;
}
