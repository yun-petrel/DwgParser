using System.Text;
using DwgParser.Core.IO;
using DwgParser.Core.Objects;

namespace DwgParser.Core.Format;

internal static class R2000DowngradeBuilder
{
    private static readonly byte[] SecondHeaderBeginSentinel =
    {
        0xD4, 0x7B, 0x21, 0xCE, 0x28, 0x93, 0x9F, 0xBF,
        0x53, 0x24, 0x40, 0x09, 0x12, 0x3C, 0xAA, 0x01,
    };

    private static readonly byte[] SecondHeaderEndSentinel =
    {
        0x2B, 0x84, 0xDE, 0x31, 0xD7, 0x6C, 0x60, 0x40,
        0xAC, 0xDB, 0xBF, 0xF6, 0xED, 0xC3, 0x55, 0xFE,
    };

    internal static readonly byte[] HeaderBeginSentinel =
    {
        0xCF, 0x7B, 0x1F, 0x23, 0xFD, 0xDE, 0x38, 0xA9,
        0x5F, 0x7C, 0x68, 0xB8, 0x4E, 0x6D, 0x33, 0x5F,
    };

    internal static readonly byte[] HeaderEndSentinel =
    {
        0x30, 0x84, 0xE0, 0xDC, 0x02, 0x21, 0xC7, 0x56,
        0xA0, 0x83, 0x97, 0x47, 0xB1, 0x92, 0xCC, 0xA0,
    };

    internal static byte[] BuildFromSource(IDwgFile source, DwgVersion targetVersion)
    {
        if (!targetVersion.IsR2000())
            throw new ArgumentException(
                $"目标版本 {targetVersion.GetVersionName()} 不是 R2000（AC1015）。", nameof(targetVersion));
        return source switch
        {
            R2000File src => BuildSameFamily(src, targetVersion),
            R2004File r2004 => BuildFromR2004Source(r2004, targetVersion),
            _ => throw new NotSupportedException(
                $"不支持从 {source.Header.Version.GetVersionName()} 转换：需要 R2000 或 R2004+ 源文件。"),
        };
    }

    private static byte[] BuildSameFamily(R2000File source, DwgVersion targetVersion)
    {
        var sections = new List<R2000Writer.SectionEntry>();
        foreach (var rec in source.Records)
        {
            if (rec.Name == "Handles") continue;
            if (rec.Name == "Objects") continue;
            int len = (int)Math.Min(rec.Size, (uint)Math.Max(0, source.RawFile.Length - (int)rec.Address));
            var data = source.RawFile.AsSpan((int)rec.Address, len).ToArray();
            sections.Add(new R2000Writer.SectionEntry(rec.Name, data) { RecordNumber = rec.Number });
        }
        NormalizeSections(sections);
        ReencodeHeaderSameFamily(sections, source, targetVersion);
        return AppendRebuiltObjects(sections, source, targetVersion);
    }

    private static void ReencodeHeaderSameFamily(List<R2000Writer.SectionEntry> sections,
        R2000File source, DwgVersion targetVersion)
    {
        var sourceVer = source.Header.Version;
        if (sourceVer == targetVersion) return;
        int idx = sections.FindIndex(s => s.Name == "Header");
        if (idx < 0 || sections[idx].Data.Length < 32) return;
        try
        {
            var values = HeaderVarsCodec.Decode(sections[idx].Data, sourceVer);
            sections[idx] = new R2000Writer.SectionEntry("Header", HeaderVarsCodec.Encode(values, targetVersion))
            {
                RecordNumber = sections[idx].RecordNumber,
            };
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException
                                   or NotSupportedException or InvalidOperationException)
        {
            source.AddDiagnostic($"版本转换：Header 节重编码失败，按源版本原样搬运（{ex.Message}）。");
        }
    }

