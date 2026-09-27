using DwgParser.Core.IO;

namespace DwgParser.Core.Objects;

public static partial class DwgObjectReader
{
    private static void DecodeDataLink(BitReader br, DwgVersion version,
        DwgObjectRecord? record, int objStart, bool r2010Plus)
    {
        if (record == null) return;
        var cap = record.DataLink ??= new Records.DataLinkCapture();

        if (version >= DwgVersion.AC1021)
        {
            DecodeDataLinkCentral(br, version, record, objStart, cap);
            DwgObjectRecord.SeekTo(br, record.CarryStartBit);
            return;
        }
        if (r2010Plus || version < DwgVersion.AC1015)
        {
            FailDataLink(br, cap, record, "未走查(低于受支持档)");
            return;
        }

        uint bitsize = br.ReadRL();
        long hs = (long)objStart * 8 + bitsize;
        if (hs <= br.BitPosition || hs > (long)record.BodyEnd * 8)
        { FailDataLink(br, cap, record, "帧界(RL vs BodyEnd)"); return; }

        long ownStart = br.BitPosition;
        br.ReadHandle();
        long eedStart = br.BitPosition;
        if (!TrySkipEed(br)) { FailDataLink(br, cap, record, "EED"); return; }
        long eedLen = br.BitPosition - eedStart;
        if (br.ReadBL() < 0) { FailDataLink(br, cap, record, "num_reactors<0"); return; }
        if (version.IsR2004Plus()) br.ReadB();

        var fields = new List<DwgObjectRecord.StringField>();
        string da, desc, tip, conn, upd;
        if (!CaptureDataLinkT(br, fields, out da)) { FailDataLink(br, cap, record, "T data_adapter 越界"); return; }
        if (!CaptureDataLinkT(br, fields, out desc)) { FailDataLink(br, cap, record, "T description 越界"); return; }
        if (!CaptureDataLinkT(br, fields, out tip)) { FailDataLink(br, cap, record, "T tooltip 越界"); return; }
        if (!CaptureDataLinkT(br, fields, out conn)) { FailDataLink(br, cap, record, "T connection_string 越界"); return; }
        cap.DataAdapter = da; cap.Description = desc; cap.Tooltip = tip;
        cap.ConnectionString = conn;

        cap.Option = br.ReadBL();
        cap.UpdateOption = br.ReadBL();
        cap.Bl92 = br.ReadBL();
        for (int i = 0; i < 7; i++) cap.Timestamp[i] = br.ReadBS();
        cap.PathOption = br.ReadBS();
        cap.Bl93 = br.ReadBL();
        if (!CaptureDataLinkT(br, fields, out upd)) { FailDataLink(br, cap, record, "T update_status 越界"); return; }
        cap.UpdateStatus = upd;
        cap.NumCustomdata = br.ReadBL();

        cap.HsBit = hs;
        cap.EedStartBit = eedStart;
        cap.EedBitLen = (int)eedLen;
        cap.StringCount = fields.Count;

        if (br.BitPosition != hs || ownStart > hs)
        {
            FailDataLink(br, cap, record,
                $"不闭合(游标 {br.BitPosition} vs hs {hs})");
            return;
        }

        record.EedStartBit = eedStart;
        record.EedBitLen = (int)eedLen;
        foreach (var f in fields) record.StringFields.Add(f);
        cap.Captured = true;
        cap.ValuesCommitted = true;
        cap.FailReason = "";
        cap.FailBit = -1;
    }

    private static bool CaptureDataLinkT(BitReader br, List<DwgObjectRecord.StringField> fields, out string value)
    {
        long start = br.BitPosition;
        value = br.ReadTV();
        fields.Add(new DwgObjectRecord.StringField(start, (int)(br.BitPosition - start), value));
        return true;
    }

    private static void FailDataLink(BitReader br, Records.DataLinkCapture cap,
        DwgObjectRecord record, string why)
    {
        cap.FailReason = why;
        cap.FailBit = br.BitPosition - record.CarryStartBit;
    }
}
