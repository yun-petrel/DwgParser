using DwgParser.Core.IO;

namespace DwgParser.Core.Objects;

internal sealed class MLeaderAcPreR2010 : MLeaderEntity
{
    public override void ReadBody(BitReader br, DwgObjectRecord? record)
    {
        Kind = DwgEntityType.MLeader;
        try
        {
            Points.Clear();

            bool cmc = record == null || record.SourceVersion >= DwgVersion.AC1018;
            if (!cmc)
                ShortCmcSink = record.MLeaderShortCmcBits ??= new List<long>();
            else
                ShortCmcSink = null;

            ReadLeaders(br, r2010Plus: false);
            ReadContext(br, cmc);
            ReadTopCommon(br, cmc);

            long numArrowheads = br.ReadBL();
            if (numArrowheads is < 0 or > 5000) throw new System.IO.EndOfStreamException();
            for (int i = 0; i < numArrowheads; i++) ArrowheadDefaults.Add(br.ReadB());
            ArrowheadCount = (int)numArrowheads;
            ReadCommonTail(br);

            Captured = true;
        }
        catch (Exception ex) when (ex is System.IO.EndOfStreamException or IndexOutOfRangeException
                                   or InvalidOperationException or ArgumentOutOfRangeException)
        {
            ShortCmcSink = null;
            if (record != null)
                record.MLeaderShortCmcBits = null;
            FallbackToKindOnly();
        }
    }
}