    private static byte[] ReencodeHeaderVarsForTarget(byte[] framed, DwgVersion sourceVer, DwgVersion targetVer,
        ulong? vxControlHandle = null)
    {
        if (framed.Length < 32 || sourceVer == targetVer) return framed;
        try
        {
            var values = HeaderVarsCodec.Decode(framed, sourceVer);
            if (vxControlHandle is ulong vx)
            {
                values["VX_TABLE_RECORD"] = new HeaderVarsCodec.HvHandle(5, 0);
                values["VX_CONTROL_OBJECT"] = new HeaderVarsCodec.HvHandle(3, vx);
                if (values.TryGetValue("HANDSEED", out var hsObj)
                    && hsObj is HeaderVarsCodec.HvHandle hs && hs.Value <= vx)
                    values["HANDSEED"] = new HeaderVarsCodec.HvHandle(hs.Code, vx + 1);
            }
            if (targetVer == DwgVersion.AC1015)
                foreach (var k in new[] { "unknown_54", "unknown_55", "unknown_56", "unknown_57" })
                    values.Remove(k);
            return HeaderVarsCodec.Encode(values, targetVer);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException
                                   or NotSupportedException or InvalidOperationException)
        {
            return framed;
        }
    }

    private static void NormalizeSections(List<R2000Writer.SectionEntry> sections)
    {
        byte[]? aux = null;
        foreach (var s in sections)
            if (s.Name == "AuxHeader" && s.Data.Length > 0) { aux = s.Data; break; }
        aux ??= R2000Writer.BuildDefaultAuxHeader();

        var byName = new Dictionary<string, byte[]>();
        foreach (var s in sections)
            if (!byName.ContainsKey(s.Name)) byName[s.Name] = s.Data;

        sections.Clear();
        sections.Add(new R2000Writer.SectionEntry("AuxHeader", aux) { RecordNumber = 5 });
        foreach (var (name, num) in new[]
                 {
                     ("Header", 0), ("Classes", 1), ("ObjFreeSpace", 3), ("Template", 4),
                 })
        {
            byName.TryGetValue(name, out var data);
            sections.Add(new R2000Writer.SectionEntry(name, data ?? Array.Empty<byte>())
            {
                RecordNumber = num,
            });
        }
    }

