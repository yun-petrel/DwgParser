using DwgParser.Core.IO;

namespace DwgParser.Core.Objects;

public static partial class DwgObjectStreamWriter
{
    private static void WriteMText(BitWriter w, DwgObjectRecord rec, byte[] src, Target t)
    {
        var (ix, iy, iz) = rec.MTextInsPt;
        w.WriteBD(ix); w.WriteBD(iy); w.WriteBD(iz);
        var (ex, ey, ez) = rec.MTextExtrusion;
        w.WriteBD(ex); w.WriteBD(ey); w.WriteBD(ez);
        var (ax, ay, az) = rec.MTextXAxisDir;
        w.WriteBD(ax); w.WriteBD(ay); w.WriteBD(az);
        w.WriteBD(rec.MTextRectWidth);
        if (t.R2007Plus)
            w.WriteBD(rec.MTextRectHeight);
        w.WriteBD(rec.MTextTextHeight);
        w.WriteBS(rec.MTextAttachment);
        w.WriteBS(rec.MTextFlowDir);
        w.WriteBD(rec.MTextExtentsHeight);
        w.WriteBD(rec.MTextExtentsWidth);

        bool wroteInlineTv = false;
        long tailStart = rec.MTextTailStartBit;
        long tailEnd = rec.HandleStreamStartBit;
        if (rec.HasHandleStream && tailEnd > tailStart && tailEnd <= (long)rec.BodyEnd * 8)
        {
            int tailLen = (int)(tailEnd - tailStart);
            bool tailAtBgEnd = false;
            if (t.UnicodeStrings && !rec.SourceVersion.IsR2004Plus())
            {
                WriteMTextTail(w, rec, src, tailStart, tailLen, t);
                tailAtBgEnd = true;
            }
            else if (t.UnicodeStrings && rec.SourceVersion.IsR2004Plus())
            {
                int keepBits = MTextTailKeepBits(rec, src, tailStart, t);
                if (keepBits > 0 && keepBits <= tailLen)
                    CopyBits(w, src, tailStart, keepBits);
                else
                    CopyBits(w, src, tailStart, tailLen);
                w.WriteB(false);
                tailAtBgEnd = true;
            }
            else if (!t.UnicodeStrings && rec.SourceVersion.IsR2004Plus())
            {
                int keepBits = MTextTailKeepBits(rec, src, tailStart, t);
                string ssText = string.Empty;
                if (keepBits > 0 && keepBits < tailLen
                    && TryReadMTextFromStringStream(rec, src, out ssText))
                {
                    if (rec.Kind == DwgObjectRecordKind.Entity) WriteTvReinlineNul(w, ssText);
                    else w.WriteTV(ssText, false);
                    CopyBits(w, src, tailStart, keepBits);
                    if (t.Version.IsR2004Plus())
                        w.WriteB(false);
                    wroteInlineTv = true;
                    if (DebugTrace)
                        System.Console.Error.WriteLine(
                            $"MTEXT407 h={rec.Handle:X} {rec.SourceVersion}→{t.Version} " +
                            $"串流正文内联 TV 前置 keep={keepBits}/{tailLen} txLen={ssText.Length}");
                }
                else
                    CopyBits(w, src, tailStart, tailLen);
            }
            else
            {
                CopyBits(w, src, tailStart, tailLen);
            }

            if (tailAtBgEnd && NeedsMTextAnnotativeBlock(rec, t))
            {
                WriteMTextAnnotativeBlock(w, rec, src);
                if (DebugTrace)
                    System.Console.Error.WriteLine(
                        $"MTEXTANN h={rec.Handle:X} {rec.SourceVersion}→{t.Version} " +
                        $"字段级重写路补 annotative 块 @{w.BitPosition}");
            }
        }
        else
        {
            long end = rec.GeoStartBit + rec.GeoBitLen;
            long pos = rec.GeoStartBit;
            foreach (var f in rec.StringFields)
            {
                if (f.Start < pos || f.Start + f.BitLen > end) continue;
                CopyBits(w, src, pos, (int)(f.Start - pos));
                pos = f.Start + f.BitLen;
            }
            CopyBits(w, src, pos, (int)(end - pos));
        }

        string text = rec.StringFields.Count > 0 ? rec.StringFields[0].Value : string.Empty;
        if (t.UnicodeStrings)
        {
            WriteMTextStringStream(w, rec, text, t);
        }
        else
        {
            if (!wroteInlineTv)
                if (rec.Kind == DwgObjectRecordKind.Entity) WriteTvReinlineNul(w, text);
                else w.WriteTV(text, false);
        }
    }

