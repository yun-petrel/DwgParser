using DwgParser.Core.IO;
using DwgParser.Core.Objects.Records;

namespace DwgParser.Core.Objects;

public static partial class DwgObjectReader
{
    private static void TryDecodeTableContentValue(BitReader br, ulong handle, DwgVersion version,
        DwgObjectRecord? record, int objStart)
    {
        if (record == null) return;
        TableContentValueRecord? lane = version switch
        {
            DwgVersion.AC1021 => new TableContentValueAcR2007(record, objStart, br),
            DwgVersion.AC1024 => new TableContentValueAcR2010(record, objStart, br),
            DwgVersion.AC1027 => new TableContentValueAcR2013(record, objStart, br),
            DwgVersion.AC1032 => new TableContentValueAcR2018(record, objStart, br),
            _ => null,
        };
        if (lane == null) return;

        long savedBit = br.BitPosition;
        try
        {
            lane.Read();
        }
        catch {  }
        br.SetPosition((int)(savedBit >> 3), (int)(savedBit & 7));
    }
}
