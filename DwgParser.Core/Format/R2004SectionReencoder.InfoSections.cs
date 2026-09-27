using DwgParser.Core.IO;
using DwgParser.Core.Objects;

namespace DwgParser.Core.Format;

internal static partial class R2004SectionReencoder
{

    private static string ReadT16(byte[] d, ref int pos, bool unicode)
    {
        if (pos + 2 > d.Length)
            throw new InvalidDataException("T16 长度字段越界");
        ushort n = BitConverter.ToUInt16(d, pos);
        pos += 2;
        if (unicode)
        {
            if (pos + (long)n * 2 > d.Length)
                throw new InvalidDataException($"T16 内容越界（n={n}）");
            var units = new char[n];
            for (int i = 0; i < n; i++) units[i] = (char)BitConverter.ToUInt16(d, pos + i * 2);
            pos += n * 2;
            var s = new string(units);
            return s.Length > 0 && s[^1] == '\0' ? s[..^1] : s;
        }
        if (pos + (long)n > d.Length)
            throw new InvalidDataException($"T16 内容越界（n={n}）");
        var bytes = new byte[n];
        Buffer.BlockCopy(d, pos, bytes, 0, n);
        pos += n;
        int len = n > 0 && bytes[n - 1] == 0 ? n - 1 : n;
        return System.Text.Encoding.Latin1.GetString(bytes, 0, len);
    }

    private static void WriteT16(BitWriter w, string value, bool unicode)
    {
        if (unicode)
        {
            w.WriteRS((ushort)(value.Length + 1));
            foreach (char c in value) w.WriteRS(c);
            w.WriteRS(0);
        }
        else
        {
            var bytes = System.Text.Encoding.Latin1.GetBytes(value);
            w.WriteRS((ushort)(bytes.Length + 1));
            foreach (var b in bytes) w.WriteRC(b);
            w.WriteRC(0);
        }
    }

    internal static void ReencodeSummaryInfo(
        List<R2004Writer.SectionEntry> entries, DwgVersion sourceVer, DwgVersion targetVer,
        Action<string>? diagnose = null)
    {
        if (sourceVer == targetVer) return;
        bool srcU = sourceVer >= DwgVersion.AC1021, dstU = targetVer >= DwgVersion.AC1021;
        if (srcU == dstU) return;
        var idx = FindEntry(entries, "AcDb:SummaryInfo");
        if (idx < 0) return;
        var d = entries[idx].Data;
        try
        {
            int p = 0;
            var strings = new string[8];
            for (int i = 0; i < 8; i++) strings[i] = ReadT16(d, ref p, srcU);
            int timersPos = p;
            p += 24;
            ushort numProps = BitConverter.ToUInt16(d, p); p += 2;
            var props = new List<(string Tag, string Value)>(numProps);
            for (int i = 0; i < numProps; i++)
            {
                string tag = ReadT16(d, ref p, srcU);
                string val = ReadT16(d, ref p, srcU);
                props.Add((tag, val));
            }
            int tailPos = p;
            p += 8;
            if (p != d.Length)
                throw new InvalidDataException($"SummaryInfo 解码未到节尾（余 {d.Length - p}B）");

            var w = new BitWriter(d.Length * 2 + 64);
            foreach (var s in strings) WriteT16(w, s, dstU);
            for (int i = 0; i < 24; i++) w.WriteRC(d[timersPos + i]);
            w.WriteRS(numProps);
            foreach (var (tag, val) in props) { WriteT16(w, tag, dstU); WriteT16(w, val, dstU); }
            for (int i = 0; i < 8; i++) w.WriteRC(d[tailPos + i]);
            byte[] nd = w.ToArray();
            entries[idx] = new R2004Writer.SectionEntry("AcDb:SummaryInfo", nd, entries[idx].TypeId);
            diagnose?.Invoke(
                $"版本转换：AcDb:SummaryInfo 已按 {targetVer.GetVersionName()} 重编码（{d.Length}→{nd.Length} 字节）。");
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or ArgumentOutOfRangeException or ArgumentException)
        {
            diagnose?.Invoke($"版本转换：AcDb:SummaryInfo 重编码失败，按原样搬运。({ex.Message})");
        }
    }

