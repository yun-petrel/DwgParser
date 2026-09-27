using DwgParser.Core.IO;

namespace DwgParser.Core.Objects;

public static partial class DwgObjectStreamWriter
{
    private static bool CarriesAppidR2007Down(DwgObjectRecord rec, Target t)
        => (t.Version == DwgVersion.AC1018 || t.Version == DwgVersion.AC1015)
           && rec.SourceVersion >= DwgVersion.AC1021
           && rec.LogicalType == TypeAppId
           && rec.Appid is { Captured: true };

    private static bool CarryAppidR2007Down(BitWriter body, DwgObjectRecord rec, byte[] src,
        Target t, out BitWriter? handles)
    {
        handles = null;
        var cap = rec.Appid;
        if (cap is null || !cap.Captured) return false;
        long start = rec.CarryStartBit;
        long ssStart = rec.StringStreamStartBit;
        long dataEnd = 0;
        if (ssStart <= start)
        {
            if (!DictionaryCarry.TryAc1021Spans(src, rec, out dataEnd, out ssStart,
                    out bool hasStrings) || !hasStrings) return false;
            start += 32;
        }
        try
        {
            var br = new BitReader(src, 0);
            DwgObjectRecord.SeekTo(br, start);
            br.ReadHandle();
            long eedStart = br.BitPosition;
            if (!SkipEedWriter(br)) return false;
            int eedLen = (int)(br.BitPosition - eedStart);
            long numReactors = br.ReadBL();
            if (numReactors is < 0 or > 100_000) return false;
            bool xdic = br.ReadB();
            if (rec.SourceVersion >= DwgVersion.AC1027) br.ReadB();
            int xrefResolved = br.ReadBS();
            long tailStart = br.BitPosition;
            if (tailStart <= start || tailStart >= ssStart) return false;

            CopyBits(body, src, start, (int)(eedStart - start));
            if (!TryWriteEedPreR2007Range(body, src, eedStart, eedLen))
                CopyBits(body, src, eedStart, eedLen);
            body.WriteBL((int)numReactors);
            if (t.Version != DwgVersion.AC1015) body.WriteB(xdic);
            body.WriteTV(cap.Name);
            body.WriteB(true);
            body.WriteBS((short)xrefResolved);
            body.WriteB(false);
            CopyBits(body, src, tailStart, (int)(ssStart - tailStart));
            if (dataEnd > 0 && !DictionaryCarry.TryAc1021HandlesTail(rec, src, dataEnd, 1,
                    out handles))
                return false;
            if (t.Version == DwgVersion.AC1015 && handles is null
                && rec.HandleStreamStartBit > 0 && rec.SourceVersion.IsR2010Plus())
            {
                long hsStart = rec.HandleStreamStartBit;
                long hsEnd = LastMeaningfulHandleEnd(src, hsStart, (long)rec.BodyEnd * 8);
                if (hsEnd <= hsStart) return false;
                var self = new BitWriter(16);
                CopyBits(self, src, hsStart, (int)(hsEnd - hsStart));
                handles = self;
            }
            if (t.Version == DwgVersion.AC1015)
            {
                if (!xdic || handles is null) return false;
                var hb = handles;
                long total = hb.BitPosition;
                var hbr = new BitReader(hb.ToArray());
                int cc = hbr.ReadRC();
                long ownerBits = 8 + (cc & 0xF) * 8L;
                if (ownerBits >= total) return false;
                var shifted = new BitWriter(16);
                DwgObjectStreamWriter.CopyBits(shifted, hb.ToArray(), 0, (int)ownerBits);
                shifted.WriteHandleRef(3, 0);
                DwgObjectStreamWriter.CopyBits(shifted, hb.ToArray(), (int)ownerBits,
                    (int)(total - ownerBits));
                handles = shifted;
            }
            if (DebugTrace)
                System.Console.Error.WriteLine(
                    $"APIDOWN h={rec.Handle:X} {rec.SourceVersion}→{t.Version} name=\"{cap.Name}\" " +
                    $"xref={xrefResolved} unk71={(cap.HasUnknown71 ? cap.Unknown71 : -1)} " +
                    $"tailBits={ssStart - tailStart}");
            return true;
        }
        catch
        {
            return false;
        }
    }
}
