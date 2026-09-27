using DwgParser.Core.IO;

namespace DwgParser.Core.Objects;

public static partial class DwgObjectStreamWriter
{
    private static bool NeedsRelativeHandleNormalization(DwgObjectRecord rec, Target t)
        => rec.SourceVersion >= DwgVersion.AC1024 && t.Version < DwgVersion.AC1021
           && System.Environment.GetEnvironmentVariable("RELREF_OFF") != "1";

    private static bool TryNormalizeRelativeHandleRefs(
        BitWriter handles, byte[] src, long from, long to, ulong ownHandle)
    {
        if (from < 0 || to <= from || (to - from) < 8) return false;
        var br = new BitReader(src);
        long p = from;
        bool anyRelative = false;
        int seen = 0;
        while (p + 8 <= to)
        {
            DwgObjectRecord.SeekTo(br, p);
            int cc = br.ReadRC();
            int code = cc >> 4;
            int size = cc & 0xF;
            long nb = 8 + size * 8L;
            if (p + nb > to)
            {
                if (to - p < 8) break;
                return false;
            }
            if (code is 6 or 8 or 10 or 12 or 14) anyRelative = true;
            if (code > 5 && code is not (6 or 8 or 10 or 12 or 14)) return false;
            p += nb;
            seen++;
        }
        if (!anyRelative || seen == 0) return false;

        p = from;
        while (p + 8 <= to)
        {
            DwgObjectRecord.SeekTo(br, p);
            int cc = br.ReadRC();
            int code = cc >> 4;
            int size = cc & 0xF;
            long nb = 8 + size * 8L;
            if (p + nb > to)
            {
                if (to - p < 8) { CopyBits(handles, src, p, (int)(to - p)); break; }
                return false;
            }
            ulong raw = 0;
            for (int i = 0; i < size; i++) raw = (raw << 8) | br.ReadRC();
            if (code is 6 or 8 or 10 or 12 or 14)
            {
                ulong abs = new DwgHandleReference(code, raw).Resolve(ownHandle);
                handles.WriteHandleRef(4, abs);
            }
            else
            {
                handles.WriteHandleRef(code, raw);
            }
            p += nb;
        }
        return true;
    }
}
