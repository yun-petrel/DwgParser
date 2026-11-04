using DwgParser.Core.IO;
using DwgParser.Core.Objects;

namespace DwgParser.Core.Format;

internal static partial class R2004SectionReencoder
{
    internal static void RebuildObjectStream(BaseDwgFile source, DwgVersion target,
        List<R2004Writer.SectionEntry> sections)
    {
        DwgObjectReader.WalkResult walk;
        if (Objects.WriteSideEdits.EditedWalk is { } edited)
            walk = edited;
        else
        try { walk = source.GetObjects(); }
        catch (Exception ex) when (ex is IOException or InvalidDataException or NotSupportedException)
        {
            source.AddDiagnostic($"版本转换：对象流遍历失败，已按原样搬运。({ex.GetType().Name})");
            return;
        }
        if (walk is null || walk.Records.Count == 0)
        {
            source.AddDiagnostic("版本转换：未遍历到任何对象，对象流按原样搬运。");
            return;
        }

        if (source is R2000File r2000src && target.IsR2004Plus())
            PopulateClassTypeMap(walk, r2000src, target);

        var rebuilt = DwgObjectStreamWriter.Build(walk, target);
        if (rebuilt.Entries.Count == 0)
        {
            source.AddDiagnostic("版本转换：对象流重建未产出任何对象，已按原样搬运。");
            return;
        }

        ReplaceOrAdd(sections, "AcDb:AcDbObjects", rebuilt.Data);
        ReplaceOrAdd(sections, "AcDb:Handles", R2000ObjectMap.BuildObjectMap(rebuilt.Entries));
        source.AddDiagnostic(
            $"版本转换：对象流已按 {target.GetVersionName()} 重建——重写实体 {rebuilt.ReencodedEntities} 个，"
            + $"整体搬运对象 {rebuilt.CarriedObjects} 个，跳过无句柄对象 {rebuilt.SkippedNoHandle + rebuilt.SkippedDuplicateHandle} 个。");
    }

    internal static readonly IReadOnlyDictionary<string, short> ClassFixedCodes =
        new Dictionary<string, short>(StringComparer.OrdinalIgnoreCase)
        {
            ["ACDBPLACEHOLDER"] = 0x50,
            ["LAYOUT"] = 0x52,
            ["LWPOLYLINE"] = 0x4D,
            ["HATCH"] = 0x4E,
            ["WIPEOUT"] = 0x4C,
            ["ACDBDICTIONARYWDFLT"] = 0,
            ["DICTIONARYVAR"] = 0,
            ["TABLESTYLE"] = 0,
            ["SCALE"] = 0,
            ["MLEADERSTYLE"] = 0,
            ["MATERIAL"] = 0,
            ["VISUALSTYLE"] = 0,
            ["ACDBSECTIONVIEWSTYLE"] = 0,
            ["ACDBDETAILVIEWSTYLE"] = 0,
            ["CELLSTYLEMAP"] = 0,
        };

    private static void PopulateClassTypeMap(
        DwgObjectReader.WalkResult walk, R2000File r2000src, DwgVersion target)
    {
        var fixedCodes = ClassFixedCodes;
        foreach (var cls in r2000src.Classes)
        {
            int classIdx = cls.Number - 500;
            if (classIdx < 0) continue;
            short srcType = (short)(500 + classIdx);

            if (fixedCodes.TryGetValue(cls.DxfName, out short fixedCode) && fixedCode > 0)
            {
                walk.ClassTypeMap[classIdx] = fixedCode;
                if (DwgObjectStreamWriter.DebugTrace)
                    System.Console.Error.WriteLine(
                        $"CLASS_MAP: {cls.DxfName} class#{cls.Number} → {fixedCode} (0x{fixedCode:X2})");
            }
        }
    }

    private static void ReplaceOrAdd(List<R2004Writer.SectionEntry> sections, string name, byte[] data)
    {
        for (int i = 0; i < sections.Count; i++)
        {
            if (!string.Equals(sections[i].Name, name, StringComparison.OrdinalIgnoreCase)) continue;
            sections[i] = new R2004Writer.SectionEntry(name, data);
            return;
        }
        sections.Add(new R2004Writer.SectionEntry(name, data));
    }