    internal static void ReencodeAppInfo(
        List<R2004Writer.SectionEntry> entries, DwgVersion sourceVer, DwgVersion targetVer,
        Action<string>? diagnose = null)
    {
        if (sourceVer == targetVer) return;
        bool srcU = sourceVer >= DwgVersion.AC1021, dstU = targetVer >= DwgVersion.AC1021;
        if (srcU == dstU) return;
        var idx = FindEntry(entries, "AcDb:AppInfo");
        if (idx < 0) return;
        var d = entries[idx].Data;
        try
        {
            int p = 0;
            string name, comment = "", product = "", version = "";
            int numStrings;
            if (srcU)
            {
                p += 4;
                name = ReadT16(d, ref p, true);
                numStrings = BitConverter.ToInt32(d, p); p += 4;
                p += 16;
                version = ReadT16(d, ref p, true);
                if (numStrings >= 2) { p += 16; comment = ReadT16(d, ref p, true); }
                if (numStrings >= 3) { p += 16; product = ReadT16(d, ref p, true); }
            }
            else
            {
                name = ReadT16(d, ref p, false);
                numStrings = BitConverter.ToInt32(d, p); p += 4;
                comment = ReadT16(d, ref p, false);
                product = ReadT16(d, ref p, false);
                version = ReadT16(d, ref p, false);
            }
            if (p != d.Length)
                throw new InvalidDataException($"AppInfo 解码未到节尾（余 {d.Length - p}B）");

            var w = new BitWriter(d.Length * 2 + 64);
            if (dstU)
            {
                w.WriteRL(2);
                WriteT16(w, name, true);
                w.WriteRL(3);
                for (int i = 0; i < 16; i++) w.WriteRC(0);
                WriteT16(w, version, true);
                for (int i = 0; i < 16; i++) w.WriteRC(0);
                WriteT16(w, comment, true);
                for (int i = 0; i < 16; i++) w.WriteRC(0);
                WriteT16(w, product, true);
            }
            else
            {
                WriteT16(w, name, false);
                w.WriteRL(numStrings);
                WriteT16(w, comment, false);
                WriteT16(w, product, false);
                WriteT16(w, version, false);
            }
            byte[] nd = w.ToArray();
            entries[idx] = new R2004Writer.SectionEntry("AcDb:AppInfo", nd, entries[idx].TypeId);
            diagnose?.Invoke(
                $"版本转换：AcDb:AppInfo 已按 {targetVer.GetVersionName()} 重编码（{d.Length}→{nd.Length} 字节）。");
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or ArgumentOutOfRangeException or ArgumentException)
        {
            diagnose?.Invoke($"版本转换：AcDb:AppInfo 重编码失败，按原样搬运。({ex.Message})");
        }
    }

    private static bool DropsDataTableClass(List<R2000Class> classes, DwgVersion sourceVer, DwgVersion targetVer)
        => !sourceVer.IsR2007Plus() && targetVer.IsR2007Plus()
           && classes.Any(c => string.Equals(c.DxfName, "DATATABLE", StringComparison.OrdinalIgnoreCase));

    private static int DropClassesByName(List<R2000Class> classes, string dxfName)
    {
        var hit = classes.Where(c => string.Equals(c.DxfName, dxfName, StringComparison.OrdinalIgnoreCase)).ToList();
        foreach (var c in hit) classes.Remove(c);
        return hit.Count;
    }

    private static bool NeedsPreR2007DataTableClass(
        List<R2000Class> classes, DwgVersion sourceVer, DwgVersion targetVer)
        => sourceVer.IsR2007Plus() && !targetVer.IsR2007Plus()
           && classes.Count > 0
           && !classes.Any(c => string.Equals(c.DxfName, "DATATABLE", StringComparison.OrdinalIgnoreCase));

    private static List<R2000Class> AppendPreR2007DataTableClass(List<R2000Class> classes)
    {
        var added = new List<R2000Class>(classes)
        {
            new R2000Class(499 + classes.Count + 1, 0, "ObjectDBX Classes", "AcDbDataTable",
                           "DATATABLE", false, 499,
                           NumInstances: 2, DwgVer: 23, MaintVer: 14),
        };
        return added;
    }

    private static int StripTailRemappedClasses(List<R2000Class> classes)
    {
        int stripped = 0;
        while (classes.Count > 0
               && ClassFixedCodes.TryGetValue(classes[^1].DxfName, out short code)
               && code > 0)
        {
            classes.RemoveAt(classes.Count - 1);
            stripped++;
        }
        return stripped;
    }

