using DwgParser.Core.IO;

namespace DwgParser.Core.Objects;

public static partial class DwgObjectStreamWriter
{

    private static long FindAfterNumReactorsPublic(byte[] src, long ownStart, long limit)
    {
        try
        {
            var br = new BitReader(src, 0);
            DwgObjectRecord.SeekTo(br, ownStart);
            br.ReadHandle();
            if (!DwgObjectReader.TrySkipEed(br)) return -1;
            int nr = br.ReadBL();
            if (nr is < 0 or > 8) return -1;
            long p = br.BitPosition;
            return p <= limit ? p : -1;
        }
        catch { return -1; }
    }

    private static bool TryCarryBlockHeaderR2007(BitWriter body, byte[] src, long from, long to,
        DwgObjectRecord rec, Target t)
    {
        if (rec.StringFields.Count == 0) return false;
        var name = rec.StringFields[0];
        long nameStart = name.Start, nameEnd = name.Start + name.BitLen;
        if (nameStart < from || nameEnd > to) return false;
        if (!TryReadTableXrefGroup(src, nameEnd, to, out short xrefResolved, out int xrefBits))
            return false;
        long _entry = body.BitPosition;
        long afterXref = nameEnd + xrefBits;
        long pos = from;
        if (t.UnicodeStrings
            && rec.SourceVersion >= DwgVersion.AC1015 && rec.SourceVersion < DwgVersion.AC1021
            && rec.EedStartBit > 0 && rec.EedBitLen > 2
            && rec.EedStartBit >= from && rec.EedStartBit + rec.EedBitLen <= to
            && TryCarryEedR2007Plus(new BitWriter(16), src, rec.EedStartBit,
                rec.EedStartBit + rec.EedBitLen, rec))
        {
            CopyBits(body, src, from, (int)(rec.EedStartBit - from));
            TryCarryEedR2007Plus(body, src, rec.EedStartBit,
                rec.EedStartBit + rec.EedBitLen, rec);
            pos = rec.EedStartBit + rec.EedBitLen;
            if (DebugTrace)
                System.Console.Error.WriteLine(
                    $"BLKEED h={rec.Handle:X} t={rec.TypeCode} EED [{rec.EedStartBit},{rec.EedStartBit + rec.EedBitLen}) → TU 化");
        }
        if (from == rec.CarryStartBit + 32 && t.Version != DwgVersion.AC1021
            && (!rec.SourceVersion.IsR2004Plus() || t.R2013Plus))
        {
            long insertAt = FindAfterNumReactorsPublic(src, from, nameStart);
            if (insertAt < 0 || insertAt < pos || insertAt > nameStart) return false;
            CopyBits(body, src, pos, (int)(insertAt - pos));
            if (rec.SourceVersion.IsR2004Plus())
            {
                if (insertAt >= nameStart) return false;
                body.WriteB(ReadSrcBit(src, insertAt));
                pos = insertAt + 1;
            }
            else
            {
                if (!t.R2000)
                {
                    long xhsUp = rec.HandleStreamStartBit > 0
                        ? rec.HandleStreamStartBit
                        : (long)rec.BodyStart * 8 + ReadRlAt(src, rec.CarryStartBit);
                    body.WriteB(SourceXdicRefIsNull(src, xhsUp, (long)rec.BodyEnd * 8));
                }
                pos = insertAt;
            }
            if (t.R2013Plus) body.WriteB(false);
        }
        CopyBits(body, src, pos, (int)(nameStart - pos));
        long _a = body.BitPosition;
        body.WriteBS(xrefResolved);
        long _b = body.BitPosition;
        var br = new BitReader(src, 0);
        br.SetPosition((int)(afterXref >> 3), (int)(afterXref & 7));
        br.ReadB(); br.ReadB(); br.ReadB(); br.ReadB(); br.ReadB();
        bool pre4 = rec.SourceVersion < DwgVersion.AC1018;
        if (pre4 && rec.BlockOwnedHandles is null) return false;
        if (!pre4) br.ReadBL();
        long basePtStart = br.BitPosition;
        if (DebugTrace)
            System.Console.Error.WriteLine(
                $"DBG h={rec.Handle:X} from={from} nameStart={nameStart} afterXref={afterXref} " +
                $"basePtStart={basePtStart} to={to} xrefBits={xrefBits}");
        CopyBits(body, src, afterXref, (int)(basePtStart - afterXref));
        if (pre4)
            body.WriteBL(rec.BlockOwnedHandles!.Count);
        long _c = body.BitPosition;
        var bp = br.Read3BD();
        body.WriteBD(bp.X); body.WriteBD(bp.Y); body.WriteBD(bp.Z);
        long _d = body.BitPosition;
        string xrefPname = br.ReadTV();
        long niStart = br.BitPosition;
        while (true)
        {
            if (br.BitPosition + 8 > to) return false;
            if (br.ReadRC() == 0) break;
        }
        long niEnd = br.BitPosition;
        CopyBits(body, src, niStart, (int)(niEnd - niStart));
        long _e = body.BitPosition;
        string description = br.ReadTV();
        long descEnd = br.BitPosition;
        long _f = body.BitPosition;
        if (descEnd < to) CopyBits(body, src, descEnd, (int)(to - descEnd));
        long _g = body.BitPosition;
        body.WriteBS(0);
        body.WriteB(true);
        body.WriteRC(0);
        long _h = body.BitPosition;
        long cs = body.BitPosition;
        WriteCentralTu(body, name.Value);
        WriteCentralTu(body, xrefPname);
        WriteCentralTu(body, description);
        int contentBits = (int)(body.BitPosition - cs);
        WriteStringStreamSize(body, (uint)contentBits);
        body.WriteB(true);
        if (DebugTrace)
            System.Console.Error.WriteLine(
                $"DBG2 h={rec.Handle:X} written={(body.BitPosition - _entry) }bits (expect incl SS≈330)");
        if (DebugTrace)
            System.Console.Error.WriteLine(
                $"BLKUP h={rec.Handle:X} t={rec.TypeCode} {rec.SourceVersion}→{t.Version} " +
                $"xp=\"{xrefPname}\" desc=\"{description}\" SS={contentBits}位");
        return true;
    }