    private static void WriteMTextStringStream(BitWriter w, DwgObjectRecord rec, string text, Target t)
    {
        long contentStart = w.BitPosition;
        if (CmcNamesInlineInSource(rec))
            WriteCmcNameLeading(w, rec);
        WriteCentralTu(w, text);
        int contentBits = (int)(w.BitPosition - contentStart);
        WriteStringStreamSize(w, (uint)contentBits);
        w.WriteB(true);
    }

    private static void WriteMTextTail(BitWriter w, DwgObjectRecord rec, byte[] src, long tailStart, int tailLen, Target t)
    {
        var br = new BitReader(src, 0);
        DwgObjectRecord.SeekTo(br, tailStart);
        br.ReadBS();
        br.ReadBD();
        br.ReadB();
        long afterUnk = br.BitPosition;
        if (afterUnk > tailStart && afterUnk <= tailStart + tailLen)
            CopyBits(w, src, tailStart, (int)(afterUnk - tailStart));
        else
            CopyBits(w, src, tailStart, tailLen);
        w.WriteB(true);
        w.WriteB(false);
    }

    private static int MTextTailKeepBits(DwgObjectRecord rec, byte[] src, long tailStart, Target t)
    {
        var br = new BitReader(src, 0);
        DwgObjectRecord.SeekTo(br, tailStart);
        br.ReadBS();
        br.ReadBD();
        br.ReadB();
        long keepEnd = br.BitPosition;
        if (t.Version.IsR2004Plus() && rec.SourceVersion.IsR2004Plus())
        {
            long bgFlag = br.ReadBL0();
            if ((bgFlag & 1) != 0)
            {
                br.ReadBL();
                DwgObjectReader.ReadEncColorIndex(br, out _);
                br.ReadBL();
            }
            keepEnd = br.BitPosition;
        }
        return (int)(keepEnd - tailStart);
    }

    private static bool TryReadMTextFromStringStream(DwgObjectRecord rec, byte[] src, out string text)
    {
        text = string.Empty;
        long hs = rec.HandleStreamStartBit;
        if (hs <= 0) return false;
        long footStart = hs - 17;
        if (footStart <= 0) return false;
        try
        {
            var br = new BitReader(src, 0) { UnicodeStrings = true };
            DwgObjectRecord.SeekTo(br, footStart);
            int dataSize = br.ReadRS();
            if (!br.ReadB()) return false;
            long contentStart = footStart - dataSize;
            if (contentStart <= 0 || dataSize <= 0) return false;
            var br2 = new BitReader(src, 0) { UnicodeStrings = true };
            DwgObjectRecord.SeekTo(br2, contentStart);
            text = br2.ReadTV();
            return true;
        }
        catch
        {
            return false;
        }
    }


    internal static bool CarriesMTextBgFillToAc1018(DwgObjectRecord rec, Target t) =>
        rec.LogicalType == TypeMText && rec.HasMTextPrefix && rec.MTextTailBitLen > 0
        && t.Version == DwgVersion.AC1018 && !rec.SourceVersion.IsR2004Plus();

    private static bool CarryMTextBgFillToAc1018(BitWriter w, DwgObjectRecord rec, byte[] src,
        long geoStart, int geoLen, Target t)
    {
        long end = geoStart + geoLen;
        if (!rec.HasHandleStream || end != rec.HandleStreamStartBit) return false;
        CopyBits(w, src, geoStart, geoLen);
        w.WriteB(true);
        w.WriteB(false);
        if (DebugTrace)
            System.Console.Error.WriteLine(
                $"MTEXTBG h={rec.Handle:X} t={rec.TypeCode} {rec.SourceVersion}→{t.Version} " +
                $"几何窗 {geoLen} 位原样 + 尾补 2 位（bg_fill BL0=0 · §182 实测位）@{end}");
        return true;
    }



