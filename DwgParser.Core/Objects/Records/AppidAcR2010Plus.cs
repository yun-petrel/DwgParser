using DwgParser.Core.IO;

namespace DwgParser.Core.Objects.Records;

internal sealed class AppidAcR2007 : AppidRecord
{
    public override string Family => "R2007";

    public AppidAcR2007(ulong handle, DwgObjectRecord rec, int objStart)
        : base(handle, rec, objStart) { }
}

internal abstract class AppidAcR2010Plus : AppidRecord
{
    protected AppidAcR2010Plus(ulong handle, DwgObjectRecord rec, int objStart)
        : base(handle, rec, objStart) { }

    protected override bool TryBeginFrame(BitReader br, out long strAreaStart, out long dataEnd,
        out BitReader? strBr)
        => CentralFrame.TryBotFrame(Rec, br, out strAreaStart, out dataEnd, out strBr);
}

internal sealed class AppidAcR2010 : AppidAcR2010Plus
{
    public override string Family => "R2010";

    public AppidAcR2010(ulong handle, DwgObjectRecord rec, int objStart)
        : base(handle, rec, objStart) { }
}

internal sealed class AppidAcR2013 : AppidAcR2010Plus
{
    public override string Family => "R2013";

    public AppidAcR2013(ulong handle, DwgObjectRecord rec, int objStart)
        : base(handle, rec, objStart) { }

    protected override bool HasDsData => true;
}

internal sealed class AppidAcR2018 : AppidAcR2010Plus
{
    public override string Family => "R2018";

    public AppidAcR2018(ulong handle, DwgObjectRecord rec, int objStart)
        : base(handle, rec, objStart) { }

    protected override bool HasDsData => true;
}