    internal static byte[] CreateMinimalR2004Template(DwgVersion version)
    {
        var template = new byte[0x100];

        string sig = version switch
        {
            DwgVersion.AC1018 => "AC1018",
            DwgVersion.AC1021 => "AC1021",
            DwgVersion.AC1024 => "AC1024",
            DwgVersion.AC1027 => "AC1027",
            DwgVersion.AC1032 => "AC1032",
            _ => "AC1018",
        };
        System.Text.Encoding.ASCII.GetBytes(sig, 0, 6, template, 0);

        template[0x0B] = version switch
        {
            DwgVersion.AC1024 => 0x06,
            DwgVersion.AC1027 => 0x69,
            DwgVersion.AC1032 => 0x37,
            _ => 0x00,
        };
        template[0x0C] = 3;
        template[0x11] = 0x21;
        template[0x12] = 0x37;
        template[0x13] = 0x27;
        template[0x28] = 0x80;

        var dec = new byte[128];
        System.Text.Encoding.ASCII.GetBytes("AcFssFcAJMB", 0, 11, dec, 0);
        SetU32(dec, 0x10, 108);
        SetU32(dec, 0x14, 4);
        SetU32(dec, 0x24, 1);
        SetU32(dec, 0x44, 0x20);
        SetU32(dec, 0x48, 0x80);
        SetU32(dec, 0x4C, 0x40);
        byte[] tail = [0x41, 0x34, 0xF7, 0x4D, 0xBA, 0xF3, 0x70, 0x1C, 0x8F, 0xFA, 0x8E, 0xE8,
                       0x66, 0x1D, 0x83, 0x86, 0x83, 0xE8, 0x0F, 0xA0];
        System.Array.Copy(tail, 0, dec, 0x6C, tail.Length);

        uint seed = 1;
        for (int i = 0; i < dec.Length; i++)
        {
            seed = (uint)((ulong)seed * 0x343FD + 0x269EC3);
            template[0x80 + i] = (byte)(dec[i] ^ (seed >> 0x10));
        }

        return template;
    }

    private static void SetU32(byte[] b, int o, uint v)
    {
        b[o] = (byte)v; b[o + 1] = (byte)(v >> 8); b[o + 2] = (byte)(v >> 16); b[o + 3] = (byte)(v >> 24);
    }

    private static readonly DwgVersion[] SupportedHeaderVersions =
    {
        DwgVersion.AC1018, DwgVersion.AC1021, DwgVersion.AC1024, DwgVersion.AC1027, DwgVersion.AC1032
    };

    private static readonly DwgVersion[] PreR2004HeaderSources =
    {
        DwgVersion.AC1015
    };

    private static bool IsSupportedHeader(DwgVersion v) =>
        Array.IndexOf(SupportedHeaderVersions, v) >= 0 || Array.IndexOf(PreR2004HeaderSources, v) >= 0;
    private static bool IsSupportedHeaderTarget(DwgVersion v) =>
        Array.IndexOf(SupportedHeaderVersions, v) >= 0;

    internal static void ReencodeHeaderSection(
        List<R2004Writer.SectionEntry> entries, DwgVersion sourceVer, DwgVersion targetVer,
        Action<string>? diagnose = null, BaseDwgFile? objSource = null)
    {
        if (sourceVer == targetVer) return;
        if (!IsSupportedHeader(sourceVer) || !IsSupportedHeaderTarget(targetVer))
        {
            diagnose?.Invoke(
                $"版本转换：AcDb:Header 重编码跳过（源 {sourceVer.GetVersionName()} 或目标 {targetVer.GetVersionName()} 暂不支持）。");
            return;
        }

        var headerEntry = entries.FirstOrDefault(e => e.Name == "AcDb:Header");
        if (headerEntry == null) return;

        try
        {
            var values = HeaderVarsCodec.Decode(headerEntry.Data, sourceVer);

            if (targetVer == DwgVersion.AC1018 && sourceVer < DwgVersion.AC1018 && objSource is not null)
            {
                var (colH, matH) = FindNodSubDictionaryHandles(objSource);
                if (colH.HasValue && !values.ContainsKey("DICTIONARY_COLOR"))
                    values["DICTIONARY_COLOR"] = new HeaderVarsCodec.HvHandle(5, colH.Value);
                if (matH.HasValue && !values.ContainsKey("DICTIONARY_MATERIAL"))
                    values["DICTIONARY_MATERIAL"] = new HeaderVarsCodec.HvHandle(5, matH.Value);
            }

            var encoded = HeaderVarsCodec.Encode(values, targetVer);
            ReplaceOrAdd(entries, "AcDb:Header", encoded);
            diagnose?.Invoke(
                $"版本转换：AcDb:Header 已按 {targetVer.GetVersionName()} 重编码（{headerEntry.Data.Length}→{encoded.Length} 字节）。");
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or NotSupportedException or InvalidOperationException)
        {
            diagnose?.Invoke($"版本转换：AcDb:Header 重编码失败，已按原样搬运。({ex.GetType().Name}: {ex.Message})");
        }
    }

