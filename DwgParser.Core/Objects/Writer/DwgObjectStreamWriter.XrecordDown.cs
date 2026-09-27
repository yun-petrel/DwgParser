using DwgParser.Core.IO;
using static DwgParser.Core.Objects.DwgObjectStreamWriter;

namespace DwgParser.Core.Objects;

public static partial class DwgObjectStreamWriter
{
    internal const short TypeXrecordLogical = 0x4F;

    internal const byte XdataCodepageLib = 39;

    private static int XrVar()
    {
        var s = System.Environment.GetEnvironmentVariable("XRVAR");
        return int.TryParse(s, out var v) ? v : 1;
    }

    internal static bool XrecordDowngradesR2007(DwgObjectRecord rec, Target t)
        => XrVar() != 0
           && (t.Version == DwgVersion.AC1018 || t.Version == DwgVersion.AC1015)
           && rec.SourceVersion >= DwgVersion.AC1021
           && rec.LogicalType == TypeXrecordLogical
           && rec.Xrecord != null;

    internal static bool CarryXrecordDowngrade(BitWriter body, DwgObjectRecord rec, byte[] src, Target t,
        out long derivedHsBit, out long xdicInsertBit)
    {
        derivedHsBit = 0;
        xdicInsertBit = -1;
        long start = rec.CarryStartBit;
        if (start <= 0) return false;
        bool srcHasRl = !rec.SourceVersion.IsR2010Plus();
        long dataBegin;
        long dataEnd;
        if (srcHasRl)
        {
            uint rlSrc = ReadRlAt(src, start);
            dataBegin = start + 32;
            dataEnd = (long)rec.BodyStart * 8 + rlSrc;
            if (rlSrc < 34 || dataEnd <= dataBegin || dataEnd > (long)rec.BodyEnd * 8) return false;
        }
        else
        {
            if (!rec.HasHandleStream || rec.HandleStreamStartBit <= start
                || rec.HandleStreamStartBit > (long)rec.BodyEnd * 8) return false;
            dataBegin = start;
            dataEnd = rec.HandleStreamStartBit;
        }

        try
        {
            var br = new BitReader(src, 0);
            DwgObjectRecord.SeekTo(br, dataBegin);
            br.ReadHandle();
            if (!DwgObjectReader.TrySkipEed(br)) return false;
            long numReactors = br.ReadBL();
            long xdicBit = -1;
            if (t.Version != DwgVersion.AC1015) br.ReadB();
            else { xdicBit = br.BitPosition; br.ReadB(); }
            long dsBit = -1;
            if (rec.SourceVersion >= DwgVersion.AC1027) { dsBit = br.BitPosition; br.ReadB(); }
            long xsizeFieldStart = br.BitPosition;
            int xsize = br.ReadBL();
            if (xsize < 0) return false;
            long xdStart = br.BitPosition;
            long xdEnd = xdStart + (long)xsize * 8;
            if (xdEnd > dataEnd) return false;

            if (!ConvertXdataPreR2007(new BitWriter(16), src, xdStart, xdEnd, out int newSize))
                return false;
            if (newSize is < 0 or > 0xFFFF) return false;

            DwgObjectRecord.SeekTo(br, xdEnd);
            br.ReadBS();
            long tailEnd = br.BitPosition;
            if (tailEnd + 1 != dataEnd) return false;

            if (t.Version == DwgVersion.AC1015 && xdicBit >= 0)
            {
                long hsStart = dataEnd;
                long hsEnd = (long)rec.BodyEnd * 8;
                long ins = LocateHandleRef(src, hsStart, hsEnd, (int)numReactors + 2);
                if (ins <= 0)
                {
                    var (cnt, refEnd) = WalkMeaningfulHandleRefs(src, hsStart, hsEnd);
                    if (cnt == (int)numReactors + 1) ins = refEnd;
                }
                if (ins <= 0) return false;
                xdicInsertBit = ins;
            }

            long drop1 = xdicBit >= 0 ? xdicBit : (dsBit >= 0 ? dsBit : -1);
            long drop2 = xdicBit >= 0 && dsBit >= 0 ? dsBit : -1;
            if (drop1 < 0) CopyBits(body, src, dataBegin, (int)(xsizeFieldStart - dataBegin));
            else
            {
                CopyBits(body, src, dataBegin, (int)(drop1 - dataBegin));
                long seg2End = drop2 >= 0 ? drop2 : xsizeFieldStart;
                CopyBits(body, src, drop1 + 1, (int)(seg2End - drop1 - 1));
                if (drop2 >= 0) CopyBits(body, src, drop2 + 1, (int)(xsizeFieldStart - drop2 - 1));
            }
            body.WriteBL(XrVar() == 2 ? xsize : newSize);
            if (!ConvertXdataPreR2007(body, src, xdStart, xdEnd, out int n2) || n2 != newSize)
                return false;
            CopyBits(body, src, xdEnd, (int)(tailEnd - xdEnd));
            derivedHsBit = dataEnd;
            if (DebugTrace)
                System.Console.Error.WriteLine(
                    $"XRDOWN h={rec.Handle:X} xsize {xsize}→{newSize} xd=[{xdStart},{xdEnd}) " +
                    $"dataEnd={dataEnd} hs={derivedHsBit} var={XrVar()}");
            return true;
        }
        catch { return false; }
    }

