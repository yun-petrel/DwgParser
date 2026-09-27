using DwgParser.Core.IO;

namespace DwgParser.Core.Objects;

public static partial class DwgObjectStreamWriter
{
    private static bool TryCarryControlDropNullEntries(BitWriter body, DwgObjectRecord rec,
        byte[] src, Target t, out BitWriter? handles)
    {
        handles = null;
        long start = rec.CarryStartBit;
        bool srcPrefixRl = PrefixHasRl(rec.SourceVersion);
        long rlPos = start;
        if (srcPrefixRl) start += 32;
        long hsStart = rec.HandleStreamStartBit;
        if (hsStart <= 0 || !rec.HasHandleStream)
        {
            if (!srcPrefixRl) return false;
            hsStart = (long)rec.BodyStart * 8 + ReadRlAt(src, rlPos);
        }
        long hsEnd = (long)rec.BodyEnd * 8;
        if (start <= 0 || hsStart <= start || hsStart >= hsEnd)
        {
            if (DebugTrace) System.Console.Error.WriteLine(
                $"CTRLDROP? h={rec.Handle:X} t={rec.TypeCode} 拒: start={start} hsStart={hsStart} hsEnd={hsEnd} hasHS={rec.HasHandleStream} bodyStart={rec.BodyStart}");
            return false;
        }
        try
        {
            var br = new BitReader(src, 0);
            DwgObjectRecord.SeekTo(br, start);
            br.ReadHandle();
            if (!SkipEedWriter(br)) return false;
            long numReactors = br.ReadBL();
            bool xdicMissing = br.ReadB();
            long afterXdic = br.BitPosition;
            long dsStart = -1, dsEnd = -1;
            if (rec.SourceVersion >= DwgVersion.AC1027)
            {
                dsStart = br.BitPosition;
                br.ReadB();
                dsEnd = br.BitPosition;
            }
            if (numReactors is < 0 or > 100000)
            {
                if (DebugTrace) System.Console.Error.WriteLine(
                    $"CTRLDROP? h={rec.Handle:X} numReactors 越界={numReactors}");
                return false;
            }
            long numEntriesStart = br.BitPosition;
            long numEntries = br.ReadBL();
            long numEntriesEnd = br.BitPosition;
            if (numEntries is < 0 or > 1_000_000)
            {
                if (DebugTrace) System.Console.Error.WriteLine(
                    $"CTRLDROP? h={rec.Handle:X} numEntries 越界={numEntries}");
                return false;
            }

            var hbr = new BitReader(src, 0);
            DwgObjectRecord.SeekTo(hbr, hsStart);
            hbr.ReadHandle();
            for (long i = 0; i < numReactors; i++) hbr.ReadHandle();
            if (!xdicMissing) hbr.ReadHandle();
            var refStart = new long[(int)numEntries + 1];
            var refVal = new ulong[(int)numEntries];
            refStart[0] = hbr.BitPosition;
            for (long i = 0; i < numEntries; i++)
            {
                var h = hbr.ReadHandle();
                refVal[(int)i] = h.Value;
                refStart[(int)i + 1] = hbr.BitPosition;
            }
            long entriesEnd = hbr.BitPosition;
            if (entriesEnd > hsEnd) return false;

            long dropCount = 0;
            for (long i = numEntries - 1; i >= 0 && refVal[(int)i] == 0; i--) dropCount++;
            if (dropCount == 0) return false;
            long keep = numEntries - dropCount;
            long dropStart = refStart[(int)keep];

            CopyCommonHead(body, src, start, numEntriesStart, dsStart, dsEnd, afterXdic, t);
            body.WriteBL((int)keep);
            CopyBits(body, src, numEntriesEnd, (int)(hsStart - numEntriesEnd));

            long hsEndEff = LastMeaningfulHandleEnd(src, hsStart, hsEnd);
            if (hsEndEff <= entriesEnd) hsEndEff = hsEnd;
            var hw = new BitWriter(16);
            CopyBits(hw, src, hsStart, (int)(dropStart - hsStart));
            CopyBits(hw, src, entriesEnd, (int)(hsEndEff - entriesEnd));
            handles = hw;
            return true;
        }
        catch
        {
            handles = null;
            return false;
        }
    }

    private static bool TryCarryDimStyleControlDowngrade(BitWriter body, DwgObjectRecord rec,
        byte[] src, Target t, out BitWriter? handles)
    {
        handles = null;
        long start = rec.CarryStartBit;
        bool srcPrefixRl = PrefixHasRl(rec.SourceVersion);
        long rlPos = start;
        if (srcPrefixRl) start += 32;
        long hsStart = rec.HandleStreamStartBit;
        if (hsStart <= 0 || !rec.HasHandleStream)
        {
            if (!srcPrefixRl) return false;
            hsStart = (long)rec.BodyStart * 8 + ReadRlAt(src, rlPos);
        }
        long hsEnd = (long)rec.BodyEnd * 8;
        if (start <= 0 || hsStart <= start || hsStart >= hsEnd) return false;
        try
        {
            var br = new BitReader(src, 0);
            DwgObjectRecord.SeekTo(br, start);
            br.ReadHandle();
            if (!SkipEedWriter(br)) return false;
            long numReactors = br.ReadBL();
            bool xdicMissing = br.ReadB();
            long afterXdic = br.BitPosition;
            long dsStart = -1, dsEnd = -1;
            if (rec.SourceVersion >= DwgVersion.AC1027)
            {
                dsStart = br.BitPosition;
                br.ReadB();
                dsEnd = br.BitPosition;
            }
            if (numReactors is < 0 or > 100000) return false;
            long numEntriesPos = br.BitPosition;
            long numEntries = br.ReadBS();
            long numMorePos = br.BitPosition;
            long numMore = br.ReadRC();
            long afterFields = br.BitPosition;
            if (numEntries is < 0 or > 100000 || numMore is < 0 or > 256) return false;
            if (afterFields > hsStart) return false;

            var hbr = new BitReader(src, 0);
            DwgObjectRecord.SeekTo(hbr, hsStart);
            hbr.ReadHandle();
            for (long i = 0; i < numReactors; i++) hbr.ReadHandle();
            if (!xdicMissing) hbr.ReadHandle();
            var entryEnd = new long[(int)numEntries + 1];
            var entryVal = new ulong[(int)numEntries];
            entryEnd[0] = hbr.BitPosition;
            for (long i = 0; i < numEntries; i++)
            {
                var h = hbr.ReadHandle();
                entryVal[(int)i] = h.Value;
                entryEnd[(int)i + 1] = hbr.BitPosition;
            }
            long keptEndPos = hbr.BitPosition;
            if (keptEndPos > hsEnd) return false;

            long dropCount = 0;
            for (long i = numEntries - 1; i >= 0 && entryVal[(int)i] == 0; i--) dropCount++;
            long keep = numEntries - dropCount;
            bool hasMore = numMore > 0;
            if (dropCount == 0 && !hasMore) return false;

            long keptEntriesEnd = entryEnd[(int)keep];

            CopyCommonHead(body, src, start, numEntriesPos, dsStart, dsEnd, afterXdic, t);
            body.WriteBS((short)keep);
            body.WriteRC(0);
            CopyBits(body, src, afterFields, (int)(hsStart - afterFields));

            var hw = new BitWriter(16);
            CopyBits(hw, src, hsStart, (int)(keptEntriesEnd - hsStart));
            handles = hw;
            return true;
        }
        catch
        {
            handles = null;
            return false;
        }
    }


}