    internal static bool DropsMTextRtlSegmentEed(byte[] src, DwgObjectRecord rec, Target t)
        => rec.LogicalType == TypeMText && rec.HasMTextPrefix
           && t.Version >= DwgVersion.AC1032
           && rec.SourceVersion >= DwgVersion.AC1015 && rec.SourceVersion < DwgVersion.AC1021
           && rec.EedStartBit > 0 && rec.EedBitLen > 2
           && IsMTextRtlSegmentEed(src, rec);

    private static bool IsMTextRtlSegmentEed(byte[] src, DwgObjectRecord rec)
    {
        if (src is null || src.Length == 0) return false;
        try
        {
            var br = new BitReader(src, 0) { UnicodeStrings = rec.SourceVersion >= DwgVersion.AC1021 };
            DwgObjectRecord.SeekTo(br, rec.EedStartBit);
            long eedEnd = rec.EedStartBit + rec.EedBitLen;
            int size = br.ReadBS();
            if (size <= 0) return false;
            br.ReadHandle();
            long payloadEnd = br.BitPosition + size * 8L;
            if (payloadEnd > eedEnd) return false;
            if (br.ReadRC() != 0) return false;
            int len = br.ReadRC();
            br.ReadRS();
            if (len <= 0 || 'M' != (char)br.ReadRC()) return false;
            const string magic = "MTEXTBEGIN";
            var sb = new System.Text.StringBuilder(magic.Length);
            sb.Append('M');
            for (int i = 1; i < magic.Length; i++)
            {
                if (br.BitPosition + 8 > payloadEnd) return false;
                sb.Append((char)br.ReadRC());
            }
            return sb.ToString() == magic;
        }
        catch
        {
            return false;
        }
    }


    internal static bool NeedsMTextAnnotative(DwgObjectRecord rec, Target t) =>
        rec.LogicalType == TypeMText && rec.HasMTextPrefix
        && t.Version >= DwgVersion.AC1032 && rec.SourceVersion >= DwgVersion.AC1021
        && rec.SourceVersion < DwgVersion.AC1032;

    internal static bool NeedsMTextAnnotativeBlock(DwgObjectRecord rec, Target t) =>
        rec.LogicalType == TypeMText && rec.HasMTextPrefix
        && t.Version >= DwgVersion.AC1032 && rec.SourceVersion < DwgVersion.AC1032
        && MTextTailKnown(rec);

    private static bool MTextTailKnown(DwgObjectRecord rec) =>
        rec.HasHandleStream && rec.HandleStreamStartBit > rec.MTextTailStartBit
        && rec.HandleStreamStartBit <= (long)rec.BodyEnd * 8;

    internal static bool DropsMTextAppidTail(DwgObjectRecord rec, Target t)
        => rec.LogicalType == TypeMText && rec.HasMTextPrefix && !t.R2007Plus
           && rec.SourceVersion >= DwgVersion.AC1032;

    internal static bool StripsMTextAnnotative(DwgObjectRecord rec, Target t) =>
        rec.LogicalType == TypeMText && rec.HasMTextPrefix
        && t.Version >= DwgVersion.AC1021 && t.Version < DwgVersion.AC1032
        && rec.SourceVersion >= DwgVersion.AC1032;

    private static long MTextBgFillEndBit(DwgObjectRecord rec, byte[] src, long geoStart)
    {
        var br = new BitReader(src, 0) { UnicodeStrings = true };
        DwgObjectRecord.SeekTo(br, geoStart);
        br.Read3BD(); br.Read3BD(); br.Read3BD();
        br.ReadBD();
        br.ReadBD();
        br.ReadBD();
        br.ReadBS(); br.ReadBS();
        br.ReadBD(); br.ReadBD();
        br.ReadBS(); br.ReadBD(); br.ReadB();
        long bgFlag = br.ReadBL0();
        if ((bgFlag & 1) != 0)
        {
            br.ReadBL();
            DwgObjectReader.ReadEncColorIndex(br, out _);
            br.ReadBL();
        }
        br.ReadB();
        return br.BitPosition;
    }

