using DwgParser.Core.IO;

namespace DwgParser.Core.Objects;

public static partial class DwgObjectReader
{
    private static void DecodeDimAssoc(BitReader br, DwgVersion version,
        DwgObjectRecord? record, int objStart, bool r2010Plus)
    {
        if (record == null) return;
        var cap = record.DimAssoc ??= new Records.DimAssocCapture();

        if (version >= DwgVersion.AC1021)
        {
            DecodeDimAssocCentralized(br, version, record, objStart, r2010Plus, cap);
            return;
        }
        if (r2010Plus || version < DwgVersion.AC1015)
        {
            Fail(br, cap, record, "未走查(低于受支持档)");
            return;
        }

        uint bitsize = br.ReadRL();
        long hs = (long)objStart * 8 + bitsize;
        if (hs <= br.BitPosition || hs > (long)record.BodyEnd * 8)
        { Fail(br, cap, record, "帧界(RL vs BodyEnd)"); return; }

        long ownStart = br.BitPosition;
        br.ReadHandle();
        long eedStart = br.BitPosition;
        if (!TrySkipEed(br)) { Fail(br, cap, record, "EED"); return; }
        long eedLen = br.BitPosition - eedStart;
        if (br.ReadBL() < 0) { Fail(br, cap, record, "num_reactors<0"); return; }
        if (version.IsR2004Plus()) br.ReadB();

        long assoc = br.ReadBL();
        if (assoc <= 0 || assoc > 0x3F)
        { Fail(br, cap, record, $"associativity={assoc}"); return; }
        cap.Associativity = (int)assoc;
        cap.TransSpaceFlag = br.ReadB();
        cap.RotatedType = br.ReadRC();

        var fields = new List<DwgObjectRecord.StringField>();
        string refFail = "";
        bool TryReadRef(int slot, bool phantom, out Records.DimAssocRef made)
        {
            made = default!;
            long strStart = br.BitPosition;
            string name = br.ReadTV();
            if (name.Length == 0) { refFail = $"ref[{slot}] 类名空串"; return false; }
            fields.Add(new DwgObjectRecord.StringField(
                strStart, (int)(br.BitPosition - strStart), name));

            var rf = new Records.DimAssocRef
            {
                Bit = slot,
                ClassName = name,
                OsnapType = br.ReadRC(),
                Phantom = phantom,
            };
            rf.NumXrefs = br.ReadBL();
            if (rf.NumXrefs < 0 || rf.NumXrefs > 64)
            { refFail = $"ref[{slot}] num_xrefs={rf.NumXrefs}"; return false; }
            if (rf.OsnapType != 0)
            {
                int subent;
                if ((subent = br.ReadBL()) < 0) { refFail = $"ref[{slot}] main_subent_type={subent}"; return false; }
                int gsm;
                if ((gsm = br.ReadBL()) < 0) { refFail = $"ref[{slot}] main_gsmarker={gsm}"; return false; }
                long nx = br.ReadBL();
                if (nx < 0 || nx > 64) { refFail = $"ref[{slot}] num_xrefpaths={nx}"; return false; }
                rf.MainSubentType = subent;
                rf.MainGsMarker = gsm;
                rf.NumXrefPaths = nx;
                for (long i = 0; i < nx; i++)
                    if (!CaptureT(br, fields, rf.XrefPaths))
                    { refFail = $"ref[{slot}] xrefpath[{i}] 空串"; return false; }
            }
            rf.OsnapDistance = br.ReadBD();
            for (int k = 0; k < 3; k++) rf.OsnapPoint[k] = br.ReadBD();
            if (rf.OsnapType is 6 or 11)
            {
                long numIntsect = br.ReadBL();
                if (numIntsect < 0 || numIntsect > 64) { refFail = $"ref[{slot}] num_intsectobj={numIntsect}"; return false; }
                int isub, igsm;
                if ((isub = br.ReadBL()) < 0) { refFail = $"ref[{slot}] intersec_subent_type={isub}"; return false; }
                if ((igsm = br.ReadBL()) < 0) { refFail = $"ref[{slot}] intersec_gsmarker={igsm}"; return false; }
                long ny = br.ReadBL();
                if (ny < 0 || ny > 64) { refFail = $"ref[{slot}] num_intersec_paths={ny}"; return false; }
                rf.NumIntersectObj = (int)numIntsect;
                rf.IntersectSubentType = isub;
                rf.IntersectGsMarker = igsm;
                rf.NumIntersecPaths = ny;
                for (long i = 0; i < ny; i++)
                    if (!CaptureT(br, fields, rf.IntersecPaths))
                    { refFail = $"ref[{slot}] intersecpath[{i}] 空串"; return false; }
            }
            rf.HasLastPtRef = br.ReadB();
            made = rf;
            return true;
        }

        long refsStart = br.BitPosition;
        bool hasLast = false;
        for (int r = 0; r < 7; r++)
        {
            bool inMask = (assoc & (1L << r)) != 0;
            if (!inMask && (!hasLast || cap.Refs.Any(x => x.Phantom))) continue;
            if (!TryReadRef(r, !inMask, out var rf)) { Fail(br, cap, record, refFail); return; }
            hasLast = rf.HasLastPtRef;
            cap.Refs.Add(rf);
        }

        if (br.BitPosition != hs && br.BitPosition < hs && TryPendingRefs(br, hs) == 1)
        {
            var keepRefs = cap.Refs.ToList();
            var keepFields = fields.ToList();
            long rewind = br.BitPosition;
            DwgObjectRecord.SeekTo(br, refsStart);
            cap.Refs.Clear();
            fields.Clear();
            bool allOk = true;
            for (int r = 0; allOk && r < 7; r++)
            {
                bool inMask = (assoc & (1L << r)) != 0;
                bool tail = !inMask && br.BitPosition < hs;
                if (!inMask && !tail) continue;
                if (!TryReadRef(r, !inMask, out var tailRf)) { allOk = false; break; }
                cap.Refs.Add(tailRf);
            }
            if (!allOk || br.BitPosition != hs || cap.Refs.Count == 0)
            {                DwgObjectRecord.SeekTo(br, rewind);
                cap.Refs.Clear();
                cap.Refs.AddRange(keepRefs);
                fields.Clear();
                fields.AddRange(keepFields);
            }
        }

        cap.HsBit = hs;
        cap.EedStartBit = eedStart;
        cap.EedBitLen = (int)eedLen;
        cap.StringCount = fields.Count;

        if (br.BitPosition != hs || fields.Count == 0 || ownStart > hs)
        {
            Fail(br, cap, record, WhyNotClosed(br, hs, fields.Count, ownStart));
            return;
        }

        record.EedStartBit = eedStart;
        record.EedBitLen = (int)eedLen;
        foreach (var f in fields) record.StringFields.Add(f);
        cap.AnchorsCaptured = true;
        cap.ValuesCommitted = true;
        cap.FailReason = "";
        cap.FailBit = -1;
    }

