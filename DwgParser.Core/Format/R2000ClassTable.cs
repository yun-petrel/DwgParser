using DwgParser.Core.IO;

namespace DwgParser.Core.Format;

public static class R2000ClassTable
{
    internal static readonly byte[] ClassesBeginSentinel =
    {
        0x8D, 0xA1, 0xC4, 0xB8, 0xC4, 0xA9, 0xF8, 0xC5,
        0xC0, 0xDC, 0xF4, 0x5F, 0xE7, 0xCF, 0xB6, 0x8A,
    };

    internal static readonly byte[] ClassesEndSentinel =
    {
        0x72, 0x5E, 0x3B, 0x47, 0x3B, 0x56, 0x07, 0x3A,
        0x3F, 0x23, 0x0B, 0xA0, 0x18, 0x30, 0x49, 0x75,
    };

    internal static List<R2000Class>? TryParseR2004Classes(byte[] data, DwgVersion sourceVersion)
    {
        if (sourceVersion < DwgVersion.AC1018)
            return TryParseR2000Classes(data);
        if (sourceVersion == DwgVersion.AC1018)
            return TryParseR2004ClassesInline(data, versionsAsBl: false)
                ?? TryParseR2004ClassesInline(data, versionsAsBl: true);
        return TryParseR2007Classes(data, extraRl: false)
            ?? TryParseR2007Classes(data, extraRl: true);
    }

