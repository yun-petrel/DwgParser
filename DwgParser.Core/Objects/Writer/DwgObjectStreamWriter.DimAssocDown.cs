using DwgParser.Core.IO;

namespace DwgParser.Core.Objects;

public static partial class DwgObjectStreamWriter
{
    private static int DaVar()
    {
        var s = System.Environment.GetEnvironmentVariable("DAVAR");
        return int.TryParse(s, out var v) ? v : 1;
    }

    internal static bool DimAssocDowngradesR2007(DwgObjectRecord rec, Target t)
    {
        var cap = rec.DimAssoc;
        return cap != null
            && DaVar() != 0
            && cap.CentralCommitted
            && rec.SourceVersion >= DwgVersion.AC1021
            && !t.R2007Plus
            && t.Version == DwgVersion.AC1018
            && cap.CentralSsBit > 0
            && cap.CentralStrings.Count > 0
            && cap.CentralStrings.Count == cap.CentralInsertBits.Count;
    }

    internal static bool CarryDimAssocDowngrade(BitWriter body, DwgObjectRecord rec, byte[] src, Target t)
    {
        if (!DimAssocDowngradesR2007(rec, t)) return false;
        var cap = rec.DimAssoc!;
        long ss = cap.CentralSsBit;
        long start = rec.CarryStartBit + (PrefixHasRl(rec.SourceVersion) ? 32 : 0);
        if (ss <= start) return false;

        long cut = -1;
        if (rec.SourceVersion >= DwgVersion.AC1027)
        {
            var hd = new BitReader(src, 0);
            DwgObjectRecord.SeekTo(hd, start);
            hd.ReadHandle();
            if (!DwgObjectReader.TrySkipEed(hd)) return false;
            hd.ReadBL();
            hd.ReadB();
            cut = hd.BitPosition;
            hd.ReadB();
            if (cut >= ss) return false;
        }
        void CopyHead(BitWriter w, long from, long to)
        {
            if (to <= from) return;
            if (cut < 0 || to <= cut || from > cut + 1) { CopyBits(w, src, from, (int)(to - from)); return; }
            if (from < cut) CopyBits(w, src, from, (int)(cut - from));
            CopyBits(w, src, cut + 1, (int)(to - cut - 1));
        }

        long cursor = start;
        for (int i = 0; i < cap.CentralStrings.Count; i++)
        {
            long ins = cap.CentralInsertBits[i];
            if (ins < cursor || ins > ss) return false;
            CopyHead(body, cursor, ins);
            body.WriteTV(cap.CentralStrings[i].Value, false);
            cursor = ins;
        }
        CopyHead(body, cursor, ss);
        if (DaVar() == 2)
            CopyBits(body, src, cap.HsBit - 17, 17);
        if (DebugTrace)
            System.Console.Error.WriteLine(
                $"DIMASSDN h={rec.Handle:X} 内联 {cap.CentralStrings.Count} 枚 TV，丢串区 [{ss}, {cap.HsBit})");
        return true;
    }
}
