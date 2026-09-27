namespace DwgParser.Core.Objects.Records;


internal sealed class RasterVariablesAcR2000 : RasterVariablesRecord
{
    public RasterVariablesAcR2000(ulong handle, DwgObjectRecord rec, int objStart)
        : base(handle, rec, objStart) { }

    public override string Family => "R2000";
}

internal sealed class RasterVariablesAcR2004 : RasterVariablesRecord
{
    public RasterVariablesAcR2004(ulong handle, DwgObjectRecord rec, int objStart)
        : base(handle, rec, objStart) { }

    public override string Family => "R2004";

    protected override bool HasXdicMissing => true;
}

internal sealed class RasterVariablesAcR2007 : RasterVariablesRecord
{
    public RasterVariablesAcR2007(ulong handle, DwgObjectRecord rec, int objStart)
        : base(handle, rec, objStart) { }

    public override string Family => "R2007";

    protected override bool HasXdicMissing => true;

    protected override bool HasTrailingStrings => true;
}

internal sealed class RasterVariablesAcR2010 : RasterVariablesRecord
{
    public RasterVariablesAcR2010(ulong handle, DwgObjectRecord rec, int objStart)
        : base(handle, rec, objStart) { }

    public override string Family => "R2010";

    protected override bool HasInlineRl => false;

    protected override bool HasXdicMissing => true;

    protected override bool HasTrailingStrings => true;
}

internal sealed class RasterVariablesAcR2013 : RasterVariablesRecord
{
    public RasterVariablesAcR2013(ulong handle, DwgObjectRecord rec, int objStart)
        : base(handle, rec, objStart) { }

    public override string Family => "R2013";

    protected override bool HasInlineRl => false;

    protected override bool HasXdicMissing => true;

    protected override bool HasDsData => true;

    protected override bool HasTrailingStrings => true;
}

internal sealed class RasterVariablesAcR2018 : RasterVariablesRecord
{
    public RasterVariablesAcR2018(ulong handle, DwgObjectRecord rec, int objStart)
        : base(handle, rec, objStart) { }

    public override string Family => "R2018";

    protected override bool HasInlineRl => false;

    protected override bool HasXdicMissing => true;

    protected override bool HasDsData => true;

    protected override bool HasTrailingStrings => true;
}
