using DwgParser.Core.IO;
using DwgParser.Core.Objects.Records;

namespace DwgParser.Core.Objects;

public static partial class DwgObjectReader
{
    private const int DataTableMaxCols = 1024;

    private const int DataTableMaxRows = 200_000;

    private static void DecodeDataTableBranch(BitReader br, DwgVersion version, DwgObjectRecord? record)
    {
        try { TryDecodeDataTable(br, version, record); }
        catch {  }
        StampSilentReject(br, record, record?.DataTable);
    }

    private static void TryDecodeDataTable(BitReader shared, DwgVersion version, DwgObjectRecord? record)
    {
        if (record is null) return;
        if (version != DwgVersion.AC1015 && version != DwgVersion.AC1018) return;

        record.DataTable ??= new DataTableCapture();
        var cap = record.DataTable;
        try
        {
            var br = new BitReader(shared.UnderlyingData(), 0) { UnicodeStrings = false };
            DwgObjectRecord.SeekTo(br, record.CarryStartBit);
            long dataStart = br.BitPosition;
            uint rl = br.ReadRL();
            long dataEnd = (long)record.BodyStart * 8 + rl;
            cap.SourceDataBits = (int)rl;
            if (rl == 0 || dataEnd > (long)record.BodyEnd * 8)
            {
                FailDataTable(cap, br, dataStart, $"帧界(RL {rl} 越出帧体)");
                return;
            }

            br.ReadHandle();
            if (!TrySkipEed(br)) { FailDataTable(cap, br, dataStart, "EED"); return; }
            br.ReadBL();
            if (version >= DwgVersion.AC1018) br.ReadB();

            cap.Flags = (int)br.ReadBL();
            cap.NumCols = (int)br.ReadBL();
            cap.NumRows = (int)br.ReadBL();
            cap.TableName = br.ReadTV();
            if (cap.NumCols is < 1 or > DataTableMaxCols || cap.NumRows is < 0 or > DataTableMaxRows)
            {
                FailDataTable(cap, br, dataStart, $"计数非法({cap.NumCols}×{cap.NumRows})");
                return;
            }

            for (int c = 0; c < cap.NumCols; c++)
            {
                var col = new DataTableColumnCapture
                {
                    Type = (int)br.ReadBL(),
                    Text = br.ReadTV(),
                };
                cap.Columns.Add(col);
                if (c == cap.NumCols - 1) continue;
                col.ValuesInRegion = true;
                for (int r = 0; r < cap.NumRows; r++)
                {
                    if (col.Type == 2) col.Reals.Add(br.ReadBD());
                    else col.Integers.Add(br.ReadBL());
                }
            }

            if (br.BitPosition != dataEnd)
            {
                cap.Columns.Clear();
                FailDataTable(cap, br, dataStart, $"停点差 {br.BitPosition - dataEnd} 位");
                return;
            }

            cap.WalkEndBit = br.BitPosition;
            cap.FailReason = "";
            cap.Captured = true;
            WalkTailReferences(br, version, record, cap, dataEnd);
        }
        catch (Exception e)
        {
            cap.Captured = false;
            cap.Columns.Clear();
            if (string.IsNullOrEmpty(cap.FailReason))
                FailDataTable(cap, br: new BitReader(shared.UnderlyingData(), 0), record.CarryStartBit,
                    $"异常 {e.GetType().Name}");
        }
    }

    private static void WalkTailReferences(BitReader br, DwgVersion version,
        DwgObjectRecord record, DataTableCapture cap, long dataEnd)
    {
        long hi = (long)record.BodyEnd * 8;
        var pos = dataEnd;
        ulong prev = record.Handle;
        while (hi - pos >= 8)
        {
            int ctrl = ReadBitsAt(br, pos, 8);
            pos += 8;
            int code = ctrl >> 4, size = ctrl & 0x0F;
            if (size > 8 || code > 14 || hi - pos < size * 8)
            {
                cap.TailRefs.Clear();
                cap.TailFailReason = $"DTT尾@非法码 0x{ctrl:X2}（第 {cap.TailRefs.Count + 1} 枚）";
                return;
            }
            ulong v = 0;
            for (int i = 0; i < size; i++) { v = (v << 8) | (uint)ReadBitsAt(br, pos, 8); pos += 8; }
            ulong abs = code switch { 10 => prev + v, 12 => prev - v, _ => v };
            if (code != 0 || size != 0) prev = abs;
            cap.TailRefs.Add(abs);
        }
        int pad = (int)(hi - pos);
        if (pad < 0 || pad > 7)
        {
            cap.TailRefs.Clear();
            cap.TailFailReason = $"DTT尾@残位 {pad}";
            return;
        }
        cap.TailPadBits = pad;
        cap.TailFailReason = "";
        cap.TailCaptured = true;
    }

    private static int ReadBitsAt(BitReader br, long bitPos, int count)
    {
        byte[] src = br.UnderlyingData();
        int v = 0;
        for (int i = 0; i < count; i++)
        {
            long b = bitPos + i;
            v = (v << 1) | ((src[b >> 3] >> (7 - (int)(b & 7))) & 1);
        }
        return v;
    }

    private static void FailDataTable(DataTableCapture cap, BitReader br, long dataStart, string why)
    {
        cap.Captured = false;
        cap.FailReason = $"DTT@{why}";
        cap.FailBit = br.BitPosition - dataStart;
    }
}