    private static void DecodeDimAssocCentralized(BitReader br, DwgVersion version,
        DwgObjectRecord record, int objStart, bool r2010Plus, Records.DimAssocCapture cap)
    {
        long hs;
        if (r2010Plus)
        {
            hs = record.HandleStreamStartBit;
            if (hs <= 0 || !record.HasHandleStream)
            { Fail(br, cap, record, "帧界(公共路径未给 HS 锚)"); return; }
        }
        else
        {
            uint bitsize = br.ReadRL();
            hs = (long)objStart * 8 + bitsize;
        }
        if (hs <= br.BitPosition || hs > (long)record.BodyEnd * 8)
        { Fail(br, cap, record, "帧界(hs vs BodyEnd)"); return; }

        byte[] data = br.UnderlyingData();
        long footer = CentralFooterStart(data, hs);
        long ss = r2010Plus ? record.StringStreamStartBit : Ac1021StringAreaStart(data, hs);
        if (footer <= 0 || ss <= 0 || ss > footer)
        { Fail(br, cap, record, "无串区(has_strings=0)"); return; }

        br.ReadHandle();
        long eedStart = br.BitPosition;
        if (!TrySkipEed(br)) { Fail(br, cap, record, "EED"); return; }
        long eedLen = br.BitPosition - eedStart;
        if (br.ReadBL() < 0) { Fail(br, cap, record, "num_reactors<0"); return; }
        br.ReadB();
        if (version >= DwgVersion.AC1027) br.ReadB();
        long assoc = br.ReadBL();
        if (assoc <= 0 || assoc > 0x3F)
        { Fail(br, cap, record, $"associativity={assoc}"); return; }
        cap.Associativity = (int)assoc;
        cap.TransSpaceFlag = br.ReadB();
        cap.RotatedType = br.ReadRC();

        var strs = new BitReader(data) { UnicodeStrings = true };
        DwgObjectRecord.SeekTo(strs, ss);
        int strCount = 0;
        var centralAnchors = new List<DwgObjectRecord.StringField>();
        var insAnchors = new List<long>();
        while (br.BitPosition < ss)
        {
            if (cap.Refs.Count >= 6) { Fail(br, cap, record, "关联点逾 6 枚"); return; }
            var rf = new Records.DimAssocRef { Bit = cap.Refs.Count };
            rf.ClassName = CentralT(strs, ref strCount, centralAnchors, br.BitPosition, insAnchors);
            if (rf.ClassName.Length == 0)
            { Fail(br, cap, record, $"ref[{rf.Bit}] 类名空串"); return; }
            rf.OsnapType = br.ReadRC();
            rf.NumXrefs = br.ReadBL();
            if (rf.NumXrefs < 0 || rf.NumXrefs > 64)
            { Fail(br, cap, record, $"ref[{rf.Bit}] num_xrefs={rf.NumXrefs}"); return; }
            if (rf.OsnapType != 0)
            {
                long sub = br.ReadBL(), gsm = br.ReadBL(), nx = br.ReadBL();
                if (sub < 0 || gsm < 0 || nx < 0 || nx > 64)
                { Fail(br, cap, record, $"ref[{rf.Bit}] num_xrefpaths={nx}"); return; }
                rf.MainSubentType = (int)sub;
                rf.MainGsMarker = (int)gsm;
                rf.NumXrefPaths = nx;
                for (long i = 0; i < nx; i++)
                    if (CentralT(strs, ref strCount, centralAnchors, br.BitPosition, insAnchors).Length == 0)
                    { Fail(br, cap, record, $"ref[{rf.Bit}] xrefpath[{i}] 空串"); return; }
            }
            rf.OsnapDistance = br.ReadBD();
            for (int k = 0; k < 3; k++) rf.OsnapPoint[k] = br.ReadBD();
            if (rf.OsnapType is 6 or 11)
            {
                long ni = br.ReadBL(), isub = br.ReadBL(), igsm = br.ReadBL(), ny = br.ReadBL();
                if (ni < 0 || ni > 64 || isub < 0 || igsm < 0 || ny < 0 || ny > 64)
                { Fail(br, cap, record, $"ref[{rf.Bit}] num_intersec_paths={ny}"); return; }
                rf.NumIntersectObj = (int)ni;
                rf.IntersectSubentType = (int)isub;
                rf.IntersectGsMarker = (int)igsm;
                rf.NumIntersecPaths = ny;
                for (long i = 0; i < ny; i++)
                    if (CentralT(strs, ref strCount, centralAnchors, br.BitPosition, insAnchors).Length == 0)
                    { Fail(br, cap, record, $"ref[{rf.Bit}] intersecpath[{i}] 空串"); return; }
            }
            rf.HasLastPtRef = br.ReadB();
            cap.Refs.Add(rf);
        }

        cap.HsBit = hs;
        cap.EedStartBit = eedStart;
        cap.EedBitLen = (int)eedLen;
        cap.StringCount = strCount;
        if (br.BitPosition != ss)
        { Fail(br, cap, record, br.BitPosition < ss ? "数值区短于串区起点" : "数值区越过串区起点"); return; }
        if (strs.BitPosition != footer)
        { Fail(br, cap, record, strs.BitPosition < footer ? "串流短于页脚" : "串流越过页脚"); return; }
        if (cap.Refs.Count == 0) { Fail(br, cap, record, "无关联点"); return; }
        cap.CentralStrings.AddRange(centralAnchors);
        cap.CentralInsertBits.AddRange(insAnchors);
        cap.CentralSsBit = ss;
        cap.CentralCommitted = true;
        cap.ValuesCommitted = true;
        cap.FailReason = "";
        cap.FailBit = -1;
    }

