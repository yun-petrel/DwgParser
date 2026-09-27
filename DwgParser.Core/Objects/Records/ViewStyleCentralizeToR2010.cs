using DwgParser.Core.IO;
using static DwgParser.Core.Objects.DwgObjectStreamWriter;

namespace DwgParser.Core.Objects.Records;

internal sealed class ViewStyleCentralizeToR2010 : ViewStyleCarryPlan
{
    public override string Name => "VSTY:preR2007源→R2010+目标(三串集中化)";

    public override bool IsCaptured(DwgObjectRecord rec)
        => rec.HasHandleStream && rec.StringFields.Count > 0
           && rec.ViewStyle is not { TemplateCaptured: true };

    internal override bool TryCarry(BitWriter body, DwgObjectRecord rec, byte[] src,
        Target t, out BitWriter? handles)
    {
        handles = null;
        if (rec.LogicalType is not (510 or 511)) return false;
        long start = rec.CarryStartBit;
        if (rec.SourceVersion.IsR2010Plus()) return false;
        start += 32;
        long hs = rec.HandleStreamStartBit;
        long hsEnd = (long)rec.BodyEnd * 8;
        if (start <= 0 || hs <= start || hs >= hsEnd) return false;
        var fields = rec.StringFields;
        int expect = rec.LogicalType == 510 ? 4 : 3;
        if (fields.Count != expect) return false;
        if (!(t.R2010Plus || t.Version == DwgVersion.AC1021)) return false;
        try
        {
            var ordered = fields.OrderBy(f => f.Start).ToList();
            long prev = start;
            foreach (var f in ordered)
            {
                if (f.Start < prev || f.Start + f.BitLen > hs) return false;
                prev = f.Start + f.BitLen;
            }
            var inserts = new List<(long At, bool IsFlags)>();
            if (t.R2013Plus)
            {
                if (!TryHeadAnchors(src, start, ordered, out long dsAt, out long vfAt)) return false;
                inserts.Add((dsAt, false));
                if (t.Version == DwgVersion.AC1032) inserts.Add((vfAt, true));
                inserts.Sort((x, y) => x.At.CompareTo(y.At));
            }
            prev = start;
            int at = 0;
            foreach (var f in ordered)
            {
                while (at < inserts.Count && inserts[at].At <= f.Start)
                {
                    if (inserts[at].At < prev) return false;
                    CopyBits(body, src, prev, (int)(inserts[at].At - prev));
                    prev = inserts[at].At;
                    if (inserts[at].IsFlags) body.WriteBL(0); else body.WriteB(false);
                    at++;
                }
                CopyBits(body, src, prev, (int)(f.Start - prev));
                prev = f.Start + f.BitLen;
            }
            while (at < inserts.Count)
            {
                if (inserts[at].At < prev || inserts[at].At > hs) return false;
                CopyBits(body, src, prev, (int)(inserts[at].At - prev));
                prev = inserts[at].At;
                if (inserts[at].IsFlags) body.WriteBL(0); else body.WriteB(false);
                at++;
            }
            CopyBits(body, src, prev, (int)(hs - prev));
            long cs = body.BitPosition;
            for (int k = 0; k < ordered.Count; k++)
            {
                WriteCentralTu(body, ordered[k].Value ?? string.Empty);
                if (k == 0 && t.Version == DwgVersion.AC1032)
                    WriteCentralTu(body, ordered[0].Value ?? string.Empty);
            }
            int contentBits = (int)(body.BitPosition - cs);
            WriteStringStreamSize(body, (uint)contentBits);
            body.WriteB(true);
            rec.WroteCentralStringStream = true;

            var hw = new BitWriter(16);
            long dataEndBit = (long)rec.BodyEnd * 8;
            long meaningful = LastMeaningfulHandleEnd(src, hs, dataEndBit);
            if (meaningful <= hs || meaningful > dataEndBit) meaningful = dataEndBit;
            CopyBits(hw, src, hs, (int)(meaningful - hs));
            handles = hw;

            if (DebugTrace)
                System.Console.Error.WriteLine(
                    $"VSCENTR h={rec.Handle:X} t={rec.TypeCode} n={ordered.Count} " +
                    $"SS={contentBits}b {rec.SourceVersion}→{t.Version}");
            return true;
        }
        catch
        {
            handles = null;
            return false;
        }
    }

    private static bool TryHeadAnchors(byte[] src, long start,
        List<DwgObjectRecord.StringField> ordered, out long dsAt, out long vfAt)
    {
        dsAt = vfAt = -1;
        try
        {
            var br = new BitReader(src, 0);
            DwgObjectRecord.SeekTo(br, start);
            br.ReadHandle();
            if (!DwgObjectReader.TrySkipEed(br)) return false;
            br.ReadBL();
            br.ReadB();
            dsAt = br.BitPosition;
            br.ReadBS();
            if (br.BitPosition > ordered[0].Start) return false;
            DwgObjectRecord.SeekTo(br, ordered[0].Start + ordered[0].BitLen);
            br.ReadB();
            vfAt = br.BitPosition;
            return dsAt > start && vfAt > dsAt;
        }
        catch { return false; }
    }
}