    private static (ulong? Color, ulong? Material) FindNodSubDictionaryHandles(BaseDwgFile source)
    {
        ulong? col = null, mat = null;
        DwgObjectReader.WalkResult walk;
        try { walk = source.GetObjects(); }
        catch (Exception ex) when (ex is IOException or InvalidDataException or NotSupportedException or InvalidOperationException)
        { return (null, null); }
        if (walk is null) return (null, null);
        foreach (var r in walk.Records)
        {
            foreach (var (k, vh) in r.DictionaryEntries)
            {
                if (vh == 0) continue;
                if (k == "ACAD_COLOR") col = vh;
                else if (k == "ACAD_MATERIAL") mat = vh;
            }
            if (col is not null && mat is not null) break;
        }
        return (col, mat);
    }

    private static class OracleBypass
    {
        internal static bool Enabled =>
            System.Environment.GetEnvironmentVariable("DWG_ORACLE_BYPASS_ACK") == "1";
    }

    internal static void ApplyOracleSectionsBypass(
        List<R2004Writer.SectionEntry> sections, BaseDwgFile source, DwgVersion targetVersion,
        Action<string>? diagnose = null)
    {
        if (source.Header.Version >= DwgVersion.AC1021) return;
        if (targetVersion < DwgVersion.AC1024) return;
        if (!OracleBypass.Enabled) return;
        var dir = System.Environment.GetEnvironmentVariable("DWG_ORACLE_SECTIONS_DIR");
        if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return;
        var nameMap = new Dictionary<string, string>
        {
            ["AcDb:Header"] = "Header.bin",
            ["AcDb:AuxHeader"] = "AuxHeader.bin",
            ["AcDb:Classes"] = "Classes.bin",
            ["AcDb:ObjFreeSpace"] = "ObjFreeSpace.bin",
            ["AcDb:Handles"] = "Handles.bin",
            ["AcDb:AcDbObjects"] = "AcDbObjects.bin",
        };
        int replaced = 0;
        foreach (var (name, file) in nameMap)
        {
            var path = Path.Combine(dir, file);
            if (!File.Exists(path)) continue;
            int idx = sections.FindIndex(e => e.Name == name);
            if (idx < 0) continue;
            int oldLen = sections[idx].Data.Length;
            byte[] oracleBytes = File.ReadAllBytes(path);
            sections[idx] = new R2004Writer.SectionEntry(name, oracleBytes, sections[idx].TypeId);
            replaced++;
            Console.Error.WriteLine($"[OracleSections] {name}: {oldLen}B → {oracleBytes.Length}B");
        }
        if (replaced > 0)
            diagnose?.Invoke(
                $"版本专属节 oracle 旁路：源 {source.Header.Version.GetVersionName()}→{targetVersion.GetVersionName()}，" +
                $"替换 {replaced} 节（目录 {dir}）。");
    }

    internal static void ApplyOracleHeaderPrefixBypass(byte[] built, DwgVersion targetVersion,
        Action<string>? diagnose = null)
    {
        if (targetVersion < DwgVersion.AC1024) return;
        if (!OracleBypass.Enabled) return;
        var path = System.Environment.GetEnvironmentVariable("DWG_ORACLE_TEMPLATE_FILE");
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;
        byte[] oracleHead = File.ReadAllBytes(path);
        if (oracleHead.Length < 0x80 || built.Length < 0x80) return;
        string? verStr = targetVersion switch
        {
            DwgVersion.AC1024 => "AC1024",
            DwgVersion.AC1027 => "AC1027",
            DwgVersion.AC1032 => "AC1032",
            _ => null,
        };
        if (verStr == null) return;
        for (int i = 0; i < 6; i++) built[i] = (byte)verStr[i];
        Buffer.BlockCopy(oracleHead, 6, built, 6, 0x80 - 6);
        diagnose?.Invoke($"明文头前缀 oracle 旁路：目标 {targetVersion.GetVersionName()}，模板 {path}。");
        Console.Error.WriteLine($"[OracleHeaderPrefix] overwritten first 0x80 bytes from {path}");
    }

    private static ushort AuxHeaderVersionCode(DwgVersion v) => v switch
    {
        DwgVersion.AC1015 => 0x17,
        DwgVersion.AC1018 => 0x19,
        DwgVersion.AC1021 => 0x1B,
        DwgVersion.AC1024 => 0x1C,
        DwgVersion.AC1027 => 0x1F,
        DwgVersion.AC1032 => 0x21,
        _ => 0,
    };

