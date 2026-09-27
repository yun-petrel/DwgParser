using DwgParser.Core.IO;

namespace DwgParser.Core.Objects;

public static partial class DwgObjectStreamWriter
{
    private const int AttribLogicalType = 0x02;

    private const int TextLogicalType = 0x01;
    private const int MTextLogicalType = 0x2C;

    private const int CentralTuUnitCeiling = 4096;

    private readonly record struct StringRewrite(int FieldIndex, long Start, int BitLen, string Value);

    internal static bool TryCarryEditedAttribStrings(BitWriter body, byte[] src, long from, long to,
        DwgObjectRecord rec, Target t)
    {
        var edits = WriteSideEdits.AttributeEditsFor(rec.Handle);
        if (edits.Count == 0) return false;
        if (rec.LogicalType is not (TextLogicalType or AttribLogicalType or MTextLogicalType))
            return AttribBail(rec, "非 TEXT/ATTRIB/MTEXT");
        if ((rec.SourceVersion >= DwgVersion.AC1021) != t.UnicodeStrings)
            return AttribBail(rec, "跨 R2007 串形界 ⇒ 交 CarryWithStrings 按值重发");
        if (rec.Kind != DwgObjectRecordKind.Entity) return AttribBail(rec, "非实体记录");
        long len = to - from;
        if (from < 0 || len <= 0 || to > (long)src.Length * 8)
            return AttribBail(rec, $"窗不合 from={from} to={to}");

        var targets = new List<StringRewrite>(edits.Count);
        foreach (var e in edits)
        {
            if (e.FieldIndex < 0 || e.FieldIndex >= rec.StringFields.Count)
                return AttribBail(rec, $"序位 {e.FieldIndex} 越界（捕获 {rec.StringFields.Count} 枚）");
            if (targets.Exists(x => x.FieldIndex == e.FieldIndex))
                return AttribBail(rec, $"序位 {e.FieldIndex} 重复登记");
            var f = rec.StringFields[e.FieldIndex];
            targets.Add(new StringRewrite(e.FieldIndex, f.Start, f.BitLen, e.Value ?? string.Empty));
        }
        bool anyInline = targets.Exists(x => x.BitLen > 0);
        bool anyStream = targets.Exists(x => x.BitLen == 0);
        if (anyInline && anyStream) return AttribBail(rec, "内联与集中流两型混登");
        return anyInline
            ? CarryEditedInlineStrings(body, src, from, len, rec, targets)
            : CarryEditedStreamStrings(body, src, from, len, rec, t, targets);
    }

    private static bool AttribBail(DwgObjectRecord rec, string why)
    {
        if (DebugTrace)
            System.Console.Error.WriteLine($"ATTEDIT-NO h={rec.Handle:X} {why}");
        return false;
    }


    private static bool CarryEditedInlineStrings(BitWriter body, byte[] src, long start, long len,
        DwgObjectRecord rec, List<StringRewrite> targets)
    {
        foreach (var w in targets)
            if (w.BitLen <= 0 || w.Start < start || w.Start + w.BitLen > start + len)
                return AttribBail(rec, $"内联窗越出搬运区 [{start},{start + len}) @#{w.FieldIndex}");
        bool rlInFrame = PrefixHasRl(rec.SourceVersion);
        uint rlBits = rlInFrame ? ReadRlAt(src, start) : 0u;
        if (rlInFrame && (rlBits == 0 || body.BitPosition != 0))
            return AttribBail(rec, $"RL 形不合 rl={rlBits} bodyBits={body.BitPosition}");

        targets.Sort((a, b) => a.Start.CompareTo(b.Start));
        long beforeBits = body.BitPosition;
        long pos = start;
        foreach (var w in targets)
        {
            CopyBits(body, src, pos, (int)(w.Start - pos));
            body.WriteTV(w.Value);
            pos = w.Start + w.BitLen;
        }
        CopyBits(body, src, pos, (int)(start + len - pos));

        int delta = (int)(body.BitPosition - beforeBits - len);
        if (rlInFrame) body.PatchRL(0, unchecked((uint)(rlBits + delta)));
        rec.DataRegionRewritten = true;
        foreach (var w in targets) WriteSideEdits.NoteFieldEmitted(rec.Handle, w.FieldIndex);
        if (DebugTrace)
            System.Console.Error.WriteLine(
                $"ATTEDIT h={rec.Handle:X} 内联 {targets.Count} 枚 窗位 {len}→{len + delta} " +
                $"{(rlInFrame ? $"RL {rlBits}→{rlBits + delta}" : "无内联 RL")}");
        return true;
    }