    private static bool TryCarryBlockHeaderOwnedCount(BitWriter body, byte[] src, long from, long to,
        DwgObjectRecord rec, bool rlInBody)
    {
        if (rec.BlockOwnedHandles is not { } owned) return false;
        if (rec.StringFields.Count == 0) return false;
        var name = rec.StringFields[0];
        long nameEnd = name.Start + name.BitLen;
        if (name.Start < from || nameEnd > to) return false;
        if (!TryReadTableXrefGroup(src, nameEnd, to, out _, out int xrefBits)) return false;
        long afterXref = nameEnd + xrefBits;
        var br = new BitReader(src, 0);
        br.SetPosition((int)(afterXref >> 3), (int)(afterXref & 7));
        for (int i = 0; i < 5; i++) br.ReadB();
        long insertAt = br.BitPosition;
        if (insertAt <= from || insertAt > to) return false;
        long hsStart = (long)rec.BodyStart * 8 + ReadRlAt(src, rec.CarryStartBit);
        int blBits;
        if (hsStart > insertAt && hsStart < to)
        {
            if (!CarryBlockHeaderOwnedWithHandles(body, src, from, to, insertAt, hsStart,
                    rec, out blBits))
                return false;
        }
        else
        {
            long blAt = body.BitPosition;
            CopyBits(body, src, from, (int)(insertAt - from));
            body.WriteBL(owned.Count);
            blBits = (int)(body.BitPosition - blAt);
            CopyBits(body, src, insertAt, (int)(to - insertAt));
        }
        if (rlInBody && blBits > 0)
        {
            uint rlNow = new BitReader(body.ToArray(), 0).ReadRL();
            body.PatchRL(0, unchecked((uint)(rlNow + blBits)));
            if (DebugTrace)
                System.Console.Error.WriteLine(
                    $"BHNOWNED18RL h={rec.Handle:X} RL {rlNow}→{rlNow + blBits}（num_owned +{blBits} 位）");
        }
        if (DebugTrace)
            System.Console.Error.WriteLine(
                $"BHNOWNED18 h={rec.Handle:X} t={rec.TypeCode} num_owned={owned.Count}"
                + $" 插入 rel={insertAt - (long)rec.BodyStart * 8}（源数据区 {to - from} 位）");
        return true;
    }

    private static bool RecIsAc1015BlockHeader(DwgObjectReader.WalkResult walk)
        => walk.Records.Any(r => r.LogicalType == TypeBlockHeader && r.Handle != 0
            && r.SourceVersion == DwgVersion.AC1015);