    private static string CentralT(BitReader strs, ref int count,
        List<DwgObjectRecord.StringField>? sink = null,
        long insertAt = -1, List<long>? insSink = null)
    {
        long start = strs.BitPosition;
        string v = strs.ReadTV();
        if (v.Length == 0) return v;
        count++;
        sink?.Add(new DwgObjectRecord.StringField(start, (int)(strs.BitPosition - start), v));
        insSink?.Add(insertAt);
        return v;
    }

    private static long CentralFooterStart(byte[] data, long hs)
    {
        var fr = new BitReader(data, 0);
        DwgObjectRecord.SeekTo(fr, hs - 1);
        if (!fr.ReadB()) return 0;
        DwgObjectRecord.SeekTo(fr, hs - 17);
        uint dataSize = fr.ReadRS();
        int sizeFieldBits = 17;
        if ((dataSize & 0x8000) != 0)
        {
            DwgObjectRecord.SeekTo(fr, hs - 33);
            dataSize = (dataSize & 0x7FFF) | ((uint)fr.ReadRS() << 15);
            sizeFieldBits = 33;
        }
        return hs - sizeFieldBits;
    }

    private static void Fail(BitReader br, Records.DimAssocCapture cap,
        DwgObjectRecord record, string why)
    {
        cap.FailReason = why;
        cap.FailBit = br.BitPosition - record.CarryStartBit;
    }

