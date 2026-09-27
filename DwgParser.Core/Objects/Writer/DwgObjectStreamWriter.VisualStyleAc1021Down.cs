using DwgParser.Core.IO;

namespace DwgParser.Core.Objects;

public static partial class DwgObjectStreamWriter
{
    private static bool CarriesVisualStyleAc1021Down(DwgObjectRecord rec, Target t)
        => t.Version == DwgVersion.AC1018
           && rec.SourceVersion == DwgVersion.AC1021
           && rec.LogicalType == TypeVisualStyle
           && rec.VisualStyle is { Captured: true }
           && rec.HasHandleStream;

    private static bool CarryVisualStyleAc1021Down(BitWriter body, DwgObjectRecord rec, byte[] src,
        Target t)
    {
        var cap = rec.VisualStyle;
        if (cap == null) return false;
        long start = rec.CarryStartBit + 32;
        long body0 = cap.BodyStartBit;
        long ss = cap.StreamStartBit;
        long hs = rec.HandleStreamStartBit;
        var sp = cap.FieldSpans;
        if (start <= 0 || body0 <= start || ss <= body0 || hs <= ss) return false;
        if (sp.Length != 60 || sp[0] != body0) return false;
        for (int i = 0; i + 3 < sp.Length; i += 2)
            if (sp[i + 1] != sp[i + 2]) return false;
        long fieldsEnd = sp[sp.Length - 1];
        if (fieldsEnd + 3 != ss) return false;
        try
        {
            string desc;
            {
                var sr = new BitReader(src, 0) { UnicodeStrings = true };
                DwgObjectRecord.SeekTo(sr, ss);
                desc = sr.ReadTV();
                if (sr.BitPosition + 17 != hs) return false;
            }
            if (desc.Length == 0 || desc.Length > 255) return false;

            long eedStart = -1;
            int eedLen = 0;
            try
            {
                var hr = new BitReader(src, 0);
                DwgObjectRecord.SeekTo(hr, start);
                hr.ReadHandle();
                long es = hr.BitPosition;
                if (SkipEedWriter(hr))
                {
                    int len = (int)(hr.BitPosition - es);
                    if (len > 2 && hr.BitPosition < body0) { eedStart = es; eedLen = len; }
                }
            }
            catch { eedStart = -1; }

            if (eedStart < 0)
                CopyBits(body, src, start, (int)(body0 - start));
            else
            {
                CopyBits(body, src, start, (int)(eedStart - start));
                if (!TryWriteEedPreR2007Range(body, src, eedStart, eedLen))
                    CopyBits(body, src, eedStart, eedLen);
                CopyBits(body, src, eedStart + eedLen,
                    (int)(body0 - eedStart - eedLen));
            }
            body.WriteTV(desc);
            CopyBits(body, src, body0, (int)(fieldsEnd - body0));
            body.WriteB(ReadSrcBit(src, fieldsEnd + 2));

            if (DebugTrace)
                System.Console.Error.WriteLine(
                    $"VSDN2118 h={rec.Handle:X} AC1021→AC1018 desc='{desc}' num=[{body0},{fieldsEnd})=499 " +
                    $"剥b45+串区{hs - fieldsEnd - 3}位 out={body.BitPosition}位");
            return true;
        }
        catch { return false; }
    }
}
