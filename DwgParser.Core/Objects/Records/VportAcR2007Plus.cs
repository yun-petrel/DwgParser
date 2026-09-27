using DwgParser.Core.IO;

namespace DwgParser.Core.Objects.Records;

internal abstract class VportCentralRecord : VportRecord
{
    protected VportCentralRecord(ulong handle, DwgVersion sourceVersion,
        DwgObjectRecord record, int objStart)
        : base(handle, sourceVersion, record, objStart) { }

    protected override bool TryBeginFrame(BitReader br, FrameHead head)
    {
        DwgObjectRecord.SeekTo(br, Rec.CarryStartBit);
        if (!CentralStrings.TryDataBits(Rec, ObjStart, br, out uint bitsize, out head.DataEnd))
            return false;
        if (!CentralStrings.TryMeasure(Rec, br.UnderlyingData(), ObjStart, bitsize,
                out head.Strings, out head.NumericEnd, out head.StringsEnd))
            return false;
        head.OwnStart = br.BitPosition;
        br.ReadHandle();
        if (!DwgObjectReader.TrySkipEed(br)) return false;
        return br.BitPosition < head.NumericEnd;
    }

    protected override bool NameInline => false;

    protected override bool XrefTriple => false;

    protected override bool HasLightsBlock => true;

    protected override bool HasGridTail => true;

    protected override bool ValuesOnly => true;
}

internal sealed class VportAcR2007 : VportCentralRecord
{
    public override string Family => "R2007";

    public VportAcR2007(ulong handle, DwgVersion sourceVersion,
        DwgObjectRecord record, int objStart)
        : base(handle, sourceVersion, record, objStart) { }
}

internal sealed class VportAcR2010 : VportCentralRecord
{
    public override string Family => "R2010";

    public VportAcR2010(ulong handle, DwgVersion sourceVersion,
        DwgObjectRecord record, int objStart)
        : base(handle, sourceVersion, record, objStart) { }
}

internal sealed class VportAcR2013 : VportCentralRecord
{
    public override string Family => "R2013";

    public VportAcR2013(ulong handle, DwgVersion sourceVersion,
        DwgObjectRecord record, int objStart)
        : base(handle, sourceVersion, record, objStart) { }

    protected override bool HasDsData => true;
}

internal sealed class VportAcR2018 : VportCentralRecord
{
    public override string Family => "R2018";

    public VportAcR2018(ulong handle, DwgVersion sourceVersion,
        DwgObjectRecord record, int objStart)
        : base(handle, sourceVersion, record, objStart) { }

    protected override bool HasDsData => true;
}
