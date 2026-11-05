using DwgParser.Core.IO;

namespace DwgParser.Core.Objects;

public static partial class DwgObjectStreamWriter
{
    private static (long Start, int Len) HatchGeometryWindow(DwgObjectRecord rec, Target t)
    {
        long start = rec.GeoStartBit;
        int len = rec.GeoBitLen;
        if (StripsDimensionClassVersion(rec, t))
            return (start + 8, len - 8);
        if (rec.LogicalType != TypeHatch) return (start, len);
        if (!rec.SourceVersion.IsR2004Plus() || !t.R2000) return (start, len);
        if (rec.HatchGradientEndBit > start && rec.HatchGradientEndBit < start + len)
        {
            int drop = (int)(rec.HatchGradientEndBit - start);
            return (rec.HatchGradientEndBit, len - drop);
        }
        return (start, len);
    }

    private static bool IsDimensionFamily(DwgObjectRecord rec) =>
        rec.LogicalType >= TypeDimOrdinate && rec.LogicalType <= TypeDimDiameter;

    internal static bool NeedsDimensionClassVersion(DwgObjectRecord rec, Target t) =>
        IsDimensionFamily(rec) && !rec.SourceVersion.IsR2010Plus() && t.R2010Plus;

    private static bool StripsDimensionClassVersion(DwgObjectRecord rec, Target t) =>
        IsDimensionFamily(rec) && rec.SourceVersion.IsR2010Plus() && !t.R2010Plus && rec.GeoBitLen > 8;

    private static bool IsAttributeFamily(DwgObjectRecord rec) =>
        rec.LogicalType == TypeAttrib || rec.LogicalType == TypeAttDef;



    internal static bool CarryAttributeTailGeometry(BitWriter body, DwgObjectRecord rec, byte[] src,
        long geoStart, int geoLen, Target t)
    {
        if (!AttributeTailCrossesVersion(rec, t)) return false;
        if ((rec.SourceVersion >= DwgVersion.AC1021) != t.UnicodeStrings) return false;

        bool srcLocked = SrcLocked(rec), srcMtext = SrcMtext(rec);
        bool tgtLocked = TgtLocked(t), tgtMtext = TgtMtext(t);
        long end = geoStart + geoLen;
        long a1 = rec.AttTailLockedBit, a2 = rec.AttTailKeepDuplicateBit;
        bool attdef = rec.LogicalType == TypeAttDef;
        if (a1 < 0 || a2 < 0 || !(geoStart <= a1 && a1 <= a2 && a2 <= end)) return false;
        long srcCur = a1 + (srcLocked ? 8 : 0) + (srcMtext ? 8 : 0);
        bool keepSrc = attdef && srcLocked;
        if (srcCur > a2 || (keepSrc && a2 + 8 > end)) return false;
        if (!(geoStart < a1 && a1 <= a2 && a2 < end)) return false;

        CopyBits(body, src, geoStart, (int)(a1 - geoStart));
        if (tgtLocked) body.WriteRC(AttTailLockedUp(rec));
        if (tgtMtext) body.WriteRC(AttTailMTextUp(rec));
        CopyBits(body, src, srcCur, (int)(a2 - srcCur));
        if (tgtLocked && attdef) body.WriteRC(AttTailKeepUp(rec));
        long tail = a2 + (keepSrc ? 8 : 0);
        CopyBits(body, src, tail, (int)(end - tail));

        if (DebugTrace)
            System.Console.Error.WriteLine(
                $"ATTTAIL h={rec.Handle:X} {(attdef ? "ATTDEF" : "ATTRIB")} {rec.SourceVersion}→{t.Version} " +
                $"locked {srcLocked}→{tgtLocked} mtext {srcMtext}→{tgtMtext} keep {keepSrc}→{tgtLocked && attdef}");
        return true;
    }

    private static void WriteHatchGradientIfUpgrade(BitWriter w, DwgObjectRecord rec, Target t)
    {
        if (rec.LogicalType != TypeHatch) return;
        if (rec.SourceVersion.IsR2004Plus() || t.R2000) return;
        for (int i = 0; i < 4; i++) w.WriteBB(2);
        for (int i = 0; i < 3; i++) w.WriteBB(2);
        if (!t.UnicodeStrings)
            w.WriteTV(string.Empty, false);
    }

