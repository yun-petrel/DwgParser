using DwgParser.Core.IO;

namespace DwgParser.Core.Objects;

public static partial class DwgObjectStreamWriter
{
    private static bool CarriesDataLinkR2010ToAc1015(DwgObjectRecord rec, Target t)
        => (t.Version == DwgVersion.AC1015 && rec.SourceVersion >= DwgVersion.AC1021
            || t.Version == DwgVersion.AC1018 && rec.SourceVersion >= DwgVersion.AC1021)
           && rec.LogicalType == TypeDataLink
           && rec.DataLink is { ValuesCommitted: true, StringCount: > 0 };

    private static bool CarryDataLinkR2010ToAc1015(BitWriter body, DwgObjectRecord rec,
        byte[] src, Target t, out BitWriter? handles)
    {
        handles = null;
        bool r2004 = t.Version == DwgVersion.AC1018;
        var cap = rec.DataLink!;
        long start = rec.CarryStartBit + (rec.SourceVersion.IsR2010Plus() ? 0 : 32);
        long ss = cap.StringAreaStartBit;
        if (start <= 0 || ss <= start || cap.HsBit <= ss) return false;

        long eedStart = 0, eedEnd = 0;
        int nr = 0;
        long xdic = 1;
        long option, updateOption, bl92, pathOption, bl93, numCustom;
        var ts = new long[7];
        try
        {
            var br = new BitReader(src, 0);
            DwgObjectRecord.SeekTo(br, start);
            br.ReadHandle();
            eedStart = br.BitPosition;
            if (!SkipEedWriter(br)) return false;
            eedEnd = br.BitPosition;
            nr = (int)br.ReadBL();
            if (nr is < 0 or > 1000) return false;
            if (r2004) xdic = br.ReadB() ? 1 : 0;
            else br.ReadB();
            if (rec.SourceVersion >= DwgVersion.AC1027) br.ReadB();
            option = br.ReadBL();
            updateOption = br.ReadBL();
            bl92 = br.ReadBL();
            for (int i = 0; i < 7; i++) ts[i] = br.ReadBS();
            pathOption = br.ReadBS();
            bl93 = br.ReadBL();
            numCustom = br.ReadBL();
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
            if (r2004) body.WriteB(xdic != 0);
            WriteDataLinkTvPreR2007(body, cap.DataAdapter);
            WriteDataLinkTvPreR2007(body, cap.Description);
            WriteDataLinkTvPreR2007(body, cap.Tooltip);
            WriteDataLinkTvPreR2007(body, cap.ConnectionString);
            body.WriteBL((int)option);
            body.WriteBL((int)updateOption);
            body.WriteBL((int)bl92);
            for (int i = 0; i < 7; i++) body.WriteBS(unchecked((short)ts[i]));
            body.WriteBS(unchecked((short)pathOption));
            body.WriteBL((int)bl93);
            WriteDataLinkTvPreR2007(body, cap.UpdateStatus);
            body.WriteBL((int)numCustom);

            long hsEnd = LastMeaningfulHandleEnd(src, cap.HsBit, (long)rec.BodyEnd * 8);
            if (hsEnd <= cap.HsBit) throw new InvalidDataException("句柄流不收口");
            var hw = new BitWriter(16);
            CopyBits(hw, src, cap.HsBit, (int)(hsEnd - cap.HsBit));
            long insertAt = hw.BitPosition;
            if (!r2004)
            {
                insertAt = DataLinkXdicInsertAt(hw);
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
            }
            handles = hw;
            if (DebugTrace)
                System.Console.Error.WriteLine(
                    $"DL15DN h={rec.Handle:X} t={rec.TypeCode} {rec.SourceVersion}→{t.Version} " +
                    $"串5 剥串区{cap.HsBit - ss}位 body={body.BitPosition} hs={hw.BitPosition}@{insertAt}");
            return true;
        }
        catch
        {
            body.TrimLastBits(body.BitPosition - entry);
            handles = null;
            return false;
        }
    }

    private static long DataLinkXdicInsertAt(BitWriter hs)
    {
        try
        {
            var data = hs.ToArray();
            var br = new BitReader(data, 0);
            long p = 0, end = (long)data.Length * 8;
            int seen = 0;
            while (p + 8 <= end && seen < 2)
            {
                DwgObjectRecord.SeekTo(br, p);
                int cc = br.ReadRC();
                long nb = 8 + (cc & 0xF) * 8L;
                if (p + nb > end) return -1;
                p += nb; seen++;
            }
            return seen == 2 ? p : -1;
        }
        catch { return -1; }
    }

    private static void WriteDataLinkTvPreR2007(BitWriter body, string? value)
    {
        if (string.IsNullOrEmpty(value)) { body.WriteBS(0); return; }
        var bytes = BitWriter.EncodeText(value);
        body.WriteBS(unchecked((short)(bytes.Length + 1)));
        foreach (var b in bytes) body.WriteRC(b);
        body.WriteRC(0);
    }
}