    internal static List<R2000Class>? TryParseR2000Classes(byte[] data)
    {
        try
        {
            int contentOff = -1;
            uint contentLen = 0;
            if (data.Length >= 38 && data.AsSpan(0, 16).SequenceEqual(ClassesBeginSentinel))
            {
                uint v = BitConverter.ToUInt32(data, 16);
                if (v > 0 && 20L + v + 18 <= data.Length)
                {
                    contentOff = 20;
                    contentLen = v;
                }
            }
            if (contentOff < 0 && data.Length >= 22)
            {
                uint v = BitConverter.ToUInt32(data, 0);
                if (v > 0 && 4L + v + 2 <= data.Length)
                {
                    contentOff = 4;
                    contentLen = v;
                }
            }
            if (contentOff < 0)
                return null;

            int endPos = contentOff + (int)contentLen;
            var br = new BitReader(data, contentOff);
            var result = new List<R2000Class>();
            while (br.BytePosition < endPos - 1)
            {
                int number = br.ReadBS();
                int proxyflag = br.ReadBS();
                string appname = br.ReadTV();
                string cppname = br.ReadTV();
                string dxfname = br.ReadTV();
                bool zombie = br.ReadB();
                int itemClassId = br.ReadBS();
                if (number == 0 && dxfname.Length == 0)
                    break;
                if (number != 500 + result.Count)
                    return null;
                result.Add(new R2000Class(number, proxyflag, appname, cppname, dxfname, zombie, itemClassId));
            }
            return result.Count > 0 ? result : null;
        }
        catch (Exception ex) when (ex is EndOfStreamException or IndexOutOfRangeException
                                     or ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    private static List<R2000Class>? TryParseR2004ClassesInline(byte[] data, bool versionsAsBl)
    {
        try
        {
            if (data.Length < 38 || !data.AsSpan(0, 16).SequenceEqual(ClassesBeginSentinel))
                return null;
            uint recLen = BitConverter.ToUInt32(data, 16);
            int contentEnd = 20 + (int)recLen;
            if (recLen == 0 || contentEnd + 18 > data.Length)
                return null;

            var br = new BitReader(data, 20);
            int maxNum = br.ReadBS();
            if (maxNum < 500)
                return null;
            int numClasses = maxNum - 499;
            br.ReadRS();
            if (!br.ReadB())
                return null;

            var result = new List<R2000Class>(numClasses);
            for (int i = 0; i < numClasses; i++)
            {
                int number = br.ReadBS();
                if (number == 0)
                    break;
                if (number != 500 + i)
                    return null;
                int proxyflag = br.ReadBS();
                string appname = br.ReadTV();
                string cppname = br.ReadTV();
                string dxfname = br.ReadTV();
                bool zombie = br.ReadB();
                int itemClassId = br.ReadBS();
                int numInstances = br.ReadBL();
                int dwgVer = versionsAsBl ? br.ReadBL() : br.ReadBS();
                int maintVer = versionsAsBl ? br.ReadBL() : br.ReadBS();
                int unknown1 = br.ReadBL();
                int unknown2 = br.ReadBL();
                result.Add(new R2000Class(number, proxyflag, appname, cppname, dxfname, zombie, itemClassId,
                    numInstances, dwgVer, maintVer, unknown1, unknown2));
            }
            return result;
        }
        catch (Exception ex) when (ex is EndOfStreamException or IndexOutOfRangeException
                                     or ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    internal static List<R2000Class>? TryParseR2007Classes(byte[] data, bool extraRl)
    {
        try
        {
            if (data.Length < 42 || !data.AsSpan(0, 16).SequenceEqual(ClassesBeginSentinel))
                return null;
            uint recLen = BitConverter.ToUInt32(data, 16);
            if (recLen == 0 || 20 + (long)recLen > data.Length)
                return null;

            var br = new BitReader(data, 20);
            if (extraRl)
                br.ReadRL();
            uint bitsize = br.ReadRL();
            int maxNum = br.ReadBS();
            if (maxNum is < 500 or > 5000)
                return null;
            int numClasses = maxNum - 499;
            br.ReadRC();
            br.ReadRC();
            if (!br.ReadB())
                return null;

            int endbitOffset = extraRl ? 191 : 159;
            long totalBits = (long)data.Length * 8;
            long endPos = (long)bitsize + endbitOffset;
            if (endPos < 0 || endPos + 1 > totalBits)
                return null;
            var str = new BitReader(data) { UnicodeStrings = true };
            str.SetPosition((int)(endPos >> 3), (int)(endPos & 7));
            if (!str.ReadB())
                return null;
            long startPos = endPos - 16;
            str.SetPosition((int)(startPos >> 3), (int)(startPos & 7));
            uint dataSize = str.ReadRS();
            if ((dataSize & 0x8000) != 0)
            {
                startPos -= 16;
                str.SetPosition((int)(startPos >> 3), (int)(startPos & 7));
                dataSize = (dataSize & 0x7FFF) | ((uint)str.ReadRS() << 15);
            }
            startPos -= dataSize;
            if (startPos < 0 || startPos >= totalBits)
                return null;
            str.SetPosition((int)(startPos >> 3), (int)(startPos & 7));

            var result = new List<R2000Class>(numClasses);
            for (int i = 0; i < numClasses; i++)
            {
                int number = br.ReadBS();
                if (number == 0)
                    break;
                if (number != 500 + i)
                    return null;
                int proxyflag = br.ReadBS();
                string appname = str.ReadTV();
                string cppname = str.ReadTV();
                string dxfname = str.ReadTV();
                bool zombie = br.ReadB();
                int itemClassId = br.ReadBS();
                int numInstances = br.ReadBL();
                int dwgVer = br.ReadBL();
                int maintVer = br.ReadBL();
                int unknown1 = br.ReadBL();
                int unknown2 = br.ReadBL();
                result.Add(new R2000Class(number, proxyflag, appname, cppname, dxfname, zombie, itemClassId,
                    numInstances, dwgVer, maintVer, unknown1, unknown2));
            }
            if (result.Count == 0 || string.IsNullOrWhiteSpace(result[0].DxfName))
                return null;
            return result;
        }
        catch (Exception ex) when (ex is EndOfStreamException or IndexOutOfRangeException
                                     or ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    internal static byte[] BuildClassesSection(IReadOnlyList<R2000Class> classes)
    {
        var body = new BitWriter(classes.Count * 48 + 64);
        foreach (var c in classes)
        {
            body.WriteBS((short)c.Number);
            body.WriteBS((short)c.ProxyFlag);
            body.WriteTV(c.AppName);
            body.WriteTV(c.CppName);
            body.WriteTV(c.DxfName);
            body.WriteB(c.IsZombie);
            body.WriteBS((short)c.ItemClassId);
        }
        body.AlignByte();
        byte[] recs = body.ToArray();

        var w = new BitWriter(38 + recs.Length);
        foreach (var b in ClassesBeginSentinel) w.WriteRC(b);
        w.WriteRL((uint)recs.Length);
        foreach (var b in recs) w.WriteRC(b);
        byte[] buf = w.ToArray();
        ushort crc = Crc.Compute16Dwg(buf, 16, 4 + recs.Length);
        w.WriteRS(crc);
        foreach (var b in ClassesEndSentinel) w.WriteRC(b);
        return w.ToArray();
    }

    internal static byte[] BuildR2004ClassesSection(IReadOnlyList<R2000Class> classes)
    {
        var body = new BitWriter(classes.Count * 72 + 64);
        body.WriteBS((short)(499 + classes.Count));
        body.WriteRS(0);
        body.WriteB(true);
        foreach (var c in classes)
        {
            body.WriteBS((short)c.Number);
            body.WriteBS((short)c.ProxyFlag);
            body.WriteTV(c.AppName);
            body.WriteTV(c.CppName);
            body.WriteTV(c.DxfName);
            body.WriteB(c.IsZombie);
            body.WriteBS((short)c.ItemClassId);
            body.WriteBL(c.NumInstances);
            body.WriteBL(c.DwgVer);
            body.WriteBL(c.MaintVer);
            body.WriteBL(c.Unknown1);
            body.WriteBL(c.Unknown2);
        }
        body.AlignByte();
        byte[] recs = body.ToArray();

        var w = new BitWriter(38 + recs.Length);
        foreach (var b in ClassesBeginSentinel) w.WriteRC(b);
        w.WriteRL((uint)recs.Length);
        foreach (var b in recs) w.WriteRC(b);
        byte[] buf = w.ToArray();
        ushort crc = Crc.Compute16Dwg(buf, 16, 4 + recs.Length);
        w.WriteRS(crc);
        foreach (var b in ClassesEndSentinel) w.WriteRC(b);
        for (int i = 0; i < 8; i++) w.WriteRC(0);
        return w.ToArray();
    }

    internal static byte[] BuildR2007ClassesSection(IReadOnlyList<R2000Class> classes, bool extraRl = false)
    {
        var main = new BitWriter(classes.Count * 64 + 64);
        var str = new BitWriter(classes.Count * 96 + 64);
        WriteR2007ClassStreams(main, str, classes);
        long mainBits = main.BitPosition, strBits = str.BitPosition;

        long headBits = 18 + 17;
        long sizeBits = SizeFieldBits(strBits);
        long dataBits = headBits + mainBits + strBits + sizeBits + 1;
        long dataBytes = (dataBits + 7) / 8;
        long size = 4 + dataBytes;
        long bitsize = dataBits + 32;

        var main2 = new BitWriter(classes.Count * 64 + 64);
        var str2 = new BitWriter(classes.Count * 96 + 64);
        WriteR2007ClassStreams(main2, str2, classes);

        var w = new BitWriter((int)(40 + dataBytes + 40));
        foreach (var b in ClassesBeginSentinel) w.WriteRC(b);
        w.WriteRL((uint)size);
        if (extraRl) w.WriteRL(0);
        w.WriteRL((uint)bitsize);
        w.WriteBS(unchecked((short)(499 + classes.Count)));
        w.WriteRC(0); w.WriteRC(0); w.WriteB(true);
        w.WriteBitsFrom(main2, main2.BitPosition);
        w.WriteBitsFrom(str2, str2.BitPosition);
        WriteSizeField(w, strBits);
        w.WriteB(true);
        w.AlignByte();
        byte[] partial = w.ToArray();
        int crcLen = (int)(w.BitPosition / 8);
        ushort crc = Crc.Compute16Dwg(partial, 16, crcLen - 16);
        w.WriteRS(crc);
        foreach (var b in ClassesEndSentinel) w.WriteRC(b);
        for (int i = 0; i < 8; i++) w.WriteRC(0);
        return w.ToArray();
    }

    private static void WriteR2007ClassStreams(BitWriter main, BitWriter str, IReadOnlyList<R2000Class> classes)
    {
        foreach (var c in classes)
        {
            main.WriteBS((short)c.Number);
            main.WriteBS((short)c.ProxyFlag);
            main.WriteB(c.IsZombie);
            main.WriteBS((short)c.ItemClassId);
            main.WriteBL(c.NumInstances);
            main.WriteBL(c.DwgVer);
            main.WriteBL(c.MaintVer);
            main.WriteBL(c.Unknown1);
            main.WriteBL(c.Unknown2);

            WriteTuNoNul(str, c.AppName);
            WriteTuNoNul(str, c.CppName);
            WriteTuNoNul(str, c.DxfName);
        }
    }

    internal static long SizeFieldBits(long strBits) => strBits >= 0x8000 ? 32 : 16;

    internal static void WriteSizeField(BitWriter w, long strBits)
    {
        if (strBits < 0x8000)
        {
            w.WriteRS(unchecked((ushort)strBits));
            return;
        }
        w.WriteRS(unchecked((ushort)(strBits >> 15)));
        w.WriteRS(unchecked((ushort)((strBits & 0x7FFF) | 0x8000)));
    }

    private static void WriteTuNoNul(BitWriter w, string? value)
    {
        if (string.IsNullOrEmpty(value)) { w.WriteBS(0); return; }
        w.WriteBS(unchecked((short)value.Length));
        foreach (char c in value) w.WriteRS(c);
    }
}
