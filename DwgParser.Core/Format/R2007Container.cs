using System.Text;
using DwgParser.Core.Compression;

namespace DwgParser.Core.Format;

public static class R2007Container
{
    public const int DataAreaStart = 0x480;
    public const int HeaderFieldsSize = 0x110;
    public const int CheckBlockSize = 32;

    const int SysDataBytes = 239;
    const int SysParityTotal = 255;
    const int DataDataBytes = 251;
    const int DataParityTotal = 255;

    public sealed class R2007Header
    {
        public ulong HeaderSize;
        public ulong FileSize;
        public ulong PagesMapCrcComp;
        public ulong PagesMapCorrection;
        public ulong PagesMapCrcSeed;
        public ulong PagesMap2Offset;
        public ulong PagesMap2Id;
        public ulong PagesMapOffset;
        public ulong PagesMapId;
        public ulong Header2Offset;
        public ulong PagesMapSizeComp;
        public ulong PagesMapSizeUncomp;
        public ulong PagesAmount;
        public ulong PagesMaxId;
        public ulong Unknown1;
        public ulong Unknown2;
        public ulong PagesMapCrcUncomp;
        public ulong Unknown3;
        public ulong Unknown4;
        public ulong Unknown5;
        public ulong NumSections;
        public ulong SectionsMapCrcUncomp;
        public ulong SectionsMapSizeComp;
        public ulong SectionsMap2Id;
        public ulong SectionsMapId;
        public ulong SectionsMapSizeUncomp;
        public ulong SectionsMapCrcComp;
        public ulong SectionsMapCorrection;
        public ulong SectionsMapCrcSeed;
        public ulong StreamVersion;
        public ulong CrcSeed;
        public ulong CrcSeedEncoded;
        public ulong RandomSeed;
        public ulong HeaderCrc;
    }

    public sealed record PageEntry(long Number, long Size, ulong Address);

    public sealed class SectionPage
    {
        public ulong Offset;
        public ulong Size;
        public long Number;
        public ulong UncompSize;
        public ulong CompSize;
        public ulong Checksum;
        public ulong Crc64;
    }

    public sealed class R2007Section
    {
        public string Name = "";
        public long DataSize;
        public long MaxSize;
        public long Encrypted;
        public long Hashcode;
        public long Unknown;
        public long Encoded;
        public readonly List<SectionPage> Pages = new();
    }

    public sealed class Model
    {
        public R2007Header Header = new();
        public readonly List<PageEntry> Pages = new();
        public readonly List<R2007Section> Sections = new();
        public readonly List<string> Diagnostics = new();
    }

