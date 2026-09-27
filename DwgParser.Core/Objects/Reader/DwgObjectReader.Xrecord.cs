using DwgParser.Core.IO;

namespace DwgParser.Core.Objects;

public static partial class DwgObjectReader
{
    private const short TypeXrecord = 0x4F;

    private static bool DecodeXrecord(BitReader br, DwgVersion version, DwgObjectRecord? record)
    {
        if (record == null) return false;
        var cap = new Records.XrecordCapture();
        cap.AttachIdentity(record);
        record.Xrecord = cap;
        try
        {
            return DecodeXrecordCore(br, version, record, cap);
        }
        catch (System.Exception ex)
        {
            cap.Captured = false;
            cap.FailReason = "exception " + ex.GetType().Name;
            cap.FailBit = br.BitPosition;
            return false;
        }
    }

    private static bool DecodeXrecordCore(BitReader br, DwgVersion version,
        DwgObjectRecord record, Records.XrecordCapture cap)
    {
        bool r2010Plus = version.IsR2010Plus();
        bool r2004Plus = version.IsR2004Plus();
        bool r2013Plus = version >= DwgVersion.AC1027;
        bool unicodeStrings = version >= DwgVersion.AC1021;

        if (!r2010Plus) br.ReadRL();
        br.ReadHandle();
        if (!TrySkipEed(br)) return Fail(cap, br, "eed");
        long numReactors = br.ReadBL();
        bool xdicMissing = false;
        if (r2004Plus)
        {
            xdicMissing = br.ReadB();
            if (r2013Plus) br.ReadB();
        }
        cap.XdicMissing = xdicMissing;
        if (numReactors is < 0 or > 100000) return Fail(cap, br, $"num_reactors={numReactors}");

        int xsize = br.ReadBL();
        cap.XdataSize = xsize;
        if (xsize < 0) return Fail(cap, br, $"xdata_size={xsize}");

        long xdStart = br.BitPosition;
        long xdEnd = xdStart + (long)xsize * 8;
        if (xdEnd > (long)record.BodyEnd * 8)
            return Fail(cap, br, $"xdata_size={xsize} overruns frame");
        cap.DataStartBit = xdStart;
        cap.DataEndBit = xdEnd;

        long declaredEnd = xdEnd;
        bool strict = TryWalkResbuf(br, xdStart, declaredEnd, declaredEnd, false, unicodeStrings, cap);
        if (!strict)
        {
            var (reason, bit) = (cap.FailReason, cap.FailBit);
            ResetWalk(cap);
            const int FrameOverrunToleranceBits = 8;
            long frameEndBit = (long)record.BodyEnd * 8 + FrameOverrunToleranceBits;
            if (TryWalkResbuf(br, xdStart, frameEndBit, declaredEnd, true, unicodeStrings, cap))
            {
                xdEnd = br.BitPosition;
                cap.DataEndBit = xdEnd;
                cap.XdataBeyondDeclared = true;
                cap.DataBeyondFrame = xdEnd > (long)record.BodyEnd * 8;
            }
            else
            {
                var (r2, b2) = (cap.FailReason, cap.FailBit);
                int items = cap.Items.Count;
                ResetWalk(cap);
                cap.FailReason =
                    $"{reason}（放宽亦退 {r2}@{b2}; 已消费 {bit - xdStart}/{xsize * 8} 位, 帧界余 {frameEndBit - declaredEnd} 位, 项数 {items}）";
                cap.FailBit = bit;
                return false;
            }
        }

        DwgObjectRecord.SeekTo(br, xdEnd);
        cap.CloningBeyondFrame = xdEnd + 16 > (long)record.BodyEnd * 8;
        cap.Cloning = br.ReadBS();
        cap.Captured = true;
        cap.FailReason = "";
        cap.FailBit = -1;
        return true;
    }

    private static void ResetWalk(Records.XrecordCapture cap)
    {
        cap.Items.Clear();
    }

    private static bool Fail(Records.XrecordCapture cap, BitReader br, string why)
    {
        cap.Captured = false;
        cap.FailReason = why;
        cap.FailBit = br.BitPosition;
        return false;
    }

    private static bool TryWalkResbuf(BitReader br, long start, long end, long limit, bool frameBounded,
        bool unicodeStrings, Records.XrecordCapture cap)
    {
        try
        {
            return TryWalkResbufCore(br, start, end, limit, frameBounded, unicodeStrings, cap);
        }
        catch
        {
            return Fail(cap, br, "out-of-stream");
        }
    }