    private static void CarryInsertGeometry(BitWriter w, DwgObjectRecord rec, byte[] src,
        long geoStart, int geoLen, Target t)
    {
        long geoEnd = geoStart + geoLen;
        long anchor = rec.InsertAfterAttribsBit;
        if (anchor < geoStart || anchor > geoEnd)
        {
            CopyBits(w, src, geoStart, geoLen);
            return;
        }
        CopyBits(w, src, geoStart, (int)(anchor - geoStart));
        if (!rec.SourceVersion.IsR2004Plus() && !t.R2000)
        {
            w.WriteBL(TargetAttribCount(rec));
            CopyBits(w, src, anchor, (int)(geoEnd - anchor));
        }
        else if (rec.SourceVersion.IsR2004Plus() && t.R2000)
        {
            long afterNumOwned = anchor + rec.InsertNumOwnedBitLen;
            if (afterNumOwned < geoEnd)
                CopyBits(w, src, afterNumOwned, (int)(geoEnd - afterNumOwned));
        }
        else
        {
            CopyBits(w, src, anchor, (int)(geoEnd - anchor));
        }
    }

    private static int TargetAttribCount(DwgObjectRecord rec)
        => rec.AttribRefs.Count > 0 ? rec.AttribRefs.Count : (rec.HasAttribPair ? 2 : 0);

    private static bool HasStringFieldInRange(DwgObjectRecord rec, long start, int len)
    {
        long end = start + len;
        foreach (var f in rec.StringFields)
            if (f.Start >= start && f.Start + f.BitLen <= end) return true;
        return false;
    }

    private static bool ReinlinesStreamAnchors(Target t) => t.Version == DwgVersion.AC1018;

    private static bool AllStringFieldsAreStreamAnchors(DwgObjectRecord rec)
    {
        if (rec.StringFields.Count == 0) return false;
        foreach (var f in rec.StringFields)
            if (f.BitLen != 0) return false;
        return true;
    }

    private static bool HasStreamAnchorInRange(DwgObjectRecord rec, long start, int len)
    {
        long end = start + len;
        foreach (var f in rec.StringFields)
            if (f.BitLen == 0 && f.Start >= start && f.Start <= end) return true;
        return false;
    }

    private static bool HasCmcNames(DwgObjectRecord rec)
        => rec.EncColor is { } e
           && (!string.IsNullOrEmpty(e.ColorName) || !string.IsNullOrEmpty(e.ColorBookName));

    private static bool CmcNamesInlineInSource(DwgObjectRecord rec)
        => rec.SourceVersion < DwgVersion.AC1021 && HasCmcNames(rec);

    private static bool CmcNamesNeedStream(DwgObjectRecord rec, Target t)
        => t.UnicodeStrings && CmcNamesInlineInSource(rec);

    private static void WriteCmcNameLeading(BitWriter w, DwgObjectRecord rec)
    {
        if (rec.EncColor is not { } e) return;
        if (!string.IsNullOrEmpty(e.ColorName)) WriteCentralTu(w, e.ColorName);
        if (!string.IsNullOrEmpty(e.ColorBookName)) WriteCentralTu(w, e.ColorBookName);
    }

