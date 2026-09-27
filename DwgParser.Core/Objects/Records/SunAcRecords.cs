namespace DwgParser.Core.Objects.Records;


internal sealed class SunAcR2000 : SunRecord
{
    public SunAcR2000(ulong handle, DwgObjectRecord rec, int objStart) : base(handle, rec, objStart) { }
    public override string Family => "R2000";
    protected override bool HasXdicMissing => false;
    protected override bool HasShortColor => true;
}

internal sealed class SunAcR2004 : SunRecord
{
    public SunAcR2004(ulong handle, DwgObjectRecord rec, int objStart) : base(handle, rec, objStart) { }

    public override string Family => "R2004";
}

internal sealed class SunAcR2007 : SunRecord
{
    public SunAcR2007(ulong handle, DwgObjectRecord rec, int objStart) : base(handle, rec, objStart) { }

    public override string Family => "R2007";

    protected override bool HasTrailingStrings => true;
}

internal sealed class SunAcR2010 : SunRecord
{
    public SunAcR2010(ulong handle, DwgObjectRecord rec, int objStart) : base(handle, rec, objStart) { }

    public override string Family => "R2010";

    protected override bool HasInlineRl => false;

    protected override bool HasTrailingStrings => true;
}

internal sealed class SunAcR2013 : SunRecord
{
    public SunAcR2013(ulong handle, DwgObjectRecord rec, int objStart) : base(handle, rec, objStart) { }

    public override string Family => "R2013";

    protected override bool HasInlineRl => false;

    protected override bool HasDsData => true;

    protected override bool HasTrailingStrings => true;
}

internal sealed class SunAcR2018 : SunRecord
{
    public SunAcR2018(ulong handle, DwgObjectRecord rec, int objStart) : base(handle, rec, objStart) { }

    public override string Family => "R2018";

    protected override bool HasInlineRl => false;

    protected override bool HasDsData => true;

    protected override bool HasTrailingStrings => true;
}
