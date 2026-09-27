namespace DwgParser.Core.Objects.Records;


internal sealed class SortentsTableAcR2000 : SortentsTableRecord
{
    public SortentsTableAcR2000(ulong handle, DwgObjectRecord rec, int objStart)
        : base(handle, rec, objStart) { }

    public override string Family => "R2000";
}

internal sealed class SortentsTableAcR2004 : SortentsTableRecord
{
    public SortentsTableAcR2004(ulong handle, DwgObjectRecord rec, int objStart)
        : base(handle, rec, objStart) { }

    public override string Family => "R2004";

    protected override bool HasXdicMissing => true;
}

internal sealed class SortentsTableAcR2007 : SortentsTableRecord
{
    public SortentsTableAcR2007(ulong handle, DwgObjectRecord rec, int objStart)
        : base(handle, rec, objStart) { }

    public override string Family => "R2007";

    protected override bool HasXdicMissing => true;

    protected override bool HasTrailingStrings => true;
}

internal sealed class SortentsTableAcR2010 : SortentsTableRecord
{
    public SortentsTableAcR2010(ulong handle, DwgObjectRecord rec, int objStart)
        : base(handle, rec, objStart) { }

    public override string Family => "R2010";

    protected override bool HasInlineRl => false;

    protected override bool HasXdicMissing => true;

    protected override bool HasTrailingStrings => true;
}

internal sealed class SortentsTableAcR2013 : SortentsTableRecord
{
    public SortentsTableAcR2013(ulong handle, DwgObjectRecord rec, int objStart)
        : base(handle, rec, objStart) { }

    public override string Family => "R2013";

    protected override bool HasInlineRl => false;

    protected override bool HasXdicMissing => true;

    protected override bool HasDsData => true;

    protected override bool HasTrailingStrings => true;
}

internal sealed class SortentsTableAcR2018 : SortentsTableRecord
{
    public SortentsTableAcR2018(ulong handle, DwgObjectRecord rec, int objStart)
        : base(handle, rec, objStart) { }

    public override string Family => "R2018";

    protected override bool HasInlineRl => false;

    protected override bool HasXdicMissing => true;

    protected override bool HasDsData => true;

    protected override bool HasTrailingStrings => true;
}