    private static void CarryWithStrings(BitWriter w, DwgObjectRecord rec, byte[] src,
        long start, int len, Target t, string? leadingString = null,
        long insertOneAt = -1, bool insertOneValue = true, int insertCount = 1,
        IReadOnlyList<BitInsert>? extraInserts = null)
    {
        if (CarriesMLeaderUp(rec, t) && ConvertMLeaderUp(w, rec, src, t)) return;
        if (CarriesMLeaderTextExtUp(rec, t)
            && AppendMLeaderTextExtendedUp(w, rec, src, t, start, len)) return;
        if (CarriesMLeaderDown(rec, t)
            && ConvertMLeaderDown(w, rec, src, t, start, len)) return;
        if (CarriesMLeaderR2010Up(rec, t)
            && ConvertMLeaderR2010Up(w, rec, src, t, start, len)) return;

        bool streamAnchors = ReinlinesStreamAnchors(t);
        bool crossBoundary = rec.SourceVersion >= DwgVersion.AC1021 != t.UnicodeStrings
                             && (HasStringFieldInRange(rec, start, len)
                                 || (streamAnchors && HasStreamAnchorInRange(rec, start, len))
                                 || (t.UnicodeStrings && (leadingString != null
                                                          || CmcNamesNeedStream(rec, t))));
        if (DebugTrace)
            System.Console.Error.WriteLine(
                $"DBG carry h={rec.Handle:X} cross={crossBoundary} unicode={t.UnicodeStrings} " +
                $"srcV={rec.SourceVersion} strF={rec.StringFields.Count} len={len} start={start}");
        if (crossBoundary && t.UnicodeStrings)
        {
            CarryWithStringsR2007(w, rec, src, start, len, leadingString, insertOneAt, insertOneValue,
                insertCount, extraInserts);
            return;
        }
        if (!crossBoundary)
        {
            CopyBits(w, src, start, len);
            return;
        }
        long end = start + len;
        if (rec.Kind == DwgObjectRecordKind.Entity
            && !t.R2007Plus
            && rec.StringStreamStartBit > 0
            && rec.StringStreamStartBit < end && rec.StringStreamStartBit >= start)
        {
            bool allCentralizedAnchors = rec.StringFields.Count > 0;
            foreach (var sf in rec.StringFields)
                if (sf.BitLen != 0 || sf.Start < start || sf.Start > rec.StringStreamStartBit)
                { allCentralizedAnchors = false; break; }
            if (allCentralizedAnchors) end = rec.StringStreamStartBit;
        }
        long pos = start;
        bool ac1015PureAnchors = t.Version == DwgVersion.AC1015 && AllStringFieldsAreStreamAnchors(rec);
        for (int si = 0; si < rec.StringFields.Count; si++)
        {
            var f = rec.StringFields[si];
            if (f.BitLen == 0 && !(streamAnchors || ac1015PureAnchors)) continue;
            if (f.Start < pos || f.Start + f.BitLen > end) continue;
            CopyBits(w, src, pos, (int)(f.Start - pos));
            if (rec.Kind == DwgObjectRecordKind.Entity) WriteTvReinlineNul(w, f.Value);
            else w.WriteTV(f.Value, false);
            WriteSideEdits.NoteFieldEmitted(rec.Handle, si);
            pos = f.Start + f.BitLen;
        }
        CopyBits(w, src, pos, (int)(end - pos));
    }

    internal static void WriteTvReinlineNul(BitWriter w, string? value)
    {
        if (string.IsNullOrEmpty(value)) { w.WriteTV(value, false); return; }
        byte[] bytes = BitWriter.EncodeText(value);
        w.WriteBS(unchecked((short)(bytes.Length + 1)));
        foreach (var b in bytes) w.WriteRC(b);
        w.WriteRC(0);
    }

    internal static void WriteCentralTu(BitWriter w, string? value)
    {
        if (string.IsNullOrEmpty(value)) { w.WriteBS(0); return; }
        w.WriteBS(unchecked((short)value.Length));
        foreach (char c in value) w.WriteRS(c);
    }

    private static void CarryWithStringsR2007(BitWriter w, DwgObjectRecord rec, byte[] src,
        long start, int len, string? leadingString = null,
        long insertOneAt = -1, bool insertOneValue = true, int insertCount = 1,
        IReadOnlyList<BitInsert>? extraInserts = null)
    {
        long end = start + len;
        long pos = start;
        var strValues = new List<string>(rec.StringFields.Count);
        for (int si = 0; si < rec.StringFields.Count; si++)
        {
            var f = rec.StringFields[si];
            if (f.Start < pos || f.Start + f.BitLen > end) continue;
            CopyBitsInsertingOne(w, src, pos, f.Start, insertOneAt, insertOneValue, insertCount,
                extraInserts);
            strValues.Add(f.Value);
            WriteSideEdits.NoteFieldEmitted(rec.Handle, si);
            pos = f.Start + f.BitLen;
        }
        CopyBitsInsertingOne(w, src, pos, end, insertOneAt, insertOneValue, insertCount,
            extraInserts);
        long contentStart = w.BitPosition;
        if (CmcNamesInlineInSource(rec))
            WriteCmcNameLeading(w, rec);
        if (leadingString != null)
            WriteCentralTu(w, leadingString);
        foreach (var v in strValues)
            WriteCentralTu(w, v);
        int contentBits = (int)(w.BitPosition - contentStart);
        WriteStringStreamSize(w, (uint)contentBits);
        w.WriteB(true);
        rec.WroteCentralStringStream = true;
    }