    public static bool TryLoad(byte[] file, out Model? model)
    {
        model = null;
        var m = new Model();
        if (file.Length < DataAreaStart + CheckBlockSize)
        {
            m.Diagnostics.Add("文件过小，不足以容纳 R2007 容器");
            return false;
        }

        var pedata = Deinterleave(file, 0x80, 3, SysDataBytes);
        if (pedata.Length < CheckBlockSize + 3)
        {
            m.Diagnostics.Add("节头部 RS 载荷过短");
            return false;
        }
        int comprLen = (int)BitConverter.ToInt32(pedata, 0x18);
        int len2 = (int)BitConverter.ToInt32(pedata, 0x1C);
        byte[] fields;
        if (comprLen < 0)
        {
            if (CheckBlockSize - (-comprLen) + HeaderFieldsSize > pedata.Length)
            {
                m.Diagnostics.Add("节头部原样载荷越界");
                return false;
            }
            fields = new byte[HeaderFieldsSize];
            Buffer.BlockCopy(pedata, CheckBlockSize, fields, 0, HeaderFieldsSize);
        }
        else
        {
            int avail = Math.Min(comprLen, pedata.Length - CheckBlockSize);
            if (avail <= 0 ||
                !Lz77Ac21.TryDecompress(pedata, CheckBlockSize, avail, HeaderFieldsSize, out fields))
            {
                m.Diagnostics.Add($"节头部解压失败（compr_len={comprLen}, len2={len2}）");
                return false;
            }
        }
        ReadHeaderFields(fields, m.Header);
        if (m.Header.HeaderSize != 0x70)
        {
            m.Diagnostics.Add($"节头部 header_size=0x{m.Header.HeaderSize:X} 非 0x70，容器判定失败");
            return false;
        }

        long pagesMapAddr = DataAreaStart + (long)m.Header.PagesMapOffset;
        var pagesMapData = ReadSystemPage(file, m, pagesMapAddr,
            (long)m.Header.PagesMapSizeComp, (long)m.Header.PagesMapSizeUncomp,
            (long)m.Header.PagesMapCorrection, "页表");
        if (pagesMapData == null) return false;
        if (!ParsePagesMap(pagesMapData, m)) return false;

        if (!TryGetPageAddress(m, (long)m.Header.SectionsMapId, out ulong secMapAddr))
        {
            m.Diagnostics.Add($"页表中找不到节目录页 id={m.Header.SectionsMapId}");
            return false;
        }
        var secMapData = ReadSystemPage(file, m, (long)secMapAddr,
            (long)m.Header.SectionsMapSizeComp, (long)m.Header.SectionsMapSizeUncomp,
            (long)m.Header.SectionsMapCorrection, "节目录");
        if (secMapData == null) return false;
        if (!ParseSectionsMap(secMapData, m)) return false;

        model = m;
        return true;
    }

    public static byte[]? ReadSection(byte[] file, Model m, string name)
    {
        var sec = m.Sections.FirstOrDefault(s =>
            string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));
        if (sec == null) return null;

