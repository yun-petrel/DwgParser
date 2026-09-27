namespace DwgParser.Core.Objects.Records;


internal sealed class TableGeometryAcR2004 : TableGeometryRecord
{
    public TableGeometryAcR2004(ulong handle, DwgObjectRecord rec, int objStart)
        : base(handle, rec, objStart) { }

    public override string Family => "R2004";
}

internal sealed class TableGeometryAcR2007 : TableGeometryRecord
{
    public TableGeometryAcR2007(ulong handle, DwgObjectRecord rec, int objStart)
        : base(handle, rec, objStart) { }

    public override string Family => "R2007";

    protected override bool HasTrailingStrings => true;
}

internal sealed class TableGeometryAcR2010 : TableGeometryRecord
{
    public TableGeometryAcR2010(ulong handle, DwgObjectRecord rec, int objStart)
        : base(handle, rec, objStart) { }

    public override string Family => "R2010";

    protected override bool HasInlineRl => false;

    protected override bool HasTrailingStrings => true;
}

internal sealed class TableGeometryAcR2013 : TableGeometryRecord
{
    public TableGeometryAcR2013(ulong handle, DwgObjectRecord rec, int objStart)
        : base(handle, rec, objStart) { }

    public override string Family => "R2013";

    protected override bool HasInlineRl => false;

    protected override bool HasDsData => true;

    protected override bool HasTrailingStrings => true;
}

internal sealed class TableGeometryAcR2018 : TableGeometryRecord
{
    public TableGeometryAcR2018(ulong handle, DwgObjectRecord rec, int objStart)
        : base(handle, rec, objStart) { }

    public override string Family => "R2018";

    protected override bool HasInlineRl => false;

    protected override bool HasDsData => true;

    protected override bool HasTrailingStrings => true;
}
