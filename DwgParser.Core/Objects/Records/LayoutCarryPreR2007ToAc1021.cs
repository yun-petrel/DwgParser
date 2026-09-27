using DwgParser.Core;
using DwgParser.Core.IO;
using static DwgParser.Core.Objects.DwgObjectStreamWriter;

namespace DwgParser.Core.Objects.Records;

internal sealed class LayoutCarryPreR2007ToAc1021
{
    public string Name => "LAYOUT:AC1015->AC1021";

    public bool IsCaptured(DwgObjectRecord rec)
        => rec.Layout is { Captured: true };

    internal bool TryCarryFrameStart(BitWriter body, byte[] src, long from, long to,
        DwgObjectRecord rec, Target t)
    {
        if (!t.UnicodeStrings) return false;
        if (t.Version != DwgVersion.AC1021 && !t.R2010Plus) return false;
        if (rec.SourceVersion != DwgVersion.AC1015) return false;
        var cap = rec.Layout;
        if (cap is not { Captured: true }) return false;
        bool carriesRl = !t.R2010Plus;
        long expectFrom = carriesRl ? rec.CarryStartBit : rec.CarryStartBit + 32;
        if (from != expectFrom) return false;
        var rlBr = new BitReader(src);
        DwgObjectRecord.SeekTo(rlBr, rec.CarryStartBit);
        long dataEnd = (long)rec.BodyStart * 8 + rlBr.ReadRL();
        if (to != dataEnd || dataEnd <= from || dataEnd > (long)rec.BodyEnd * 8) return false;

        long s0 = cap.StringStarts[0], e0 = cap.StringEnds[0];
        long s1 = cap.StringStarts[1], e1 = cap.StringEnds[1];
        long s2 = cap.StringStarts[2], e2 = cap.StringEnds[2];
        long s3 = cap.StringStarts[3], e3 = cap.StringEnds[3];
        long pv0 = cap.PlotViewStartBit, pv1 = cap.PlotViewEndBit;
        if (!(s0 >= 0 && s0 < e0 && e0 == s1 && s1 < e1 && e1 < s2 && s2 < e2
            && e2 < pv0 && pv0 < pv1 && pv1 < s3 && s3 < e3
            && e3 <= dataEnd)) return false;

        long ownStart = rec.CarryStartBit + 32;
        var br = new BitReader(src);
        DwgObjectRecord.SeekTo(br, ownStart);
        br.ReadHandle();
        long eedStart = br.BitPosition;
        if (!DwgObjectReader.TrySkipEed(br)) return false;
        if (br.ReadBL() is < 0 or > 8) return false;
        if (br.BitPosition != s0) return false;
        long headEnd = br.BitPosition;
        if (carriesRl) body.WriteRL(0);
        CopyBits(body, src, ownStart, (int)(headEnd - ownStart));
        body.WriteB(SourceXdicRefIsNull(src, dataEnd, (long)rec.BodyEnd * 8));
        if (t.R2013Plus) body.WriteB(false);

        CopyBits(body, src, e1, (int)(s2 - e1));
        CopyBits(body, src, e2, (int)(pv0 - e2));
        CopyBits(body, src, pv1, 2);
        body.WriteBD(1.0);
        CopyBits(body, src, pv1 + 4, (int)(s3 - pv1 - 4));
        body.WriteBS(0);
        body.WriteBS(2);
        body.WriteBS(300);
        CopyBits(body, src, e3, (int)(dataEnd - e3));
        body.WriteBL(0);

        long contentStart = body.BitPosition;
        WriteCentralTu(body, cap.PrinterCfgFile);
        WriteCentralTu(body, cap.PaperSize);
        WriteCentralTu(body, cap.CanonicalMediaName);
        WriteCentralTu(body, cap.Stylesheet);
        WriteCentralTu(body, cap.LayoutName);
        int contentBits = (int)(body.BitPosition - contentStart);
        WriteStringStreamSize(body, (uint)contentBits);
        body.WriteB(true);

        int dataBits = carriesRl ? (int)body.BitPosition - 32 : (int)body.BitPosition;
        if (carriesRl) body.PatchRL(0, (uint)(10 + 32 + dataBits));
        rec.StringFields.Add(new DwgObjectRecord.StringField(
            contentStart, contentBits, cap.LayoutName ?? string.Empty));
        if (System.Environment.GetEnvironmentVariable("DWGDBG") == "1")
            System.Console.Error.WriteLine(
                $"LAY82 h={rec.Handle:X} AC1015->{t.Version} RL={(carriesRl ? 10 + 32 + dataBits : 0)} 数据区{dataBits}位 "
                + $"TU流{contentBits}位 name='{cap.LayoutName}' pv={pv1 - pv0}位");
        return true;
    }
}