    private static byte[] AppendRebuiltObjects(List<R2000Writer.SectionEntry> sections,
        BaseDwgFile source, DwgVersion targetVersion, ulong? vxHandle = null)
    {
        byte[]? objectData = null;
        List<(ulong Handle, long Offset)>? entries = null;
        var walk = SafeGetObjects(source);
        if (walk is { Records.Count: > 0 })
        {
            PopulateDowngradeClassTypeMap(walk);
            var rebuilt = DwgObjectStreamWriter.Build(walk, targetVersion);
            if (rebuilt.Entries.Count > 0)
            {
                objectData = rebuilt.Data;
                entries = rebuilt.Entries;
                source.AddDiagnostic(
                    $"版本转换：对象流已按 {targetVersion.GetVersionName()} 重建——"
                    + $"重写实体 {rebuilt.ReencodedEntities} 个，整体搬运对象 {rebuilt.CarriedObjects} 个。");
            }
        }
        if (vxHandle is ulong vxh && objectData is not null && entries is not null
            && targetVersion.IsR2000() && R2000VxControl.FrameIsSelfConsistent())
        {
            int vxOff = objectData.Length;
            var withVx = new byte[vxOff + R2000VxControl.Frame.Length];
            Buffer.BlockCopy(objectData, 0, withVx, 0, vxOff);
            Buffer.BlockCopy(R2000VxControl.Frame, 0, withVx, vxOff, R2000VxControl.Frame.Length);
            objectData = withVx;
            entries.Add((vxh, vxOff));
            source.AddDiagnostic($"版本转换：AC1015 目标已补 VX_CONTROL 表（句柄 {vxh:X}）。");
        }

        if (objectData is null)
        {
            source.AddDiagnostic("版本转换：对象流未能重建，输出文件不含对象数据。");
            return R2000Writer.Build(targetVersion, sections);
        }

        int headerSize = 0x19 + 6 * 9 + 2 + R2000Writer.FileHeaderSentinel.Length;
        long objAddr = headerSize;
        foreach (var prior in sections)
            objAddr += prior.Data.Length;

        sections.Add(new R2000Writer.SectionEntry("Objects", objectData) { RecordNumber = 6, InLocator = false });

        var absolute = entries!.Select(p => (p.Handle, Offset: objAddr + p.Offset)).ToList();
        sections.Add(new R2000Writer.SectionEntry("Handles", R2000ObjectMap.BuildObjectMap(absolute)) { RecordNumber = 2 });

        if (!targetVersion.IsR2004Plus())
        {
            int hdrSize = 0x19 + 6 * 9 + 2 + R2000Writer.FileHeaderSentinel.Length;
            long addr = hdrSize;
            var layout = new Dictionary<string, (long Addr, uint Size)>();
            foreach (var s in sections)
            {
                layout[s.Name] = (addr, (uint)s.Data.Length);
                addr += s.Data.Length;
            }
            var second = BuildSecondHeader(targetVersion, layout, addr, walk?.Records, vxHandle);
            sections.Add(new R2000Writer.SectionEntry("2ndHeader", second) { InLocator = false });
        }


        if (targetVersion.IsR2004Plus())
        {
            var r2004Sections = new List<R2004Writer.SectionEntry>();
            foreach (var sec in sections)
                r2004Sections.Add(new R2004Writer.SectionEntry(sec.Name, sec.Data));
            r2004Sections.Add(new R2004Writer.SectionEntry("AcDb:Preview", new byte[0x36]));
            r2004Sections.Add(new R2004Writer.SectionEntry("AcDb:SummaryInfo", Array.Empty<byte>()));
            r2004Sections.Add(new R2004Writer.SectionEntry("AcDb:FileDepList", Array.Empty<byte>()));
            r2004Sections.Add(new R2004Writer.SectionEntry("AcDb:AppInfo", Array.Empty<byte>()));
            r2004Sections.Add(new R2004Writer.SectionEntry("AcDb:RevHistory", Array.Empty<byte>()));
            r2004Sections.Add(new R2004Writer.SectionEntry("AcDb:Security", Array.Empty<byte>()));
            var template = new byte[0x100];
            string sig = targetVersion switch
            {
                DwgVersion.AC1018 => "AC1018", DwgVersion.AC1021 => "AC1021",
                DwgVersion.AC1024 => "AC1024", DwgVersion.AC1027 => "AC1027",
                DwgVersion.AC1032 => "AC1032", _ => "AC1018",
            };
            Encoding.ASCII.GetBytes(sig, 0, 6, template, 0);
            var dec = new byte[120];
            Encoding.ASCII.GetBytes("AcFssFcAJMB", 0, 11, dec, 0);
            SetU32(dec, 0x10, 108); SetU32(dec, 0x14, 4); SetU32(dec, 0x24, 1);
            SetU32(dec, 0x44, 0x20); SetU32(dec, 0x48, 0x80); SetU32(dec, 0x4C, 0x40);
            uint seed = 1;
            for (int i = 0; i < 120; i++)
            {
                seed = (uint)((ulong)seed * 0x343FD + 0x269EC3);
                template[0x80 + i] = (byte)(dec[i] ^ (seed >> 0x10));
            }
            return R2004Writer.Build(targetVersion, template, r2004Sections);
        }
        return R2000Writer.Build(targetVersion, sections);
    }

