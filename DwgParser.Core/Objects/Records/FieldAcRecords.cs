namespace DwgParser.Core.Objects.Records;


internal sealed class FieldAc1015 : FieldRecord
{
    public FieldAc1015(ulong handle, DwgObjectRecord rec, int objStart) : base(handle, rec, objStart) { }

    public override string Family => "R2000";
}

internal sealed class FieldAc1018 : FieldRecord
{
    public FieldAc1018(ulong handle, DwgObjectRecord rec, int objStart) : base(handle, rec, objStart) { }

    public override string Family => "R2004";

    protected override bool HasXdicMissing => true;
}


internal sealed class FieldAcR2007 : FieldRecord
{
    public FieldAcR2007(ulong handle, DwgObjectRecord rec, int objStart) : base(handle, rec, objStart) { }

    public override string Family => "R2007";

    protected override bool HasXdicMissing => true;

    protected override bool Central => true;
}

internal sealed class FieldAcR2010 : FieldRecord
{
    public FieldAcR2010(ulong handle, DwgObjectRecord rec, int objStart) : base(handle, rec, objStart) { }

    public override string Family => "R2010";

    protected override bool HasXdicMissing => true;

    protected override bool Central => true;
}

internal sealed class FieldAcR2013 : FieldRecord
{
    public FieldAcR2013(ulong handle, DwgObjectRecord rec, int objStart) : base(handle, rec, objStart) { }

    public override string Family => "R2013";

    protected override bool HasXdicMissing => true;

    protected override bool HasDsData => true;

    protected override bool Central => true;
}

internal sealed class FieldAcR2018 : FieldRecord
{
    public FieldAcR2018(ulong handle, DwgObjectRecord rec, int objStart) : base(handle, rec, objStart) { }

    public override string Family => "R2018";

    protected override bool HasXdicMissing => true;

    protected override bool HasDsData => true;

    protected override bool Central => true;
}