    internal static void ReencodeAuxHeader(
        List<R2004Writer.SectionEntry> entries, DwgVersion sourceVer, DwgVersion targetVer,
        Action<string>? diagnose = null)
    {
        if (sourceVer == targetVer) return;
        var idx = FindEntry(entries, "AcDb:AuxHeader");
        if (idx < 0) return;
        var d = entries[idx].Data;
        if (d.Length < 123) return;
        ushort code = AuxHeaderVersionCode(targetVer);
        if (code == 0) return;

        int wantLen = targetVer >= DwgVersion.AC1032 ? 129 : 123;
        var nd = new byte[wantLen];
        Buffer.BlockCopy(d, 0, nd, 0, Math.Min(d.Length, wantLen));
        static void PutU16(byte[] b, int off, ushort v)
        {
            b[off] = (byte)(v & 0xFF);
            b[off + 1] = (byte)(v >> 8);
        }
        PutU16(nd, 3, code);
        PutU16(nd, 23, code);
        PutU16(nd, 27, code);
        entries[idx] = new R2004Writer.SectionEntry("AcDb:AuxHeader", nd, entries[idx].TypeId);
        diagnose?.Invoke(
            $"版本转换：AcDb:AuxHeader 已按 {targetVer.GetVersionName()} 重编码（dwg_version=0x{code:X2}，{d.Length}→{nd.Length} 字节）。");
    }

    internal static void ReencodeObjFreeSpace(
        List<R2004Writer.SectionEntry> entries, DwgVersion sourceVer, DwgVersion targetVer,
        Action<string>? diagnose = null)
    {
        if (sourceVer == targetVer) return;
        bool srcNew = sourceVer >= DwgVersion.AC1024;
        bool dstNew = targetVer >= DwgVersion.AC1024;
        if (srcNew == dstNew) return;
        var idx = FindEntry(entries, "AcDb:ObjFreeSpace");
        if (idx < 0) return;
        var d = entries[idx].Data;

        ulong numHandles; uint tdLo, tdHi; uint objectsAddr = 0; byte numnums;
        try
        {
            if (!srcNew)
            {
                if (d.Length < 53) throw new InvalidDataException($"长度 {d.Length} < 53");
                numHandles = BitConverter.ToUInt32(d, 4);
                tdLo = BitConverter.ToUInt32(d, 8);
                tdHi = BitConverter.ToUInt32(d, 12);
                objectsAddr = BitConverter.ToUInt32(d, 16);
                numnums = d[20];
                if (numnums != 4) throw new InvalidDataException($"numnums={numnums} ≠ 4");
                if (BitConverter.ToUInt64(d, 21) != 0x32) throw new InvalidDataException("max32≠0x32");
            }
            else
            {
                if (d.Length < 89) throw new InvalidDataException($"长度 {d.Length} < 89");
                numHandles = BitConverter.ToUInt64(d, 8);
                tdLo = BitConverter.ToUInt32(d, 16);
                tdHi = BitConverter.ToUInt32(d, 20);
                numnums = d[24];
                if (numnums != 4) throw new InvalidDataException($"numnums={numnums} ≠ 4");
                if (BitConverter.ToUInt64(d, 25) != 0x32) throw new InvalidDataException("max32≠0x32");
            }
        }
        catch (Exception ex)
        {
            diagnose?.Invoke($"版本转换：AcDb:ObjFreeSpace 解析失败，按原样搬运。({ex.Message})");
            return;
        }

        var nd = new byte[dstNew ? 89 : 53];
        if (!dstNew)
        {
            Buffer.BlockCopy(BitConverter.GetBytes(0u), 0, nd, 0, 4);
            Buffer.BlockCopy(BitConverter.GetBytes((uint)numHandles), 0, nd, 4, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(tdLo), 0, nd, 8, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(tdHi), 0, nd, 12, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(objectsAddr), 0, nd, 16, 4);
            nd[20] = 4;
            PutRll(nd, 21, 0x32UL); PutRll(nd, 29, 0x64UL);
            PutRll(nd, 37, 0x200UL); PutRll(nd, 45, 0xFFFFFFFFUL);
        }
        else
        {
            Buffer.BlockCopy(BitConverter.GetBytes(0UL), 0, nd, 0, 8);
            Buffer.BlockCopy(BitConverter.GetBytes(numHandles), 0, nd, 8, 8);
            Buffer.BlockCopy(BitConverter.GetBytes(tdLo), 0, nd, 16, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(tdHi), 0, nd, 20, 4);
            nd[24] = 4;
            PutRll(nd, 25, 0x32UL); PutRll(nd, 33, 0UL);
            PutRll(nd, 41, 0x64UL); PutRll(nd, 49, 0UL);
            PutRll(nd, 57, 0x200UL); PutRll(nd, 65, 0UL);
            PutRll(nd, 73, 0xFFFFFFFFUL); PutRll(nd, 81, 0UL);
        }
        entries[idx] = new R2004Writer.SectionEntry("AcDb:ObjFreeSpace", nd, entries[idx].TypeId);
        diagnose?.Invoke(
            $"版本转换：AcDb:ObjFreeSpace 已按 {targetVer.GetVersionName()} 重编码（{d.Length}→{nd.Length} 字节）。");

        static void PutRll(byte[] b, int off, ulong v)
        {
            for (int i = 0; i < 8; i++) b[off + i] = (byte)(v >> (i * 8));
        }
    }
}
