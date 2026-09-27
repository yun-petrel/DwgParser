using DwgParser.Core.IO;

namespace DwgParser.Core.Objects;

public static partial class DwgObjectStreamWriter
{
    private static int BuildXdicSlotHandleStream(BitWriter handles, DwgObjectRecord rec, byte[] src,        Target t, long cfDataEnd, long cfXdicBit, long cfEnd, int cfXdicOp)    {        int handlesBitLen = -1;        int pre = (int)Math.Max(0, cfXdicBit - cfDataEnd);









        int layIns = cfXdicOp < 0 && rec.SourceVersion == DwgVersion.AC1015            && rec.Layout is { Captured: true }            ? t.Version switch { DwgVersion.AC1021 => 16, DwgVersion.AC1018 => 8, _ => 0 }            : 0;
        bool bhFoldedCf = false;
        if (cfXdicOp < 0)
        {
            CopyBits(handles, src, cfDataEnd, pre);
            if (layIns >= 8)
                handles.WriteHandleRef(5, 0);
            if (layIns >= 16)
                handles.WriteHandleRef(4, 0);



            if (rec.LogicalType == TypeBlockHeader                && (t.Version == DwgVersion.AC1021 || t.Version == DwgVersion.AC1018)                && rec.SourceVersion < DwgVersion.AC1018 && rec.BlockOwnedHandles != null)                bhFoldedCf = FoldBlockHeaderOwned(handles, src, cfXdicBit + 8, cfEnd, rec);            if (!bhFoldedCf)
            {
                int tail = (int)Math.Max(0, cfEnd - cfXdicBit - 8);                CopyBits(handles, src, cfXdicBit + 8, tail);
                handlesBitLen = pre + tail + layIns;
            }            else                handlesBitLen = (int)handles.BitPosition;        }
        else
        {            CopyBits(handles, src, cfDataEnd, pre);
            handles.WriteRC(0x30);
            int tail = (int)Math.Max(0, cfEnd - cfXdicBit);            CopyBits(handles, src, cfXdicBit, tail);            handlesBitLen = pre + 8 + tail;        }        long entryEnd = 0;
        {            var hbr = new BitReader(src, 0);            long hp = cfDataEnd;            long kept = 0;
            while (hp + 8 <= cfEnd)            {                DwgObjectRecord.SeekTo(hbr, hp);
                int cc = hbr.ReadRC();




                if (cc == 0) break;
                int bits = 8 + (cc & 0xF) * 8;
                if (hp + bits > cfEnd) break;
                bool isXdicSlot = cfXdicOp < 0 && hp == cfXdicBit;
                hp += bits;                if (isXdicSlot) continue;
                if (cfXdicOp > 0 && hp == cfXdicBit + bits) kept += 8;
                kept += bits;            }










            if (cfXdicOp > 0 && hp == cfXdicBit && kept > 0
                && (rec.SourceVersion.IsR2010Plus()
                    || (t.Version == DwgVersion.AC1015
                        && (rec.SourceVersion == DwgVersion.AC1018
                            || rec.SourceVersion == DwgVersion.AC1021)
                        && IsXdicSlotAtTailType(rec)))) kept += 8;


            if (kept > 0 && kept < handles.BitPosition && !bhFoldedCf && layIns != 8)
            {
                long drop = handles.BitPosition - kept;
                handles.TrimLastBits(drop);
                handlesBitLen -= (int)drop;
            }
            entryEnd = kept;
        }













        long tz = handles.CountTrailingZeroBits();
        long trimPad = (tz / 8) * 8;
        if (layIns != 0) trimPad = 0;







        if (entryEnd > 0) trimPad = 0;
        if (trimPad > 0)
        {
            handles.TrimLastBits(trimPad);
            handlesBitLen -= (int)trimPad;
        }
        return handlesBitLen;
    }















    private static bool IsXdicSlotAtTailType(DwgObjectRecord rec)
        => rec.LogicalType is 0x2A or 0x3C or 0x3E or 0x4F or 0x50
           || ClassTypeCodes.Normalize(rec.ClassName) is "FIELDLIST" or "RASTERVARIABLES";
}
