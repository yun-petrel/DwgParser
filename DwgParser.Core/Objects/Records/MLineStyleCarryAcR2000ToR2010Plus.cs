using DwgParser.Core.IO;
using static DwgParser.Core.Objects.DwgObjectStreamWriter;

namespace DwgParser.Core.Objects.Records;

internal sealed class MLineStyleCarryAcR2000ToR2010Plus
{
    public string Name => "MLINESTYLE:AC1015->R2010+";

    public bool IsCaptured(DwgObjectRecord rec)
        => rec.MLineStyle is { Captured: true, SemanticCaptured: true, Lines.Length: > 0 }
           && !string.IsNullOrEmpty(rec.MLineStyle!.Name);

    internal bool TryCarryFrameStart(BitWriter body, byte[] src, long from, long to,
        DwgObjectRecord rec, Target t, LtypeTable? ltypeTable, out BitWriter? handles)
    {
        handles = null;
        if (!t.R2010Plus || !t.UnicodeStrings) return false;
        if (rec.SourceVersion is not (DwgVersion.AC1015 or DwgVersion.AC1018)) return false;
        if (from != rec.CarryStartBit + 32) return false;
        var cap = rec.MLineStyle;
        if (cap is not { Captured: true, SemanticCaptured: true } || cap.Lines is not { Length: > 0 } ls)
            return false;
        if (string.IsNullOrEmpty(cap.Name)) return false;
        if (cap.FillFlag != 0) return false;
        foreach (var l in ls) if (l.ColorFlag != 0) return false;
        uint fillRgb = UpgradedCmcRgb(cap.FillIdx);
        if (fillRgb == 0) return false;
        var lineRgb = new uint[ls.Length];
        for (int i = 0; i < ls.Length; i++)
            if ((lineRgb[i] = UpgradedCmcRgb(ls[i].ColorIdx)) == 0) return false;
        bool lt2018 = t.Version >= DwgVersion.AC1032;
        if (lt2018 && ltypeTable == null) return false;

        long ownStart = rec.CarryStartBit + 32;
        var br = new BitReader(src);
        DwgObjectRecord.SeekTo(br, ownStart);
        br.ReadHandle();
        long eedStart = br.BitPosition;
        if (!DwgObjectReader.TrySkipEed(br)) return false;
        if (br.BitPosition - eedStart > 2) return false;
        if (br.ReadBL() is < 0 or > 8) return false;
        long headEnd = br.BitPosition;
        bool srcHasXdic = rec.SourceVersion >= DwgVersion.AC1018;
        bool xdicNull = false;
        if (srcHasXdic) { xdicNull = br.ReadB(); headEnd = br.BitPosition; }
        CopyBits(body, src, ownStart, (int)(headEnd - ownStart));
        if (!srcHasXdic)
        {
            xdicNull = SourceXdicRefIsNull(src, rec.HandleStreamStartBit, (long)rec.BodyEnd * 8);
            body.WriteB(xdicNull);
        }
        if (t.R2013Plus) body.WriteB(false);

        body.WriteBS((short)cap.Flag);
        WriteCmc(body, fillRgb);
        body.WriteBD(cap.StartAngle);
        body.WriteBD(cap.EndAngle);
        body.WriteRC((byte)ls.Length);
        for (int i = 0; i < ls.Length; i++)
        {
            body.WriteBD(ls[i].Offset);
            WriteCmc(body, lineRgb[i]);
            if (!lt2018) body.WriteBS((short)ls[i].LtIndex);
        }

        long contentStart = body.BitPosition;
        WriteCentralTu(body, cap.Name);
        WriteCentralTu(body, cap.Description);
        int contentBits = (int)(body.BitPosition - contentStart);
        WriteStringStreamSize(body, (uint)contentBits);
        body.WriteB(true);

        if (cap.NameBitLen > 0)
            rec.StringFields.Add(new DwgObjectRecord.StringField(
                cap.NameStartBit, cap.NameBitLen, cap.Name));

        rec.HandleStreamStartBit = rec.BodyStart * 8 + ReadSrcRl(src, rec);
        rec.HasHandleStream = true;

        if (lt2018)
        {
            if (!TryBuildR2018Handles(src, rec, ls, ltypeTable!, xdicNull, out var hw)) return false;
            handles = hw;
        }

        if (DebugTrace)
            System.Console.Error.WriteLine(
                $"MLS73R10 h={rec.Handle:X} AC1015->{t.Version} body={body.BitPosition}位 "
                + $"lines={ls.Length} name='{cap.Name}' xd={xdicNull} lt18={lt2018}");
        return true;
    }

    private static bool TryBuildR2018Handles(byte[] src, DwgObjectRecord rec,
        MLineStyleCapture.MLineStyleLine[] ls, LtypeTable table, bool xdicMissing, out BitWriter? hw)
    {
        hw = null;
        long hsStart = rec.HandleStreamStartBit;
        long hsEnd = (long)rec.BodyEnd * 8;
        if (hsStart <= 0 || hsStart >= hsEnd) return false;
        try
        {
            var dr = new BitReader(src);
            DwgObjectRecord.SeekTo(dr, rec.CarryStartBit + 32);
            dr.ReadHandle();
            if (!SkipEedWriter(dr)) return false;
            long numReactors = dr.ReadBL();
            if (numReactors is < 0 or > 8) return false;

            var hr = new BitReader(src);
            DwgObjectRecord.SeekTo(hr, hsStart);
            var w = new BitWriter(16);
            var h0 = hr.ReadHandle();
            w.WriteHandleRef(h0.Code, h0.Resolve(rec.Handle));
            for (long i = 0; i < numReactors; i++)
            {
                var h = hr.ReadHandle();
                w.WriteHandleRef(h.Code, h.Resolve(rec.Handle));
            }
            if (!xdicMissing)
            {
                var h = hr.ReadHandle();
                w.WriteHandleRef(h.Code, h.Resolve(rec.Handle));
            }
            foreach (var l in ls)
            {
                ulong lh = LtypeHandleForIndex(l.LtIndex, table);
                if (lh == 0) return false;
                w.WriteHandleRef(5, lh);
            }
            if (DebugTrace)
                System.Console.Error.WriteLine(
                    $"MLS73R10HS h={rec.Handle:X} nr={numReactors} xm={xdicMissing} refs={ls.Length}");
            hw = w;
            return true;
        }
        catch { hw = null; return false; }
    }

    private static long ReadSrcRl(byte[] src, DwgObjectRecord rec)
    {
        var br = new BitReader(src);
        DwgObjectRecord.SeekTo(br, rec.CarryStartBit);
        return br.ReadRL();
    }

    private static uint UpgradedCmcRgb(int idx)
        => idx switch
        {
            256 => 0xC0000000u,
            0 => 0xC1000000u,
            257 => 0xC8000000u,
            >= 1 and <= 255 => 0xC3000000u | (uint)idx,
            _ => 0u,
        };

    private static void WriteCmc(BitWriter body, uint packed)
    {
        body.WriteBS(0);
        body.WriteBL(unchecked((int)packed));
        body.WriteRC(0);
    }
}
