using DwgParser.Core.IO;

namespace DwgParser.Core.Objects;

internal sealed class MLeaderAcR2010Plus : MLeaderEntity
{
    public override void ReadBody(BitReader br, DwgObjectRecord? record)
    {
        Kind = DwgEntityType.MLeader;
        try
        {
            Points.Clear();

            ClassVersion = br.ReadBS();
            if (ClassVersion is < 1 or > 10) throw new System.IO.EndOfStreamException();

            ReadLeaders(br, r2010Plus: true);
            ReadContext(br, cmc: true);
            CtxTextTop = br.ReadBS();
            CtxTextBottom = br.ReadBS();
            ReadTopCommon(br, cmc: true);
            ReadCommonTail(br);

            AttachDir = br.ReadBS();
            AttachTop = br.ReadBS();
            AttachBottom = br.ReadBS();
            if (IsR2013Plus) IsTextExtended = br.ReadB();

            if (record != null && record.StringStreamStartBit > 0
                && br.BitPosition != record.StringStreamStartBit)
                throw new System.IO.EndOfStreamException();

            Captured = true;
        }
        catch (Exception ex) when (ex is System.IO.EndOfStreamException or IndexOutOfRangeException
                                   or InvalidOperationException or ArgumentOutOfRangeException)
        {
            FallbackToKindOnly();
        }
    }
}