    private static byte[] BuildSecondHeader(DwgVersion version,
        Dictionary<string, (long Addr, uint Size)> layout, long selfAddr,
        IEnumerable<DwgObjectRecord>? records, ulong? vxHandle = null)
    {
        const int SizeFieldOffset = 16;
        var w = new BitWriter(512);
        foreach (var b in SecondHeaderBeginSentinel) w.WriteRC(b);

        int sizeAdr = w.BytePosition;
        w.WriteRL(0);

        w.WriteBL((int)selfAddr);
        string sig = version switch
        {
            DwgVersion.AC1015 => "AC1015",
            _ => "AC1015",
        };
        foreach (var c in Encoding.ASCII.GetBytes(sig)) w.WriteRC(c);
        for (int i = sig.Length; i < 11; i++) w.WriteRC(0);
        w.WriteRC(0x0F);
        w.WriteRC(0x01);
        w.WriteRS(0x691F);
        w.WriteRS(39);

        w.WriteBS(6);
        void WriteSection(int nr, string name, bool real)
        {
            w.WriteRC((byte)nr);
            if (real && layout.TryGetValue(name, out var s))
            {
                w.WriteBL((int)s.Addr);
                w.WriteBL((int)s.Size);
            }
            else
            {
                w.WriteBL(0);
                w.WriteBL(0);
            }
        }
        WriteSection(0, "Header", true);
        WriteSection(1, "Classes", true);
        WriteSection(2, "Handles", true);
        WriteSection(3, "ObjFreeSpace", false);
        WriteSection(4, "Template", false);
        WriteSection(5, "AuxHeader", true);

        var byType = new Dictionary<short, ulong>();
        if (records is not null)
            foreach (var r in records)
                if (!byType.ContainsKey(r.TypeCode))
                    byType[r.TypeCode] = r.Handle;

        ulong GetHdl(short type) => byType.TryGetValue(type, out var h) ? h : 0UL;
        var handles = new (byte Nr, ulong Value)[14]
        {
            (0, 0),
            (1, GetHdl(0x30)),
            (2, GetHdl(0x32)),
            (3, GetHdl(0x34)),
            (4, GetHdl(0x38)),
            (5, GetHdl(0x3C)),
            (6, GetHdl(0x3E)),
            (7, GetHdl(0x40)),
            (8, GetHdl(0x42)),
            (9, GetHdl(0x44)),
            (10, vxHandle ?? GetHdl(0x46)),
            (11, 0),
            (12, 0),
            (13, 0),
        };

        w.WriteBS(14);
        foreach (var (nr, value) in handles)
        {
            var bytes = HandleBytes(value);
            w.WriteRC((byte)bytes.Length);
            w.WriteRC((byte)nr);
            foreach (var b in bytes) w.WriteRC(b);
        }

        w.AlignByte();
        var data = w.ToArray();
        int crcPos = w.BytePosition;
        ushort crc = Crc.Compute16Dwg(data, SizeFieldOffset, crcPos - SizeFieldOffset);
        w.WriteRS(crc);

        foreach (var b in new byte[] { 0x38, 0xEE, 0x5B, 0x84, 0xE4, 0xEF, 0xFA, 0x1F })
            w.WriteRC(b);

        foreach (var b in SecondHeaderEndSentinel) w.WriteRC(b);

        data = w.ToArray();
        uint size = (uint)(data.Length - SizeFieldOffset - 16);
        data[sizeAdr] = (byte)size;
        data[sizeAdr + 1] = (byte)(size >> 8);
        data[sizeAdr + 2] = (byte)(size >> 16);
        data[sizeAdr + 3] = (byte)(size >> 24);
        return data;

        static byte[] HandleBytes(ulong v)
        {
            if (v == 0) return [0x00];
            int n = 0;
            for (ulong t = v; t != 0; t >>= 8) n++;
            var b = new byte[n];
            for (int i = n - 1; i >= 0; i--) { b[i] = (byte)(v & 0xFF); v >>= 8; }
            return b;
        }
    }

    private static DwgObjectReader.WalkResult? SafeGetObjects(BaseDwgFile file)
    {
        if (Objects.WriteSideEdits.EditedWalk is { } edited) return edited;
        try { return file.GetObjects(); }
        catch (Exception ex) when (ex is IOException or InvalidDataException or NotSupportedException
                                      or ArgumentException or System.Text.DecoderFallbackException)
        {
            return null;
        }
    }