    private static bool ConvertXdataPreR2007(BitWriter body, byte[] src, long start, long end,
        out int newSize)
    {
        newSize = 0;
        try
        {
            var br = new BitReader(src, 0) { UnicodeStrings = true };
            DwgObjectRecord.SeekTo(br, start);
            long bodyStart = body.BitPosition;
            while (br.BitPosition < end)
            {
                long itemStart = br.BitPosition;
                int code = br.ReadRS();
                if (code is < 0 or >= 2000) return false;
                if (!ClassifyResbuf(code, out int valBytes, out bool isString)) return false;
                if (isString)
                {
                    int len = br.ReadRS();
                    if (len < 0 || br.BitPosition + (long)len * 16 > end) return false;
                    var chars = new char[len];
                    for (int i = 0; i < len; i++) chars[i] = (char)br.ReadRS();
                    int ulen = len;
                    while (ulen > 0 && chars[ulen - 1] == '\0') ulen--;
                    byte[] bytes = BitWriter.EncodeText(new string(chars, 0, ulen));
                    if (bytes.Length > short.MaxValue) return false;
                    body.WriteRS((ushort)code);
                    body.WriteRS((ushort)bytes.Length);
                    body.WriteRC(XdataCodepageLib);
                    foreach (var b in bytes) body.WriteRC(b);
                }
                else if (valBytes == -1)
                {
                    int lenc = br.ReadRC();
                    if (br.BitPosition + (long)lenc * 8 > end) return false;
                    CopyBits(body, src, itemStart, 16 + 8 + lenc * 8);
                    DwgObjectRecord.SeekTo(br, itemStart + 16 + 8 + (long)lenc * 8);
                }
                else
                {
                    if (br.BitPosition + (long)valBytes * 8 > end) return false;
                    CopyBits(body, src, itemStart, 16 + valBytes * 8);
                    DwgObjectRecord.SeekTo(br, itemStart + 16 + (long)valBytes * 8);
                }
            }
            if (br.BitPosition != end) return false;
            newSize = (int)(body.BitPosition - bodyStart) / 8;
            return true;
        }
        catch { return false; }
    }

    private static bool ClassifyResbuf(int code, out int valBytes, out bool isString)
    {
        valBytes = 0; isString = false;
        if (code >= 300)
        {
            if (code >= 440)
            {
                if (code >= 1000)
                {
                    if (code == 1004) valBytes = -1;
                    else if (code <= 1009) isString = true;
                    else if (code <= 1039) valBytes = 24;
                    else if (code <= 1042) valBytes = 8;
                    else if (code <= 1069) valBytes = 24;
                    else if (code == 1070) valBytes = 2;
                    else if (code == 1071) valBytes = 4;
                    else return false;
                }
                else if (code <= 459) valBytes = 4;
                else if (code <= 469) valBytes = 8;
                else if (code <= 479) isString = true;
                else if (code == 999) isString = true;
                else return false;
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
            else if (code <= 109) return false;
            else if (code <= 139) valBytes = 24;
            else if (code <= 149) valBytes = 8;
            else if (code <= 169) valBytes = 8;
            else if (code <= 179) valBytes = 2;
            else return false;
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
        return true;
    }
}