    private static bool TryWalkResbufCore(BitReader br, long start, long end, long limit, bool frameBounded,
        bool unicodeStrings, Records.XrecordCapture cap)
    {
        DwgObjectRecord.SeekTo(br, start);
        bool terminated = false;
        int guard = 0;
        while (br.BitPosition < end)
        {
            if (guard++ > 1_000_000) return Fail(cap, br, "guard");
            long itemStart = br.BitPosition;
            int code = br.ReadRS();
            int valBytes = 0; bool isString = false; bool unmapped = code is < 0 or >= 2000;
            if (!unmapped)
            {
                if (code >= 300)
                {
                    if (code >= 440)
                    {
                        if (code >= 1000)
                        {
                            if (code == 1004) { valBytes = -1; }
                            else if (code <= 1009) isString = true;
                            else if (code <= 1039) valBytes = 24;
                            else if (code <= 1042) valBytes = 8;
                            else if (code <= 1069) valBytes = 24;
                            else if (code == 1070) valBytes = 2;
                            else if (code == 1071) valBytes = 4;
                            else unmapped = true;
                        }
                        else if (code <= 459) valBytes = 4;
                        else if (code <= 469) valBytes = 8;
                        else if (code <= 479) isString = true;
                        else if (code == 999) isString = true;
                        else unmapped = true;
                    }
                    else if (code <= 309) isString = true;
                    else if (code <= 319) valBytes = -1;
                    else if (code <= 329) valBytes = 8;
                    else if (code <= 369) valBytes = 8;
                    else if (code <= 389) valBytes = 2;
                    else if (code <= 399) valBytes = 8;
                    else if (code <= 409) valBytes = 2;
                    else if (code <= 419) isString = true;
                    else if (code <= 429) valBytes = 4;
                    else isString = true;
                }
                else if (code >= 105)
                {
                    if (code >= 210)
                    {
                        if (code <= 269) valBytes = 24;
                        else if (code <= 279) valBytes = 2;
                        else if (code <= 289) valBytes = 1;
                        else valBytes = 1;
                    }
                    else if (code == 105) valBytes = 8;
                    else if (code <= 109) unmapped = true;
                    else if (code <= 139) valBytes = 24;
                    else if (code <= 149) valBytes = 8;
                    else if (code <= 169) valBytes = 8;
                    else if (code <= 179) valBytes = 2;
                    else unmapped = true;
                }
                else if (code >= 38)
                {
                    if (code <= 59) valBytes = 8;
                    else if (code <= 79) valBytes = 2;
                    else if (code <= 99) valBytes = 4;
                    else isString = true;
                }
                else if (code <= 4) isString = true;
                else if (code == 5) valBytes = 8;
                else if (code <= 9) isString = true;
                else valBytes = 24;
            }

            if (unmapped)
            {
                if (frameBounded && itemStart >= limit)
                {
                    DwgObjectRecord.SeekTo(br, itemStart);
                    terminated = true;
                    break;
                }
                return Fail(cap, br, $"unmapped code {code}");
            }

            var item = new Records.XrecordItem { Code = code };
            if (isString)
            {
                if (!TryReadStringItem(br, end, unicodeStrings, item))
                    return Fail(cap, br, $"string overrun code {code}");
            }
            else if (valBytes == -1)
            {
                int lenc = br.ReadRC();
                if (br.BitPosition + (long)lenc * 8 > end)
                    return Fail(cap, br, $"binary overrun code {code} len {lenc}");
                var bin = new byte[lenc];
                for (int i = 0; i < lenc; i++) bin[i] = br.ReadRC();
                item.Binary = bin;
            }
            else
            {
                if (br.BitPosition + (long)valBytes * 8 > end)
                    return Fail(cap, br, $"scalar overrun code {code}");
                ReadScalarItem(br, code, valBytes, item);
            }
            cap.Items.Add(item);
            item.BitLen = (int)(br.BitPosition - itemStart);
            if (br.BitPosition <= itemStart) return Fail(cap, br, "no progress");
        }
        if (frameBounded)
        {
            if (!terminated) return Fail(cap, br, $"relaxed no-terminator at {br.BitPosition}");
            if (br.BitPosition < limit) return Fail(cap, br, "relaxed underread");
        }
        else if (br.BitPosition != limit)
            return Fail(cap, br, $"misaligned: consumed {cap.Items.Count} items");
        return true;
    }

    private static bool TryReadStringItem(BitReader br, long end, bool unicodeStrings,
        Records.XrecordItem item)
    {
        int len = br.ReadRS();
        if (len < 0) return false;
        if (unicodeStrings)
        {
            if (br.BitPosition + (long)len * 16 > end) return false;
            var chars = new char[len];
            for (int i = 0; i < len; i++) chars[i] = (char)br.ReadRS();
            int ulen = len;
            while (ulen > 0 && chars[ulen - 1] == '\0') ulen--;
            item.Text = new string(chars, 0, ulen);
        }
        else
        {
            if (br.BitPosition + 8 + (long)len * 8 > end) return false;
            byte cp = br.ReadRC();
            var bytes = new byte[len];
            for (int i = 0; i < len; i++) bytes[i] = br.ReadRC();
            int blen = len;
            while (blen > 0 && bytes[blen - 1] == 0) blen--;
            var enc = br.LegacyEncoding ?? System.Text.Encoding.Latin1;
            item.Text = enc.GetString(bytes, 0, blen);
        }
        return true;
    }

    private static void ReadScalarItem(BitReader br, int code, int valBytes, Records.XrecordItem item)
    {
        bool isHandleCode = code == 5 || code == 105
            || (code >= 320 && code <= 329) || (code >= 330 && code <= 369)
            || (code >= 390 && code <= 399);
        if (isHandleCode && valBytes == 8)
        {
            ulong v = 0;
            for (int i = 0; i < 8; i++) v = (v << 8) | br.ReadRC();
            item.IsHandle = true;
            item.HandleValue = v;
            return;
        }
        if (valBytes == 24)
        {
            item.Point = new[] { br.ReadRD(), br.ReadRD(), br.ReadRD() };
            return;
        }
        if (valBytes == 8)
        {
            if (IsRealCode(code)) { item.Number = br.ReadRD(); return; }
            ulong lo = br.ReadRL();
            ulong hi = br.ReadRL();
            item.Number = unchecked((long)(lo | (hi << 32)));
            return;
        }
        if (valBytes == 4) { item.Number = (long)br.ReadRL(); return; }
        if (valBytes == 2) { item.Number = (long)br.ReadRS(); return; }
        item.Number = (long)br.ReadRC();
    }

    private static bool IsRealCode(int code)
        => (code >= 38 && code <= 59) || (code >= 140 && code <= 149)
           || (code >= 460 && code <= 469) || (code >= 1040 && code <= 1042);
}