    internal static void ReencodeClasses(
        List<R2004Writer.SectionEntry> entries, DwgVersion sourceVer, DwgVersion targetVer,
        Action<string>? diagnose = null)
    {
        if (sourceVer == targetVer) return;
        bool srcR7 = sourceVer >= DwgVersion.AC1021;
        bool dstR7 = targetVer >= DwgVersion.AC1021;
        var idx = FindEntry(entries, "AcDb:Classes");
        if (idx < 0) return;
        var d = entries[idx].Data;

        bool pre4ToR4 = sourceVer < DwgVersion.AC1018 && targetVer == DwgVersion.AC1018;
        if (srcR7 != dstR7 || (targetVer == DwgVersion.AC1021 && sourceVer >= DwgVersion.AC1024)
            || pre4ToR4)
        {
            var parsed = R2000ClassTable.TryParseR2004Classes(d, sourceVer);
            if (parsed is not { Count: > 0 })
            {
                diagnose?.Invoke($"版本转换：AcDb:Classes 解析失败（{d.Length}B），按原样搬运。");
                return;
            }
            if (targetVer == DwgVersion.AC1018 && parsed.Any(c => c.ProxyFlag == -32768))
            {
                int zombied = parsed.Count(c => c.ProxyFlag == -32768);
                parsed = parsed.Select(c => c.ProxyFlag == -32768 ? c with { ProxyFlag = 0 } : c).ToList();
                diagnose?.Invoke($"版本转换：AcDb:Classes 归一 {zombied} 枚 0x8000 僵尸类声明（R2000 方言 → R2004 pf=0）。");
            }
            int stripped = sourceVer < DwgVersion.AC1018 ? StripTailRemappedClasses(parsed) : 0;
            int dtAdded = 0;
            if (NeedsPreR2007DataTableClass(parsed, sourceVer, targetVer))
            {
                parsed = AppendPreR2007DataTableClass(parsed);
                dtAdded = 1;
            }
            int dtDropped = 0;
            byte[] nd7 = dstR7
                ? R2000ClassTable.BuildR2007ClassesSection(parsed, extraRl: targetVer > DwgVersion.AC1021)
                : targetVer == DwgVersion.AC1018
                    ? R2000ClassTable.BuildR2004ClassesSection(parsed)
                    : R2000ClassTable.BuildClassesSection(parsed);
            entries[idx] = new R2004Writer.SectionEntry("AcDb:Classes", nd7, entries[idx].TypeId);
            diagnose?.Invoke(
                $"版本转换：AcDb:Classes 已按 {targetVer.GetVersionName()} 重编码 {parsed.Count} 类（{d.Length}→{nd7.Length} 字节）"
                + (stripped > 0 ? $"，尾部剔除 {stripped} 枚走固定码的类。" : ".")
                + (dtDropped > 0 ? $" 并剔除 {dtDropped} 枚 DATATABLE 类声明。" : "")
                + (dtAdded > 0 ? " 并补齐 pre-R2007 目标必需的 DATATABLE 类声明（§222 对象侧对偶）。" : ""));
            return;
        }

        bool srcNew = sourceVer >= DwgVersion.AC1024;
        bool dstNew = targetVer >= DwgVersion.AC1024;
        if (srcNew == dstNew) return;
        if (d.Length < 28) return;

        byte[] nd;
        if (!srcNew && dstNew)
        {
            nd = new byte[d.Length + 4];
            Buffer.BlockCopy(d, 0, nd, 0, 20);
            Buffer.BlockCopy(BitConverter.GetBytes(0u), 0, nd, 20, 4);
            Buffer.BlockCopy(d, 20, nd, 24, d.Length - 20);
            uint size = BitConverter.ToUInt32(nd, 16);
            Buffer.BlockCopy(BitConverter.GetBytes(size + 4), 0, nd, 16, 4);
        }
        else
        {
            nd = new byte[d.Length - 4];
            Buffer.BlockCopy(d, 0, nd, 0, 20);
            Buffer.BlockCopy(d, 24, nd, 20, d.Length - 24);
            uint size = BitConverter.ToUInt32(nd, 16);
            Buffer.BlockCopy(BitConverter.GetBytes(size - 4), 0, nd, 16, 4);
        }
        entries[idx] = new R2004Writer.SectionEntry("AcDb:Classes", nd, entries[idx].TypeId);
        diagnose?.Invoke(
            $"版本转换：AcDb:Classes hsize 字段已按 {targetVer.GetVersionName()} {(dstNew ? "插入" : "拔除")}（{d.Length}→{nd.Length} 字节）。");
    }

    internal static int FindEntry(List<R2004Writer.SectionEntry> entries, string name)
    {
        for (int i = 0; i < entries.Count; i++)
            if (string.Equals(entries[i].Name, name, StringComparison.OrdinalIgnoreCase))
                return i;
        return -1;
    }
}