    private static bool CarryEditedStreamStrings(BitWriter body, byte[] src, long start, long len,
        DwgObjectRecord rec, Target t, List<StringRewrite> targets)
    {
        long winEnd = start + len;
        bool rlInFrame = PrefixHasRl(rec.SourceVersion);
        uint rlBits = rlInFrame ? ReadRlAt(src, start) : 0u;
        if (rlInFrame && (rlBits == 0 || body.BitPosition != 0))
            return AttribBail(rec, $"RL 形不合 rl={rlBits} win={len} bodyBits={body.BitPosition}");
        long dataEnd = winEnd;
        long ss = rec.StringStreamStartBit;
        if (ss < start || ss >= dataEnd) return AttribBail(rec, $"串流起点 ss={ss} 出区 [{start},{dataEnd})");

        var br = new BitReader(src, 0);
        DwgObjectRecord.SeekTo(br, dataEnd - 1);
        if (!br.ReadB()) return AttribBail(rec, "has_strings=0 ⇒ 无串流");
        DwgObjectRecord.SeekTo(br, dataEnd - 17);
        uint dataSize = br.ReadRS();
        int sizeFieldBits = 17;
        if ((dataSize & 0x8000) != 0)
        {
            DwgObjectRecord.SeekTo(br, dataEnd - 33);
            dataSize = (dataSize & 0x7FFF) | (uint)(br.ReadRS() << 15);
            sizeFieldBits = 33;
        }
        if (dataSize >= (uint)(dataEnd - start) || dataEnd - sizeFieldBits - dataSize != ss)
            return AttribBail(rec, $"甲不闭合 data_size={dataSize} 窗={dataEnd - start} ss={ss}");
        long streamEnd = dataEnd - sizeFieldBits;

        var spans = new List<(long Start, int Bits)>(rec.StringFields.Count + 1);
        long p = ss;
        while (p < streamEnd)
        {
            DwgObjectRecord.SeekTo(br, p);
            int units = br.ReadBS();
            long afterLen = br.BitPosition;
            if (units < 0 || units > CentralTuUnitCeiling
                || afterLen + (long)units * 16 > streamEnd)
                return AttribBail(rec, $"走查失步 @{p} units={units}");
            spans.Add((p, (int)(afterLen + units * 16 - p)));
            p = afterLen + (long)units * 16;
        }
        if (p != streamEnd || spans.Count != rec.StringFields.Count)
            return AttribBail(rec, $"乙不闭合 走查 {spans.Count} 枚 / 捕获 {rec.StringFields.Count} 枚，末位 {p} vs {streamEnd}");

        var rewrite = new string?[spans.Count];
        foreach (var w in targets)
        {
            if (w.FieldIndex >= rewrite.Length) return AttribBail(rec, $"序位 {w.FieldIndex} 出流");
            rewrite[w.FieldIndex] = w.Value;
        }

        long beforeBits = body.BitPosition;
        CopyBits(body, src, start, (int)(ss - start));
        long contentStart = body.BitPosition;
        for (int i = 0; i < spans.Count; i++)
        {
            if (rewrite[i] is string v) WriteCentralTu(body, v);
            else CopyBits(body, src, spans[i].Start, spans[i].Bits);
        }
        int contentBits = (int)(body.BitPosition - contentStart);
        WriteStringStreamSize(body, (uint)contentBits);
        body.WriteB(true);
        CopyBits(body, src, dataEnd, (int)(winEnd - dataEnd));

        int delta = (int)(body.BitPosition - beforeBits - len);
        if (rlInFrame) body.PatchRL(0, unchecked((uint)(rlBits + delta)));
        rec.DataRegionRewritten = true;
        foreach (var w in targets) WriteSideEdits.NoteFieldEmitted(rec.Handle, w.FieldIndex);
        if (DebugTrace)
            System.Console.Error.WriteLine(
                $"ATTEDIT h={rec.Handle:X} 集中流 {spans.Count} 枚（改写 {targets.Count} 枚）" +
                $" data_size {dataSize}→{contentBits} 帧窗 {len}→{len + delta}" +
                $" {(rlInFrame ? $"RL {rlBits}→{rlBits + delta}" : "无内联 RL")} t={t.Version}");
        return true;
    }
}