        long need = sec.DataSize;
        foreach (var p in sec.Pages)
            need = Math.Max(need, (long)p.Offset + (long)p.UncompSize);
        if (need <= 0 || need > RsDecoder.MaxReasonableSize) return null;
        int total = (int)need;
        var buffer = new byte[total];
        bool any = false;
        foreach (var p in sec.Pages)
        {
            if (!TryGetPageAddress(m, p.Number, out ulong addr)) continue;
            var payload = DecodeDataPage(file, m, addr, p, sec);
            if (payload == null) continue;
            long room = total - (long)p.Offset;
            if (room <= 0) continue;
            int take = (int)Math.Min(payload.Length, room);
            Buffer.BlockCopy(payload, 0, buffer, (int)p.Offset, take);
            any = true;
        }
        return any ? buffer : null;
    }

    private static byte[]? ReadSystemPage(byte[] file, Model m, long addr,
        long sizeComp, long sizeUncomp, long repeat, string what)
    {
        if (sizeComp <= 0 || sizeUncomp <= 0 || sizeUncomp > RsDecoder.MaxReasonableSize)
        {
            m.Diagnostics.Add($"{what} 尺寸非法：comp={sizeComp} uncomp={sizeUncomp}");
            return null;
        }
        if (repeat < 1) repeat = 1;
        long pesize = ((sizeComp + 7) & ~7L) * repeat;
        long blocks = (pesize + SysDataBytes - 1) / SysDataBytes;
        if (blocks <= 0 || blocks > 0x100000)
        {
            m.Diagnostics.Add($"{what} RS 块数异常：{blocks}");
            return null;
        }
        long diskSize = (blocks * SysParityTotal + 7) & ~7L;
        if (addr < 0 || addr + diskSize > file.Length)
        {
            m.Diagnostics.Add($"{what} 盘上范围越界：@0x{addr:X} +{diskSize} / {file.Length}");
            return null;
        }
        var region = Deinterleave(file, (int)addr, (int)blocks, SysDataBytes);
        foreach (int prefix in new[] { CheckBlockSize, 0 })
        {
            int avail = (int)Math.Min(sizeComp, (long)region.Length - prefix);
            if (avail <= 0) continue;
            if (Lz77Ac21.TryDecompress(region, prefix, avail, (int)sizeUncomp, out var data))
                return data;
            if (avail == sizeUncomp && region.Length - prefix >= sizeUncomp)
            {
                var raw = new byte[sizeUncomp];
                Buffer.BlockCopy(region, prefix, raw, 0, (int)sizeUncomp);
                return raw;
            }
        }
        m.Diagnostics.Add($"{what} 解压失败：comp={sizeComp} uncomp={sizeUncomp}");
        return null;
    }

    private static byte[]? DecodeDataPage(byte[] file, Model m, ulong addr,
        SectionPage p, R2007Section sec)
    {
        int need = (int)Math.Min(p.UncompSize, (long)RsDecoder.MaxReasonableSize);
        if (need <= 0) return null;
        long comp = (long)p.CompSize;
        if (comp <= 0) comp = (long)p.Size;

        byte[] payload;
        int[] prefixes;
        if (sec.Encoded == 4)
        {
            long pesize = (comp + 7) & ~7L;
            long blocks = (pesize + DataDataBytes - 1) / DataDataBytes;
            if (blocks <= 0 || blocks > 0x100000) return null;
            long diskSize = (blocks * DataParityTotal + 31) & ~31L;
            if ((long)addr + diskSize > file.Length) return null;
            payload = Deinterleave(file, (int)addr, (int)blocks, DataDataBytes);
            prefixes = new[] { CheckBlockSize, 0 };
        }
        else
        {
            long take = Math.Min(comp, file.Length - (long)addr);
            if (take <= 0) return null;
            payload = new byte[take];
            Buffer.BlockCopy(file, (int)addr, payload, 0, (int)take);
            prefixes = new[] { 0 };
        }

        if (p.CompSize == p.UncompSize)
        {
            int got = (int)Math.Min(need, payload.Length);
            var data = new byte[got];
            Buffer.BlockCopy(payload, 0, data, 0, got);
            return data;
        }

        foreach (int prefix in prefixes)
        {
            int avail = (int)Math.Min(comp, (long)payload.Length - prefix);
            if (avail <= 0) continue;
            if (Lz77Ac21.TryDecompress(payload, prefix, avail, need, out var data))
                return data;
        }
        m.Diagnostics.Add($"{sec.Name} 页 {p.Number} 解压失败（comp={comp} uncomp={need}）");
        return null;
    }

    private static byte[] Deinterleave(byte[] src, int offset, int blocks, int dataSize)
    {
        var dst = new byte[blocks * dataSize];
        for (int i = 0; i < blocks; i++)
        {
            int di = i * dataSize;
            int si = offset + i;
            for (int j = 0; j < dataSize; j++)
            {
                int sp = si + blocks * j;
                if (sp >= src.Length) break;
                dst[di + j] = src[sp];
            }
        }
        return dst;
    }

    private static void ReadHeaderFields(byte[] f, R2007Header h)
    {
        h.HeaderSize = U64(f, 0x00);
        h.FileSize = U64(f, 0x08);
        h.PagesMapCrcComp = U64(f, 0x10);
        h.PagesMapCorrection = U64(f, 0x18);
        h.PagesMapCrcSeed = U64(f, 0x20);
        h.PagesMap2Offset = U64(f, 0x28);
        h.PagesMap2Id = U64(f, 0x30);
        h.PagesMapOffset = U64(f, 0x38);
        h.PagesMapId = U64(f, 0x40);
        h.Header2Offset = U64(f, 0x48);
        h.PagesMapSizeComp = U64(f, 0x50);
        h.PagesMapSizeUncomp = U64(f, 0x58);
        h.PagesAmount = U64(f, 0x60);
        h.PagesMaxId = U64(f, 0x68);
        h.Unknown1 = U64(f, 0x70);
        h.Unknown2 = U64(f, 0x78);
        h.PagesMapCrcUncomp = U64(f, 0x80);
        h.Unknown3 = U64(f, 0x88);
        h.Unknown4 = U64(f, 0x90);
        h.Unknown5 = U64(f, 0x98);
        h.NumSections = U64(f, 0xA0);
        h.SectionsMapCrcUncomp = U64(f, 0xA8);
        h.SectionsMapSizeComp = U64(f, 0xB0);
        h.SectionsMap2Id = U64(f, 0xB8);
        h.SectionsMapId = U64(f, 0xC0);
        h.SectionsMapSizeUncomp = U64(f, 0xC8);
        h.SectionsMapCrcComp = U64(f, 0xD0);
        h.SectionsMapCorrection = U64(f, 0xD8);
        h.SectionsMapCrcSeed = U64(f, 0xE0);
        h.StreamVersion = U64(f, 0xE8);
        h.CrcSeed = U64(f, 0xF0);
        h.CrcSeedEncoded = U64(f, 0xF8);
        h.RandomSeed = U64(f, 0x100);
        h.HeaderCrc = U64(f, 0x108);
    }

    private static bool ParsePagesMap(byte[] data, Model m)
    {
        int q = 0;
        ulong acc = DataAreaStart;
        while (q + 16 <= data.Length)
        {
            long size = BitConverter.ToInt64(data, q);
            long number = BitConverter.ToInt64(data, q + 8);
            q += 16;
            m.Pages.Add(new PageEntry(number, size, acc));
            acc += (ulong)Math.Max(0, size);
        }
        if (m.Pages.Count == 0)
        {
            m.Diagnostics.Add("页表为空");
            return false;
        }
        return true;
    }

    private static bool ParseSectionsMap(byte[] data, Model m)
    {
        int q = 0;
        long limit = m.Header.NumSections > 1 ? (long)m.Header.NumSections - 1 : (long)m.Header.NumSections;
        while (m.Sections.Count < limit && q + 64 <= data.Length)
        {
            var s = new R2007Section
            {
                DataSize = BitConverter.ToInt64(data, q),
                MaxSize = BitConverter.ToInt64(data, q + 8),
                Encrypted = BitConverter.ToInt64(data, q + 16),
                Hashcode = BitConverter.ToInt64(data, q + 24),
            };
            long nameLen = BitConverter.ToInt64(data, q + 32);
            s.Unknown = BitConverter.ToInt64(data, q + 40);
            s.Encoded = BitConverter.ToInt64(data, q + 48);
            long numPages = BitConverter.ToInt64(data, q + 56);
            q += 64;
            if (nameLen <= 0 || (nameLen & 1) != 0 || nameLen > 48 || q + nameLen > data.Length)
                break;
            s.Name = Encoding.Unicode.GetString(data, q, (int)nameLen).TrimEnd('\0');
            q += (int)nameLen;
            if (numPages < 0 || numPages > 0x10000 || q + numPages * 56 > data.Length)
                break;
            for (long j = 0; j < numPages; j++)
            {
                s.Pages.Add(new SectionPage
                {
                    Offset = U64(data, q),
                    Size = U64(data, q + 8),
                    Number = BitConverter.ToInt64(data, q + 16),
                    UncompSize = U64(data, q + 24),
                    CompSize = U64(data, q + 32),
                    Checksum = U64(data, q + 40),
                    Crc64 = U64(data, q + 48),
                });
                q += 56;
            }
            m.Sections.Add(s);
        }
        if (m.Sections.Count == 0)
        {
            m.Diagnostics.Add("节目录为空");
            return false;
        }
        return true;
    }

    public static bool TryGetPageAddressPublic(Model m, long number, out ulong address)
        => TryGetPageAddress(m, number, out address);

    private static bool TryGetPageAddress(Model m, long number, out ulong address)
    {
        foreach (var e in m.Pages)
        {
            if (e.Number == number || (number < 0 && e.Number == -number))
            {
                address = e.Address;
                return true;
            }
        }
        address = 0;
        return false;
    }

    private static ulong U64(byte[] b, int o) => BitConverter.ToUInt64(b, o);
}