    private static void StampBlockHeaderOwnedVectors(DwgObjectReader.WalkResult walk, byte[] src, DwgVersion target)
    {
        if (target != DwgVersion.AC1021 && target != DwgVersion.AC1018 && !target.IsR2010Plus()) return;
        foreach (var rec in walk.Records)
        {
            if (rec.LogicalType != TypeBlockHeader || rec.Handle == 0) continue;
            if (rec.SourceVersion < DwgVersion.AC1015 || rec.SourceVersion >= DwgVersion.AC1018) continue;
            var owned = TryReadBlockOwnedChain(walk, src, rec);
            if (owned != null)
            {
                rec.BlockOwnedHandles = owned;
                if (DebugTrace)
                    System.Console.Error.WriteLine(
                        $"BHVEC h={rec.Handle:X} n={owned.Count} [{string.Join(",", owned.Select(x => x.ToString("X")))}]");
            }
        }
    }

    private static List<ulong>? TryReadBlockOwnedChain(DwgObjectReader.WalkResult walk, byte[] src, DwgObjectRecord rec)
    {
        var br = new BitReader(src);
        DwgObjectRecord.SeekTo(br, rec.CarryStartBit);
        uint rl = br.ReadRL();
        long hs = (long)rec.BodyStart * 8 + rl;
        long end = (long)rec.BodyEnd * 8;
        if (hs <= 0 || end <= hs) return null;
        br.ReadHandle();
        if (!DwgObjectReader.TrySkipEed(br)) return null;
        int nr = br.ReadBL();
        if (nr is < 0 or > 32) return null;
        var refs = new List<(int code, ulong val)>();
        for (long hp = hs; hp + 8 <= end && refs.Count < nr + 12; )
        {
            DwgObjectRecord.SeekTo(br, hp);
            int cc = br.ReadRC();
            int bits = 8 + (cc & 0xF) * 8;
            if (hp + bits > end) break;
            ulong v = 0;
            for (int i = 0; i < (cc & 0xF); i++) v = (v << 8) | br.ReadRC();
            refs.Add((cc >> 4, v));
            hp += bits;
        }
        int b0 = 1 + nr;
        if (refs.Count < b0 + 6
            || refs[0].code != 4
            || refs[b0].code != 3 || refs[b0 + 1].code != 5 || refs[b0 + 2].code != 3
            || refs[b0 + 3].code != 4 || refs[b0 + 4].code != 4
            || refs[b0 + 5].code != 3)
            return null;
        ulong first = refs[b0 + 3].val, last = refs[b0 + 4].val;
        if (first == 0 && last == 0) return new List<ulong>();
        if (first == 0 || last == 0) return null;
        var vec = new List<ulong>();
        foreach (var e in walk.Entities)
        {
            if (e.Handle == 0 || e.OwnerHandle != rec.Handle) continue;
            if (e.Kind is DwgEntityType.Block or DwgEntityType.EndBlk) continue;
            vec.Add(e.Handle);
        }
        if (vec.Count == 0 || vec[0] != first)
        {
            vec.Clear();
            List<ulong> nested = new();
            foreach (var e in walk.Entities)
            {
                if (e.Handle == 0 || e.Handle < first || e.Handle > last) continue;
                if (e.Kind is DwgEntityType.Block or DwgEntityType.EndBlk) continue;
                if (e.OwnerHandle != 0 && e.OwnerHandle != rec.Handle)
                {
                    nested.Add(e.Handle);
                    continue;
                }
                vec.Add(e.Handle);
            }
            var window = vec.Concat(nested).ToList();
            window.Sort();
            vec.Sort();
            if (window.Count == 0 || window[0] != first || window[^1] != last) return null;
            if (vec.Count == 0 || vec[0] != first) return null;
            if (DebugTrace)
                System.Console.Error.WriteLine(
                    $"BHTOP h={rec.Handle:X} 区间 {window.Count} 枚 → 顶层 {vec.Count} 枚"
                    + $"（剥嵌套子实体 {nested.Count} 枚）");
        }
        return vec;
    }




    internal static void WriteTableStyleOverride(BitWriter body, Records.TableStyleRowCapture row,
        int bl3, int id, int ctype, int merge, double m0, double m1)
    {
        body.WriteBL(bl3);
        bool margins = m0 != 0.0 || m1 != 0.0;
        WriteTableCellStyle(body, row, th: row.TextHeight, align: row.Alignment,
            vdt: row.DataType, vut: row.UnitType, merge: merge, m0: m0, m1: m1, margins: margins);
        body.WriteBL(id);
        body.WriteBL(ctype);
    }

