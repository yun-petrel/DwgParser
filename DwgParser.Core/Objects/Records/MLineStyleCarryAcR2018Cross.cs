using DwgParser.Core.IO;
using static DwgParser.Core.Objects.DwgObjectStreamWriter;

namespace DwgParser.Core.Objects.Records;

internal sealed class MLineStyleCarryAcR2018Cross : MLineStyleCarryPlan
{
    public override string Name => "MLS:跨R_2018(双向)";

    public override bool IsCaptured(DwgObjectRecord rec)
        => rec.HasHandleStream || rec.SourceVersion == DwgVersion.AC1021;

    internal override bool TryCarry(BitWriter body, DwgObjectRecord rec, byte[] src,
        LtypeTable ltypeTable, Target t, out BitWriter? handles)
    {
        handles = null;
        bool upgrade = rec.SourceVersion < DwgVersion.AC1032;
        long start = rec.CarryStartBit;
        if (PrefixHasRl(rec.SourceVersion))
            start += 32;
        long hsStart = rec.HandleStreamStartBit > 0
            ? rec.HandleStreamStartBit
            : PreR2010HandleStreamStart(src, rec);
        if (start <= 0 || hsStart <= start || hsStart > (long)rec.BodyEnd * 8) return false;
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
            br.ReadBS();
            SkipCmcMainstream(br);
            br.ReadBD(); br.ReadBD();
            int numLines = br.ReadRC();
            if (numLines is < 0 or > 256) return false;
            var ltStart = new long[numLines];
            var ltEnd = new long[numLines];
            var ltValue = new int[numLines];
            var lineEnd = new long[numLines];
            for (int i = 0; i < numLines; i++)
            {
                br.ReadBD();
                SkipCmcMainstream(br);
                lineEnd[i] = br.BitPosition;
                if (upgrade)
                {
                    ltStart[i] = br.BitPosition;
                    ltValue[i] = br.ReadBS();
                    ltEnd[i] = br.BitPosition;
                }
            }
            if (br.BitPosition > hsStart) return false;
            long afterLines = br.BitPosition;

            var hbr = new BitReader(src, 0);
            DwgObjectRecord.SeekTo(hbr, hsStart);
            var prefixRefs = new List<(int Code, ulong Value)>(2 + (int)numReactors);
            var lineHandles = new List<ulong>(numLines);
            void TakeRef()
            {
                var h = hbr.ReadHandle();
                prefixRefs.Add((h.Code, h.Value));
            }
            TakeRef();
            for (long i = 0; i < numReactors; i++) TakeRef();
            if (!xdicMissing) TakeRef();
            if (upgrade)
            {
                for (int i = 0; i < numLines; i++)
                    lineHandles.Add(LtypeHandleForIndex(ltValue[i], ltypeTable));
            }
            else
            {
                for (int i = 0; i < numLines; i++)
                {
                    var h = hbr.ReadHandle();
                    lineHandles.Add(h.Resolve(rec.Handle));
                }
                for (int i = 0; i < numLines; i++)
                    ltValue[i] = LtypeIndexForHandle(lineHandles[i], ltypeTable);
            }
            if (hbr.BitPosition > (long)rec.BodyEnd * 8) return false;

            long headEnd = numLines > 0 ? (upgrade ? ltStart[0] : lineEnd[0]) : afterLines;
            CopyCommonHead(body, src, start, headEnd, dsStart, dsEnd, afterXdic, t);
            long cursor = headEnd;
            if (upgrade)
            {
                for (int i = 0; i < numLines; i++)
                {
                    if (ltStart[i] > cursor)
                        CopyBits(body, src, cursor, (int)(ltStart[i] - cursor));
                    cursor = ltEnd[i];
                }
                CopyBits(body, src, cursor, (int)(hsStart - cursor));
            }
            else
            {
                for (int i = 0; i < numLines; i++)
                {
                    CopyBits(body, src, cursor, (int)(lineEnd[i] - cursor));
                    body.WriteBS((short)ltValue[i]);
                    cursor = lineEnd[i];
                }
                CopyBits(body, src, cursor, (int)(hsStart - cursor));
            }

            var hw = new BitWriter(16);
            foreach (var (code, value) in prefixRefs)
                hw.WriteHandleRef(code, value);
            if (upgrade)
            {
                for (int i = 0; i < numLines; i++)
                    hw.WriteHandleRef(5, lineHandles[i]);
            }
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
