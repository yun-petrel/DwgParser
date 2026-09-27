using DwgParser.Core.IO;

namespace DwgParser.Core.Objects.Records;

internal abstract class DictVarCentralRecord : DictVarRecord
{
    protected DictVarCentralRecord(ulong handle, DwgObjectRecord rec, int objStart)
        : base(handle, rec, objStart) { }

    internal override void Read(BitReader br)
    {
        if (!TryBeginFrame(br, out long strAreaStart, out long dataEnd, out BitReader? strBr))
            return;

        br.ReadBL();
        if (HasXdicMissing) br.ReadB();
        if (HasDsData) br.ReadB();
        int schema = br.ReadRC();

        long expected = strAreaStart > 0 ? strAreaStart : dataEnd;
        if (br.BitPosition != expected) return;
        if (strBr is null) return;
        string strvalue = strBr.ReadTV();

        Cap.StrValue = strvalue;
        Cap.Schema = schema;
        Cap.Captured = true;
        StrValue = strvalue;
        Schema = schema;
    }

    protected virtual bool TryBeginFrame(BitReader br, out long strAreaStart, out long dataEnd,
        out BitReader? strBr)
        => CentralFrame.TryRlFrame(Rec, ObjStart, br, out strAreaStart, out dataEnd, out strBr);

    protected virtual bool HasDsData => false;
}

internal sealed class DictVarAcR2007 : DictVarCentralRecord
{
    public override string Family => "R2007";

    public DictVarAcR2007(ulong handle, DwgObjectRecord rec, int objStart)
        : base(handle, rec, objStart) { }
}

internal abstract class DictVarAcR2010Plus : DictVarCentralRecord
{
    protected DictVarAcR2010Plus(ulong handle, DwgObjectRecord rec, int objStart)
        : base(handle, rec, objStart) { }

    protected override bool TryBeginFrame(BitReader br, out long strAreaStart, out long dataEnd,
        out BitReader? strBr)
        => CentralFrame.TryBotFrame(Rec, br, out strAreaStart, out dataEnd, out strBr);

    protected override bool HasDsData => true;
}

internal sealed class DictVarAcR2010 : DictVarAcR2010Plus
{
    public override string Family => "R2010";

    public DictVarAcR2010(ulong handle, DwgObjectRecord rec, int objStart)
        : base(handle, rec, objStart) { }

    protected override bool HasDsData => false;
}

internal sealed class DictVarAcR2013 : DictVarAcR2010Plus
{
    public override string Family => "R2013";

    public DictVarAcR2013(ulong handle, DwgObjectRecord rec, int objStart)
        : base(handle, rec, objStart) { }
}

internal sealed class DictVarAcR2018 : DictVarAcR2010Plus
{
    public override string Family => "R2018";

    public DictVarAcR2018(ulong handle, DwgObjectRecord rec, int objStart)
        : base(handle, rec, objStart) { }
}