    internal static void WriteTableCellStyle(BitWriter body, Records.TableStyleRowCapture row,
        double th, int align, int vdt, int vut, int merge, double m0, double m1, bool margins)
    {
        body.WriteBL(5);
        body.WriteBS(1);
        body.WriteBL(0);
        body.WriteBL(merge);
        WriteCmc(body, row.FillColor);
        body.WriteBL(1);
        body.WriteBL(0);
        body.WriteBL(0);
        body.WriteBL(vdt);
        body.WriteBL(vut);
        body.WriteBD(0.0);
        body.WriteBD(1.0);
        body.WriteBL(align);
        WriteCmc(body, row.TextColor);
        body.WriteBD(th);
        body.WriteBS(margins ? (short)1 : (short)0);
        if (margins)
        {
            body.WriteBD(m0);
            body.WriteBD(m1);
            body.WriteBD(0.06);
            body.WriteBD(0.06);
            body.WriteBD(0.18);
            body.WriteBD(0.18);
        }
        body.WriteBL(6);
        for (int i = 0; i < 6; i++)
        {
            body.WriteBL(1 << i);
            body.WriteBL(0);
            body.WriteBL(1);
            WriteCmc(body, row.BorderCol[i]);
            body.WriteBL(row.BorderLw[i]);
            body.WriteBL(0);
            body.WriteBD(0.045);
        }
    }

    internal static void WriteCmc(BitWriter w, (int Idx, uint Rgb, byte Flag) c)
    {
        w.WriteBS((short)c.Idx);
        w.WriteBL(unchecked((int)c.Rgb));
        w.WriteRC(c.Flag);
    }

    private static void WriteTableStyleTstyBlock(BitWriter w, ulong tsty)
    {
        w.WriteHandleRef(5, tsty);
        for (int i = 0; i < 6; i++) w.WriteHandleRef(5, 0);
    }

    private static readonly byte[] Vs511Template =
    {
        0x92, 0x0F, 0x00, 0x00, 0x00, 0x18, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0xA2, 0x04, 0x03, 0x00, 0x00, 0x00, 0x18, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0xA2, 0x02, 0x33, 0x00, 0x00, 0x00, 0x18,
        0x00, 0x10, 0x00, 0x00, 0x01, 0x80, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x0A, 0x20, 0x40, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01, 0x72, 0x02,
        0x02, 0x8C, 0xC0, 0x00, 0x00, 0x06, 0x00, 0x02, 0x33, 0x00, 0x00, 0x00,
        0x18, 0x00, 0x00, 0x00,
    };

    private static readonly byte[] Vs510Template =
    {
        0x93, 0xCD, 0x00, 0x00, 0x00, 0x18, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0xA2, 0x04, 0x00, 0x00, 0x00, 0x60, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x02, 0x88, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01,
        0x22, 0x02, 0x33, 0x00, 0x00, 0x00, 0x18, 0x00, 0x09, 0x94, 0x00, 0x00,
        0x00, 0x60, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x02, 0x88, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xA2, 0x04, 0x00, 0x00, 0x00, 0x60,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x02, 0x88, 0x10, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x5C, 0x80, 0x80, 0xC0, 0x00, 0x00, 0x06, 0x00,
        0x04, 0x00, 0x00, 0x00, 0x64, 0x00, 0x31, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x88, 0x10, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x08, 0x80,
        0x83, 0x40, 0xC1, 0x6A, 0x22, 0xA7, 0xD9, 0x0F, 0xC9, 0xF8, 0xCA, 0xE6,
        0x5A, 0x70, 0xA5, 0x83, 0xA0, 0x7E, 0x1F, 0xE8, 0x1C, 0x63, 0x53, 0x78,
        0xFA, 0x1F, 0x8C, 0xAE, 0x65, 0xA7, 0x0A, 0x58, 0x3A, 0x17, 0xE7, 0x94,
        0x4A, 0x7F, 0x12, 0x7A, 0x97, 0xE9, 0xF8,
    };

    private static bool TryCarryViewStyleR2010(BitWriter body, byte[] src, long from, long to,
        DwgObjectRecord rec)
    {
        long hs = rec.HandleStreamStartBit;
        if (hs <= 0 || to != hs) return false;
        var vs = rec.ViewStyle;
        if (vs == null) return false;
        long ownStart = rec.CarryStartBit + 32;
        long headEnd = vs.TemplateHeadEndBit;
        if (headEnd <= ownStart || headEnd > hs) return false;
        if (from == ownStart)
            CopyBits(body, src, ownStart, (int)(headEnd - ownStart));
        else if (from != headEnd)
            return false;
        byte[] tmpl = rec.LogicalType == TypeSectionViewStyle ? Vs510Template : Vs511Template;
        int tplBits = rec.LogicalType == TypeSectionViewStyle ? 1301 : 603;
        var tplR = new BitReader(tmpl);
        for (int i = 0; i < tplBits; i++)
            body.WriteB(tplR.ReadB());
        bool is510 = rec.LogicalType == TypeSectionViewStyle;
        long contentStart = body.BitPosition;
        WriteCentralTu(body, vs.TemplateDesc);
        WriteCentralTu(body, vs.TemplateExclude);
        WriteCentralTu(body, vs.TemplatePattern);
        if (is510)
            WriteCentralTu(body, vs.TemplateHatchPattern);
        int contentBits = (int)(body.BitPosition - contentStart);
        WriteStringStreamSize(body, (uint)contentBits);
        body.WriteB(true);
        rec.StringFields.Add(new DwgObjectRecord.StringField(
            contentStart, contentBits, vs.TemplateDesc ?? string.Empty));
        if (DebugTrace) System.Console.Error.WriteLine(
            $"VSUP h={rec.Handle:X} type={rec.TypeCode} {rec.SourceVersion}→R2010 tmpl={tplBits}位 SS={contentBits}位 body={body.BitPosition}位");
        return true;
    }