    internal static bool TryCarryEedR2007Plus(BitWriter body, byte[] src, long eedStart, long eedEnd,
        DwgObjectRecord rec)
    {
        try
        {
            var br = new BitReader(src);
            DwgObjectRecord.SeekTo(br, eedStart);
            while (br.BitPosition < eedEnd)
            {
                ushort size = (ushort)br.ReadBS();
                if (size == 0) { body.WriteBS(0); break; }
                var h = br.ReadHandle();
                long payloadEnd = br.BitPosition + size * 8;
                if (payloadEnd > eedEnd) return false;
                var tw = new BitWriter(16);
                bool ok = true;
                while (br.BitPosition < payloadEnd && ok)
                {
                    long subStart = br.BitPosition;
                    int code = br.ReadRC();
                    switch (code)
                    {
                        case 0:
                        {
                            if (br.BitPosition + 24 > payloadEnd) { ok = false; break; }
                            int lenc = br.ReadRC();
                            int cpLib = br.ReadRS_BE();
                            if (lenc < 0 || lenc > size || br.BitPosition + lenc * 8 > payloadEnd) { ok = false; break; }
                            var rawBytes = new byte[lenc];
                            for (int i = 0; i < lenc; i++) rawBytes[i] = (byte)br.ReadRC();
                            int b0 = rawBytes.Length > 0 && rawBytes[0] == 0x27 ? 1 : 0;
                            int bn = rawBytes.Length - b0;
                            while (bn > 0 && rawBytes[b0 + bn - 1] == 0) bn--;
                            int cpEff = cpLib == 0x27 ? 0 : cpLib;
                            string text = DecodeCodepageText(rawBytes, b0, bn, cpEff);
                            var units = new ushort[text.Length];
                            for (int i = 0; i < text.Length; i++) units[i] = text[i];
                            int ulen = units.Length;
                            while (ulen > 0 && units[ulen - 1] == 0) ulen--;
                            tw.WriteRC(0);
                            tw.WriteRS((ushort)ulen);
                            for (int i = 0; i < ulen; i++) tw.WriteRS(units[i]);
                            break;
                        }
                        case 4:
                        {
                            if (br.BitPosition + 8 > payloadEnd) { ok = false; break; }
                            int lenc = br.ReadRC();
                            int bits = 8 + lenc * 8;
                            if (subStart + bits > payloadEnd) { ok = false; break; }
                            CopyBits(tw, src, subStart, bits);
                            DwgObjectRecord.SeekTo(br, subStart + bits);
                            break;
                        }
                        case 5:
                        {
                            var hh = br.ReadHandle();
                            tw.WriteHandleRef(hh.Code, hh.Value);
                            break;
                        }
                        case 40 or 41 or 42: br.ReadRD(); break;
                        case 70: br.ReadRS(); break;
                        case 71: br.ReadRL(); break;
                        default:
                            ok = false;
                            break;
                    }
                    if (ok && code is 40 or 41 or 42 or 70 or 71)
                        CopyBits(tw, src, subStart, (int)(br.BitPosition - subStart));
                }
                if (!ok || br.BitPosition != payloadEnd) return false;
                tw.AlignByte();
                int newSize = (int)(tw.BitPosition / 8);
                if (newSize > ushort.MaxValue) return false;
                body.WriteBS((short)newSize);
                body.WriteHandleRef(h.Code, h.Value);
                Append(body, tw);
            }
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string DecodeCodepageText(byte[] bytes, int offset, int count, int cpLib)
    {
        if (count <= 0) return string.Empty;
        int win = cpLib switch
        {
            30 => 1252,
            31 => 936,
            32 => 950,
            38 => 932,
            39 => 936,
            40 => 949,
            41 => 950,
            28 => 1250, 29 => 1251, 44 => 1258,
            25 => 949, 26 => 1361, 27 => 866,
            _ => 936,
        };
        try
        {
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
            var enc = System.Text.Encoding.GetEncoding(win);
            return enc.GetString(bytes, offset, count);
        }
        catch
        {
            try
            {
                return System.Text.Encoding.UTF8.GetString(bytes, offset, count);
            }
            catch
            {
                var sb = new System.Text.StringBuilder(count);
                for (int i = 0; i < count; i++) sb.Append((char)bytes[offset + i]);
                return sb.ToString();
            }
        }
    }

    internal static bool TryWriteEedR2007PlusForEntity(BitWriter body, byte[] src,
        DwgObjectRecord rec, Target t)
    {
        if (!t.UnicodeStrings
            || rec.SourceVersion < DwgVersion.AC1015 || rec.SourceVersion >= DwgVersion.AC1021
            || rec.EedStartBit <= 0 || rec.EedBitLen <= 2)
            return false;
        long eedEnd = rec.EedStartBit + rec.EedBitLen;
        var strictOk = TryCarryEedR2007Plus(new BitWriter(16), src, rec.EedStartBit, eedEnd, rec);
        if (strictOk)
            return TryCarryEedR2007Plus(body, src, rec.EedStartBit, eedEnd, rec);
        var lenientOk = TryCarryEedXdataR2007Plus(new BitWriter(16), src, rec.EedStartBit, eedEnd, rec);
        if (lenientOk)
            return TryCarryEedXdataR2007Plus(body, src, rec.EedStartBit, eedEnd, rec);
        if (DebugTrace)
            System.Console.Error.WriteLine(
                $"EEDUP-FAIL h={rec.Handle:X} t={rec.TypeCode} srcV={rec.SourceVersion} eed={rec.EedStartBit}+{rec.EedBitLen} strict={strictOk} lenient={lenientOk}");
        return false;
    }

    internal static bool TryCarryEedXdataR2007Plus(BitWriter body, byte[] src, long eedStart,
        long eedEnd, DwgObjectRecord rec)
    {
        try
        {
            var br = new BitReader(src);
            DwgObjectRecord.SeekTo(br, eedStart);
            while (br.BitPosition < eedEnd)
            {
                ushort size = (ushort)br.ReadBS();
                if (size == 0) { body.WriteBS(0); break; }
                var h = br.ReadHandle();
                long payloadEnd = br.BitPosition + size * 8;
                if (payloadEnd > eedEnd) return false;
                var tw = new BitWriter(16);
                bool stop = false;
                while (br.BitPosition < payloadEnd && !stop)
                {
                    long subStart = br.BitPosition;
                    int code = br.ReadRC();
                    void Verbatim(int bytes)
                    {
                        if (subStart + bytes * 8 > payloadEnd) { Rest(); return; }
                        CopyBits(tw, src, subStart, bytes * 8);
                        DwgObjectRecord.SeekTo(br, subStart + bytes * 8);
                    }
                    void Rest()
                    {
                        CopyBits(tw, src, subStart, (int)(payloadEnd - subStart));
                        DwgObjectRecord.SeekTo(br, payloadEnd);
                        stop = true;
                    }
                    switch (code)
                    {
                        case 1: Verbatim(3); break;
                        case 2: Verbatim(2); break;
                        case 3: Verbatim(9); break;
                        case 5: Verbatim(9); break;
                        case 10 or 11 or 12 or 13: Verbatim(25); break;
                        case 0:
                        {
                            if (br.BitPosition + 24 > payloadEnd) { Rest(); break; }
                            int lenc = br.ReadRC();
                            _ = br.ReadRS_BE();
                            if (lenc > size || br.BitPosition + lenc * 8 > payloadEnd) { Rest(); break; }
                            var units = new ushort[lenc];
                            for (int i = 0; i < lenc; i++) units[i] = (ushort)br.ReadRC();
                            int ulen = lenc;
                            while (ulen > 0 && units[ulen - 1] == 0) ulen--;
                            tw.WriteRC((byte)code);
                            tw.WriteRS((ushort)ulen);
                            for (int i = 0; i < ulen; i++) tw.WriteRS(units[i]);
                            break;
                        }
                        case 4:
                        {
                            if (br.BitPosition + 8 > payloadEnd) { Rest(); break; }
                            int lenc = br.ReadRC();
                            int bits = 8 + lenc * 8;
                            if (subStart + bits > payloadEnd) { Rest(); break; }
                            CopyBits(tw, src, subStart, bits);
                            DwgObjectRecord.SeekTo(br, subStart + bits);
                            break;
                        }
                        case 40 or 41 or 42: br.ReadRD(); CopyBits(tw, src, subStart, (int)(br.BitPosition - subStart)); break;
                        case 70: br.ReadRS(); CopyBits(tw, src, subStart, (int)(br.BitPosition - subStart)); break;
                        case 71: br.ReadRL(); CopyBits(tw, src, subStart, (int)(br.BitPosition - subStart)); break;
                        default:  Rest(); break;
                    }
                }
                if (br.BitPosition != payloadEnd) return false;
                tw.AlignByte();
                int newSize = (int)(tw.BitPosition / 8);
                if (newSize > ushort.MaxValue) return false;
                body.WriteBS((short)newSize);
                body.WriteHandleRef(h.Code, h.Value);
                Append(body, tw);
            }
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool CentralizeInlineStrings(DwgObjectRecord rec, Target t)
        => t.UnicodeStrings
           && rec.SourceVersion >= DwgVersion.AC1015 && rec.SourceVersion < DwgVersion.AC1021
           && rec.StringFields.Count > 0;

}
