using DwgParser.Core.IO;
using DwgParser.Core.Objects.Records;

namespace DwgParser.Core.Objects;

public static partial class DwgObjectReader
{
    private const int DataLinkStringFloor = 5;

    private static void DecodeDataLinkCentral(BitReader br, DwgVersion version,
        DwgObjectRecord record, int objStart, Records.DataLinkCapture cap)
    {
        DwgObjectRecord.SeekTo(br, record.CarryStartBit);
        if (!CentralStrings.TryDataBits(record, objStart, br, out uint bitsize, out long dataEnd))
        { FailDataLink(br, cap, record, "集中流 bitsize 未定位"); return; }
        if (!CentralStrings.TryMeasure(record, br.UnderlyingData(), objStart, bitsize,
                out BitReader strings, out long areaStart, out long areaEnd))
        { FailDataLink(br, cap, record, "集中流串区量不出"); return; }

        br.ReadHandle();
        long eedStart = br.BitPosition;
        if (!TrySkipEed(br)) { FailDataLink(br, cap, record, "EED"); return; }
        long eedLen = br.BitPosition - eedStart;
        if (br.ReadBL() < 0) { FailDataLink(br, cap, record, "num_reactors<0"); return; }
        br.ReadB();
        if (version >= DwgVersion.AC1027) br.ReadB();

        cap.Option = br.ReadBL();
        cap.UpdateOption = br.ReadBL();
        cap.Bl92 = br.ReadBL();
        for (int i = 0; i < 7; i++) cap.Timestamp[i] = br.ReadBS();
        cap.PathOption = br.ReadBS();
        cap.Bl93 = br.ReadBL();
        cap.NumCustomdata = br.ReadBL();
        if (br.BitPosition != areaStart)
        { FailDataLink(br, cap, record, $"标量段不闭合(游标 {br.BitPosition} vs 串区首 {areaStart})"); return; }

        var got = new List<string>();
        for (int i = 0; i < 24 && strings.BitPosition < areaEnd; i++) got.Add(strings.ReadTV());
        if (strings.BitPosition != areaEnd || got.Count < DataLinkStringFloor)
        { FailDataLink(br, cap, record, $"串区不闭合(枚数 {got.Count}，游标 {strings.BitPosition} vs 区末 {areaEnd})"); return; }

        cap.DataAdapter = got[0];
        cap.Description = got[1];
        cap.Tooltip = got[2];
        cap.ConnectionString = got[3];
        cap.UpdateStatus = got[4];
        cap.HsBit = dataEnd;
        cap.StringAreaStartBit = areaStart;
        cap.StringAreaEndBit = areaEnd;
        cap.EedStartBit = eedStart;
        cap.EedBitLen = (int)eedLen;
        cap.StringCount = got.Count;
        cap.ValuesCommitted = true;
        cap.FailReason = "";
        cap.FailBit = -1;
    }
}
