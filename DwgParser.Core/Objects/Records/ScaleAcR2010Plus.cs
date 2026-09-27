using DwgParser.Core.IO;

namespace DwgParser.Core.Objects.Records;

internal sealed class ScaleAcR2007 : ScaleRecord
{
    public override string Family => "R2007";

    public ScaleAcR2007(ulong handle, DwgObjectRecord rec, int objStart)
        : base(handle, rec, objStart) { }
}

internal abstract class ScaleAcR2010Plus : ScaleRecord
{
    protected ScaleAcR2010Plus(ulong handle, DwgObjectRecord rec, int objStart)
        : base(handle, rec, objStart) { }

    protected override bool TryBeginFrame(BitReader br, out long strAreaStart, out long dataEnd,
        out BitReader? strBr)
        => CentralFrame.TryBotFrame(Rec, br, out strAreaStart, out dataEnd, out strBr);
}

internal sealed class ScaleAcR2010 : ScaleAcR2010Plus
{
    public override string Family => "R2010";

    public ScaleAcR2010(ulong handle, DwgObjectRecord rec, int objStart)
        : base(handle, rec, objStart) { }
}

internal sealed class ScaleAcR2013 : ScaleAcR2010Plus
{
    public override string Family => "R2013";

    public ScaleAcR2013(ulong handle, DwgObjectRecord rec, int objStart)
        : base(handle, rec, objStart) { }

    protected override bool HasDsData => true;
}

internal sealed class ScaleAcR2018 : ScaleAcR2010Plus
{
    public override string Family => "R2018";

    public ScaleAcR2018(ulong handle, DwgObjectRecord rec, int objStart)
        : base(handle, rec, objStart) { }

    protected override bool HasDsData => true;
}