    private static bool CaptureT(BitReader br, List<DwgObjectRecord.StringField> fields,
        List<string>? values = null)
    {
        long start = br.BitPosition;
        string value = br.ReadTV();
        if (value.Length == 0) return false;
        fields.Add(new DwgObjectRecord.StringField(start, (int)(br.BitPosition - start), value));
        values?.Add(value);
        return true;
    }

    private static string WhyNotClosed(BitReader br, long hs, int fieldCount, long ownStart)
    {
        if (ownStart > hs) return "own 起点越界";
        if (br.BitPosition == hs) return fieldCount == 0 ? "无串可捕" : "不闭合(余量非整枚关联点)";
        if (br.BitPosition > hs) return "不闭合(游标越过 hs)";
        int pending = TryPendingRefs(br, hs);
        return pending > 0 ? $"不闭合(余 {pending} 枚关联点未读)" : "不闭合(余量非整枚关联点)";
    }

    private static int TryPendingRefs(BitReader br, long hs)
    {
        long start = br.BitPosition;
        try
        {
            for (int n = 1; n <= 3; n++)
            {
                if (!TryReadRefShape(br, hs)) return 0;
                if (br.BitPosition == hs) return n;
                if (br.BitPosition > hs) return 0;
            }
        }
        catch { return 0; }
        finally { DwgObjectRecord.SeekTo(br, start); }
        return 0;
    }

    private static bool TryReadRefShape(BitReader br, long hs)
    {
        if (br.ReadTV().Length == 0) return false;
        int osnapType = br.ReadRC();
        long numXrefs = br.ReadBL();
        if (numXrefs < 0 || numXrefs > 64) return false;
        if (osnapType != 0)
        {
            if (br.ReadBL() < 0 || br.ReadBL() < 0) return false;
            long nx = br.ReadBL();
            if (nx < 0 || nx > 64) return false;
            for (long i = 0; i < nx; i++)
                if (br.ReadTV().Length == 0) return false;
        }
        for (int k = 0; k < 4; k++) br.ReadBD();
        if (osnapType is 6 or 11)
        {
            long ni = br.ReadBL();
            if (ni < 0 || ni > 64) return false;
            if (br.ReadBL() < 0 || br.ReadBL() < 0) return false;
            long ny = br.ReadBL();
            if (ny < 0 || ny > 64) return false;
            for (long i = 0; i < ny; i++)
                if (br.ReadTV().Length == 0) return false;
        }
        br.ReadB();
        return br.BitPosition <= hs;
    }
}