    private static byte[] BuildFromR2004Source(R2004File r2004, DwgVersion targetVersion)
    {
        var vxWalk = SafeGetObjects(r2004);
        ulong? vxHandle = R2000VxControl.TrySynthesisHandle(vxWalk, out ulong vxh) ? vxh : null;
        byte[]? objects = null, handles = null;
        var standard = new Dictionary<string, byte[]>();
        foreach (var (name, data) in r2004.GetAllSectionData())
        {
            switch (name)
            {
                case "AcDb:AcDbObjects":
                case "AcDb:Objects":
                    objects ??= data;
                    break;
                case "AcDb:Handles":
                    handles ??= data;
                    break;
                case "AcDb:Header":
                    standard["Header"] = ReencodeHeaderVarsForTarget(
                        ReframeHeaderSection(data), r2004.Header.Version, targetVersion, vxHandle);
                    break;
                case "AcDb:Classes":
                    {
                        var parsed = R2000ClassTable.TryParseR2004Classes(data, r2004.Header.Version);
                        standard["Classes"] = parsed is { Count: > 0 } ? R2000ClassTable.BuildClassesSection(parsed) : data;
                        break;
                    }
                case "AcDb:ObjFreeSpace": standard["ObjFreeSpace"] = data; break;
                case "AcDb:Template": standard["Template"] = data; break;
                case "AcDb:AuxHeader": standard["AuxHeader"] = data; break;
            }
        }

        var sections = new List<R2000Writer.SectionEntry>();
        foreach (var (slot, recNum) in new[]
                 {
                     ("Header", 0), ("Classes", 1), ("ObjFreeSpace", 3),
                     ("Template", 4), ("AuxHeader", 5),
                 })
        {
            standard.TryGetValue(slot, out var data);
            sections.Add(new R2000Writer.SectionEntry(slot, data ?? Array.Empty<byte>())
            {
                RecordNumber = recNum,
            });
        }
        NormalizeSections(sections);

        return AppendRebuiltObjects(sections, r2004, targetVersion, vxHandle);
    }

    internal static byte[] ReframeHeaderSection(byte[] data)
    {
        if (data.Length < 16 + 4 + 2 + 16
            || !data.AsSpan(0, 16).SequenceEqual(HeaderBeginSentinel))
            return data;

        int endIdx = IndexOf(data, HeaderEndSentinel, 20);
        int rl = (int)BitConverter.ToUInt32(data, 16);
        if (endIdx < 0 || rl < 0 || 20 + rl > data.Length || endIdx < 20 + rl)
            return data;

        var buf = new byte[38 + rl];
        Buffer.BlockCopy(data, 0, buf, 0, 20 + rl);
        ushort crc = Crc.Compute16Dwg(buf, 16, 4 + rl);
        buf[20 + rl] = (byte)crc;
        buf[20 + rl + 1] = (byte)(crc >> 8);
        Buffer.BlockCopy(HeaderEndSentinel, 0, buf, 22 + rl, 16);
        return buf;
    }

    private static int IndexOf(byte[] haystack, byte[] needle, int from)
    {
        for (int i = from; i + needle.Length <= haystack.Length; i++)
        {
            if (haystack.AsSpan(i, needle.Length).SequenceEqual(needle))
                return i;
        }
        return -1;
    }

    private static void SetU32(byte[] b, int o, uint v)
    {
        b[o]=(byte)v; b[o+1]=(byte)(v>>8); b[o+2]=(byte)(v>>16); b[o+3]=(byte)(v>>24);
    }

    private static void PopulateDowngradeClassTypeMap(DwgObjectReader.WalkResult walk)
    {
        if (walk.Classes is not { Count: > 0 } classes) return;
        foreach (var cls in classes)
        {
            if (!R2004SectionReencoder.ClassFixedCodes.TryGetValue(cls.DxfName, out short fixedCode)
                || fixedCode <= 0) continue;
            if (fixedCode is 0x4D or 0x4E) continue;
            walk.ClassTypeMap[fixedCode - 500] = (short)cls.Number;
            if (DwgObjectStreamWriter.DebugTrace)
                System.Console.Error.WriteLine(
                    $"CLASS_DN: {cls.DxfName} 固定码 {fixedCode} → class#{cls.Number}（键 {fixedCode - 500}）");
        }
    }
}