    private static void CarryMTextAnnotativeR2018(BitWriter w, DwgObjectRecord rec, byte[] src, long geoStart, int geoLen)
    {
        long bgEnd = MTextBgFillEndBit(rec, src, geoStart);
        CopyBits(w, src, geoStart, (int)(bgEnd - geoStart));
        WriteMTextAnnotativeBlock(w, rec, src);
        CopyBits(w, src, bgEnd, (int)(geoStart + geoLen - bgEnd));
    }

    private static void CarryMTextStripAnnotative(BitWriter w, DwgObjectRecord rec, byte[] src, long geoStart, int geoLen)
    {
        long bgEnd = MTextBgFillEndBit(rec, src, geoStart);
        CopyBits(w, src, geoStart, (int)(bgEnd - geoStart));
        long dataEnd = geoStart + geoLen;
        long ss = rec.StringStreamStartBit;
        if (ss > 0 && bgEnd <= ss && ss <= dataEnd)
        {
            CopyBits(w, src, ss, (int)(dataEnd - ss));
            return;
        }
        var br = new BitReader(src, 0) { UnicodeStrings = true };
        DwgObjectRecord.SeekTo(br, bgEnd);
        MTextAnnotativeScale.Read(br);
        CopyBits(w, src, br.BitPosition, (int)(dataEnd - br.BitPosition));
    }

    private static void WriteMTextAnnotativeBlock(BitWriter w, DwgObjectRecord rec, byte[] src)
    {
        var ann = MTextAnnotativeScale.FromMirror(rec);
        if (TryGetMTextColumnInfo(rec, src, out var col))
        {
            ann.ColumnType = col.Type;
            ann.NumColumnHeights = col.Values.Count;
            ann.ColumnWidth = col.Width;
            ann.Gutter = col.Gutter;
            ann.ColumnHeights.AddRange(col.Values);
        }
        ann.Write(w);
    }

    private readonly record struct MTextColumnInfo(short Type, double Width, double Gutter, List<double> Values);

    private static bool TryGetMTextColumnInfo(DwgObjectRecord rec, byte[] src, out MTextColumnInfo col)
    {
        col = default;
        if (rec.EedBitLen <= 0 || rec.EedStartBit < 0) return false;
        var br = new BitReader(src, 0);
        DwgObjectRecord.SeekTo(br, rec.EedStartBit);
        long eedEnd = rec.EedStartBit + rec.EedBitLen;
        while (br.BitPosition + 2 <= eedEnd)
        {
            int size = br.ReadBS();
            if (size == 0) break;
            if (br.BitPosition + size * 8 + 8 > eedEnd) return false;
            br.ReadHandle();
            var payload = new byte[size];
            for (int i = 0; i < size; i++) payload[i] = (byte)br.ReadRC();
            int tag = IndexOfUtf16(payload, "ACAD_MTEXT_COLUMN_INFO_BEGIN");
            if (tag < 0) continue;
            int p = tag + "ACAD_MTEXT_COLUMN_INFO_BEGIN".Length * 2 + 2;
            short type = 0;
            var reals = new List<double>();
            while (p + 1 < payload.Length)
            {
                int code = payload[p++];
                if (code == 0x46 && p + 2 <= payload.Length)
                {
                    if (type == 0) type = (short)(payload[p] | (payload[p + 1] << 8));
                    p += 2;
                }
                else if (code == 0x28 && p + 8 <= payload.Length)
                {
                    reals.Add(BitConverter.ToDouble(payload, p));
                    p += 8;
                }
                else break;
            }
            if (type is not (1 or 2) || reals.Count < 2) return false;
            col = new MTextColumnInfo(type, reals[0], reals[1], reals.Skip(2).ToList());
            return true;
        }
        return false;
    }

    private static int IndexOfUtf16(byte[] payload, string text)
    {
        var pat = new byte[text.Length * 2];
        for (int i = 0; i < text.Length; i++)
        {
            pat[i * 2] = (byte)text[i];
            pat[i * 2 + 1] = 0;
        }
        for (int i = 0; i + pat.Length <= payload.Length; i++)
        {
            bool hit = true;
            for (int j = 0; j < pat.Length; j++)
                if (payload[i + j] != pat[j]) { hit = false; break; }
            if (hit) return i;
        }
        return -1;
    }

}
