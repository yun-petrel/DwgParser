using DwgParser.Core.IO;
using DwgParser.Core.Objects.Records;

namespace DwgParser.Core.Objects;

public static partial class DwgObjectReader
{
    private const string TableContentUnitAnchor = "11111110111111111111111111111111";

    private static void DecodeTableContentStructure(BitReader br, DwgVersion version,
        DwgObjectRecord? record, int objStart, bool r2010Plus)
    {
        if (record == null || r2010Plus) return;
        if (version.IsR2007Plus()) return;
        if (version < DwgVersion.AC1015) return;

        var cap = record.TableContent ??= new TableContentCapture();
        void Fail(string why)
        {
            cap.FailReason = why;
            cap.FailBit = br.BitPosition - record.CarryStartBit;
        }

        long dataStart = br.BitPosition;
        uint rlBits;
        try { rlBits = br.ReadRL(); }
        catch { Fail("RL 读不出"); return; }
        if (rlBits == 0 || rlBits > 4_000_000) { Fail($"RL 非法({rlBits})"); return; }
        long dataEnd = dataStart + rlBits;
        byte[] src = br.UnderlyingData();
        if (dataEnd > (long)src.Length * 8) { Fail($"RL 越出对象流(声明末位 {dataEnd})"); return; }

        const int anchorLen = 32;
        var anchors = new List<long>();
        for (long p = dataStart; p + anchorLen <= dataEnd && anchors.Count < 4096; p++)
            if (BitMatch(src, p, TableContentUnitAnchor)) anchors.Add(p);

        if (anchors.Count < 4) { Fail($"等距锚不足(声明 {rlBits} 位内仅 {anchors.Count} 枚)"); return; }

        var groups = new List<(long First, int Count)>();
        long curFirst = anchors[0];
        int curCount = 1;
        long step = -1;
        for (int i = 1; i < anchors.Count; i++)
        {
            long d = anchors[i] - anchors[i - 1];
            if (step < 0 || d == step)
            {
                step = d;
                curCount++;
            }
            else
            {
                groups.Add((curFirst, curCount));
                curFirst = anchors[i];
                curCount = 1;
                step = -1;
            }
        }
        groups.Add((curFirst, curCount));

        long unitStep = groups[0].First >= 0 ? anchors[1] - anchors[0] : 0;
        if (unitStep <= 0) { Fail($"步长非正({unitStep})"); return; }
        foreach (var g in groups)
            if (g.Count < 2) { Fail($"有组只 1 枚(锚 {anchors.Count} / 组 {groups.Count})"); return; }

        cap.SourceDataBits = (int)rlBits;
        cap.UnitAnchorBits = anchorLen;
        cap.UnitStepBits = (int)unitStep;
        cap.UnitCount = anchors.Count;
        cap.CellGroupCount = groups.Count;
        cap.FirstUnitBit = anchors[0];
        cap.HeaderBits = (int)(anchors[0] - dataStart);
        cap.UnitStarts.Clear();
        cap.UnitStarts.AddRange(anchors);
        cap.FailReason = "";
        cap.FailBit = -1;
        cap.Captured = true;

        if (System.Environment.GetEnvironmentVariable("DWGDBG") == "1")
            Console.Error.WriteLine($"[TC531] h={record.Handle:X} RL={rlBits} anchors={anchors.Count} " +
                $"groups={groups.Count} step={unitStep} header={cap.HeaderBits}");
    }

    private static bool BitMatch(byte[] src, long bitPos, string pattern)
    {
        for (int k = 0; k < pattern.Length; k++)
        {
            long p = bitPos + k;
            int byteIndex = (int)(p >> 3);
            if (byteIndex < 0 || byteIndex >= src.Length) return false;
            int bit = (src[byteIndex] >> (7 - (int)(p & 7))) & 1;
            if (bit != pattern[k] - '0') return false;
        }
        return true;
    }
}
