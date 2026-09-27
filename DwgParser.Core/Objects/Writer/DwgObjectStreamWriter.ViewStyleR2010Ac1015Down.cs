using System.Collections.Generic;
using DwgParser.Core.IO;
using DwgParser.Core.Objects.Records;

namespace DwgParser.Core.Objects;

public static partial class DwgObjectStreamWriter
{
    private static bool CarriesViewStyleR2010ToAc1015(DwgObjectRecord rec, Target t)
        => t.Version == DwgVersion.AC1015
           && rec.SourceVersion >= DwgVersion.AC1021
           && rec.LogicalType is TypeDetailViewStyle or TypeSectionViewStyle
           && rec.HasHandleStream
           && rec.HandleStreamStartBit > 0
           && rec.StringStreamStartBit > 0
           && rec.ViewStyle is { ValuesCommitted: true, FieldValues: not null };

    private static bool CarryViewStyleR2010ToAc1015(BitWriter body, DwgObjectRecord rec,
        byte[] src, out BitWriter? handles)
    {
        handles = null;
        var vals = rec.ViewStyle!.FieldValues!;
        bool section = rec.LogicalType == TypeSectionViewStyle;
        long start = rec.CarryStartBit
            + (rec.SourceVersion.IsR2010Plus() ? 0 : 32);
        long ss = rec.StringStreamStartBit;
        long hs = rec.HandleStreamStartBit;
        if (start <= 0 || ss <= start || hs <= ss) return false;

        const int Cmc = 0, Tv = 1, Drop = 2;
        var edits = new List<(long A, long B, int Kind, string? Tv)>();
        long eedStart = 0, eedEnd = 0, headEnd = 0;
        int nr = 0;
        try
        {
            var br = new BitReader(src, 0);
            DwgObjectRecord.SeekTo(br, start);

            void AddCmc()
            {
                long a = br.BitPosition;
                short idx = br.ReadBS();
                br.ReadBL();
                if (br.ReadRC() != 0 || idx is < 0 or > 256)
                    throw new InvalidDataException("510/511 CMC 有名/簿");
                edits.Add((a, br.BitPosition, Cmc, null));
            }

            void AddTv(string value) => edits.Add((br.BitPosition, br.BitPosition, Tv, value ?? ""));

            br.ReadHandle();
            eedStart = br.BitPosition;
            if (!SkipEedWriter(br)) return false;
            eedEnd = br.BitPosition;
            nr = (int)br.ReadBL();
            if (nr is < 0 or > 8) return false;
            br.ReadB();
            if (rec.SourceVersion >= DwgVersion.AC1027) br.ReadB();
            headEnd = br.BitPosition;
            if (br.ReadBS() != 0) return false;
            AddTv(vals.Description);
            br.ReadB();
            if (vals.ViewstyleFlags != ViewStyleValues.NoField)
            {
                long vf0 = br.BitPosition;
                br.ReadBL();
                edits.Add((vf0, br.BitPosition, Drop, null));
            }
            if (br.ReadBS() != 0) return false;
            br.ReadBL();
            AddCmc();
            br.ReadBD();
            if (section)
            {
                AddCmc();
                br.ReadBD();
                AddTv(vals.IdentifierExcludeCharacters);
                br.ReadBD();
                br.ReadBL();
                AddCmc();
                br.ReadBL();
                AddCmc();
                br.ReadBD();
                br.ReadBD();
                AddCmc();
                br.ReadBD();
                br.ReadBL();
                br.ReadBD();
                br.ReadBL();
                AddTv(vals.ViewlabelPattern);
                AddCmc();
                AddCmc();
                AddTv(((SectionViewStyleValues)vals).HatchPattern);
                br.ReadBD();
                br.ReadBL();
                br.ReadB();
                br.ReadB();
                br.ReadBL();
                br.ReadBD();
                br.ReadBL();
                br.ReadBD();
                int k = (int)br.ReadBL();
                if (k is < 0 or > 1024) return false;
                for (int i = 0; i < k; i++) br.ReadBD();
            }
            else
            {
                AddTv(vals.IdentifierExcludeCharacters);
                br.ReadBD();
                br.ReadRC();
                AddCmc();
                br.ReadBD();
                br.ReadBL();
                AddCmc();
                AddCmc();
                br.ReadBD();
                br.ReadBL();
                br.ReadBD();
                br.ReadBL();
                AddTv(vals.ViewlabelPattern);
                br.ReadBL();
                AddCmc();
                br.ReadBL();
                AddCmc();
                br.ReadRC();
            }
            if (br.BitPosition != ss) return false;
        }
        catch { return false; }

        long entry = body.BitPosition;
        try
        {
            CopyBits(body, src, start, (int)(eedStart - start));
            if (!TryWriteEedPreR2007Range(body, src, eedStart, (int)(eedEnd - eedStart)))
                CopyBits(body, src, eedStart, (int)(eedEnd - eedStart));
            body.WriteBL(nr);
            long cur = headEnd;
            foreach (var (a, b, kind, tv) in edits)
            {
                if (a < cur || b < a || b > ss) throw new InvalidDataException("edit 区间不合");
                CopyBits(body, src, cur, (int)(a - cur));
                if (kind == Cmc) { if (!WriteFoldedCmc(body, src, a, b)) throw new InvalidDataException("CMC 折形不合"); }
                else if (kind == Tv) body.WriteTV(tv, false);
                cur = b;
            }
            CopyBits(body, src, cur, (int)(ss - cur));

            long hsEnd = LastMeaningfulHandleEnd(src, hs, (long)rec.BodyEnd * 8);
            if (hsEnd <= hs) throw new InvalidDataException("句柄流不收口");
            var hw = new BitWriter(16);
            CopyBits(hw, src, hs, (int)(hsEnd - hs));
            long insertAt = DictionaryCarry.ScanOwnerReactorPrefix(hw.ToArray());
            if (insertAt < 0) insertAt = AllCode4RunEnd(hw);
            if (insertAt < 0) insertAt = WalkRefsByCount(hw, 1 + nr);
            if (insertAt <= 0 || insertAt > hw.BitPosition) throw new InvalidDataException("插点不合");
            var probe = new BitReader(hw.ToArray(), 0);
            DwgObjectRecord.SeekTo(probe, insertAt);
            if (insertAt == hw.BitPosition || (probe.ReadRC() >> 4) != 3)
            {
                long total = hw.BitPosition;
                var shifted = new BitWriter(16);
                CopyBits(shifted, hw.ToArray(), 0, (int)insertAt);
                shifted.WriteHandleRef(3, 0);
                CopyBits(shifted, hw.ToArray(), (int)insertAt, (int)(total - insertAt));
                hw = shifted;
            }
            handles = hw;
            if (DebugTrace)
                System.Console.Error.WriteLine(
                    $"VSDN1015 h={rec.Handle:X} t={rec.TypeCode} {rec.SourceVersion}→AC1015 " +
                    $"折 {System.Linq.Enumerable.Count(edits, e => e.Kind == Cmc)} 枚 CMC + " +
                    $"内联 {System.Linq.Enumerable.Count(edits, e => e.Kind == Tv)} 枚串 + " +
                    $"剥 {System.Linq.Enumerable.Count(edits, e => e.Kind == Drop)} 段 剥串区{hs - ss}位 " +
                    $"body={body.BitPosition} hs={hw.BitPosition}@{insertAt}");
            return true;
        }
        catch
        {
            body.TrimLastBits(body.BitPosition - entry);
            handles = null;
            return false;
        }
    }

    private static long WalkRefsByCount(BitWriter hs, int count)
    {
        if (count <= 0) return -1;
        try
        {
            var data = hs.ToArray();
            var br = new BitReader(data, 0);
            long p = 0, end = (long)data.Length * 8;
            int seen = 0;
            while (seen < count && p + 8 <= end)
            {
                DwgObjectRecord.SeekTo(br, p);
                int cc = br.ReadRC();
                long nb = 8 + (cc & 0xF) * 8L;
                if (p + nb > end) return -1;
                p += nb; seen++;
            }
            return seen == count ? p : -1;
        }
        catch { return -1; }
    }
}