    internal static void WriteStringStreamSize(BitWriter w, uint dataSize)
    {
        if (dataSize < 0x8000)
        {
            w.WriteRS((ushort)dataSize);
        }
        else
        {
            w.WriteRS((ushort)((dataSize >> 15) & 0x7FFF));
            w.WriteRS((ushort)((dataSize & 0x7FFF) | 0x8000));
        }
    }

    private static bool TryReadCentralString(byte[] src, long startBit, out string value)
    {
        value = string.Empty;
        if (startBit < 0 || startBit >= (long)src.Length * 8) return false;
        try
        {
            var br = new BitReader(src) { UnicodeStrings = true };
            DwgObjectRecord.SeekTo(br, startBit);
            value = br.ReadTV();
            return true;
        }
        catch
        {
            return false;
        }
    }


}

public static partial class DwgObjectStreamWriter
{
    private static void RewriteBlockHeaderOwnedCountInBody(BitWriter body, byte[] src, DwgObjectRecord rec)
    {
        try
        {
            byte[] written = body.ToArray();
            long total = body.BitPosition;
            if (total <= 0) return;
            var br = new BitReader(written, 0);
            long p = 0;
            if (!rec.SourceVersion.IsR2010Plus()) { br.ReadRL(); p = br.BitPosition; }
            br.ReadHandle();
            if (!DwgObjectReader.TrySkipEed(br)) return;
            int nr = br.ReadBL();
            if (nr is < 0 or > 8) return;
            if (rec.SourceVersion.IsR2004Plus()) br.ReadB();
            if (rec.SourceVersion >= DwgVersion.AC1027) br.ReadB();
            if (rec.SourceVersion < DwgVersion.AC1021) br.ReadTV();
            if (rec.SourceVersion < DwgVersion.AC1021) { br.ReadB(); br.ReadBS(); br.ReadB(); }
            else br.ReadBS();
            for (int i = 0; i < 5; i++) br.ReadB();
            long countStart = br.BitPosition;
            br.ReadBL();
            long countEnd = br.BitPosition;
            if (countStart <= p || countEnd > total) return;
            long suffix = total - countEnd;
            body.TrimLastBits(total - countStart);
            body.WriteBL(rec.BlockOwnedHandles!.Count);
            if (suffix > 0) CopyBits(body, written, countEnd, (int)suffix);
            rec.DataRegionRewritten = true;

            if (DebugTrace)
                System.Console.Error.WriteLine(
                    $"BHCNT60 h={rec.Handle:X} src={rec.SourceVersion} num_owned " +
                    $"{rec.BlockOwnedSourceCount}→{rec.BlockOwnedHandles!.Count} (bit {countStart}..{countEnd}) " +
                    $"newBodyBits={body.BitPosition} totalWas={total}");
        }
        catch (Exception ex) when (ex is EndOfStreamException or IndexOutOfRangeException
                                      or ArgumentException or OverflowException)
        {
        }
    }
}

public static partial class DwgObjectStreamWriter
{
    internal static bool TryBlockHeaderHandleStreamStart(DwgObjectRecord rec, byte[] src, out long hs)
    {
        hs = -1;
        if (rec.SourceVersion.IsR2010Plus())
        {
            hs = rec.HandleStreamStartBit;
            return hs > 0;
        }
        try
        {
            var br = new BitReader(src, 0);
            DwgObjectRecord.SeekTo(br, rec.CarryStartBit);
            uint rl = br.ReadRL();
            hs = (long)rec.BodyStart * 8 + rl;
            return hs > 0;
        }
        catch (Exception ex) when (ex is EndOfStreamException or IndexOutOfRangeException
                                      or ArgumentException or OverflowException)
        {
            return false;
        }
    }
}
