using System.Text;
using DwgParser.Core.Compression;
using DwgParser.Core.IO;

namespace DwgParser.Core.Format;

public static class R2004Writer
{
    private const int SystemPageHeaderSize = 20;
    private const uint PageType = 0x4163043Bu;
    private const uint MapMagic = 0x41630E3Bu;
    private const uint InfoMagic = 0x4163003Bu;
    private const uint PageXorMask = 0x4164536Bu;
    private const int HeaderFieldSize = 108;
    private const int MaxDecompSize = 0x7400;
    private const int MinStoreLength = 4;

    public sealed class SectionEntry
    {
        public string Name { get; }
        public byte[] Data { get; }
        public int TypeId { get; }
        public int RecordNumber { get; internal set; }
        public long FileAddress { get; internal set; }
        public int FileSize { get; internal set; }
        internal int Capacity { get; set; } = MaxDecompSize;

        private byte[][] _payloads = [];

        public int ContentSize => Data.Length;

        public SectionEntry(string name, byte[] data, int typeId = 0)
        { Name = name; Data = data; TypeId = typeId; }

        internal int PageCount => Math.Max(1, (Data.Length + Capacity - 1) / Capacity);

        internal int CompType { get; set; } = 2;

        internal void PreparePages()
        {
            int pages = PageCount;
            _payloads = new byte[pages][];
            for (int j = 0; j < pages; j++)
            {
                int off = j * Capacity;
                int len = Math.Clamp(Data.Length - off, 0, Capacity);
                if (CompType == 1)
                {
                    var raw = new byte[Capacity];
                    if (len > 0) Buffer.BlockCopy(Data, off, raw, 0, len);
                    _payloads[j] = raw;
                }
                else
                {
                    int storeLen = Math.Max(len, MinStoreLength);
                    var slice = new byte[storeLen];
                    if (len > 0) Buffer.BlockCopy(Data, off, slice, 0, len);
                    _payloads[j] = RsEncoder.Compress(slice);
                }
            }
        }

        internal byte[] Payload(int j) => _payloads[j];

        internal int PageDataSize(int j)
            => CompType == 1 ? Capacity : _payloads[j].Length;

        internal int PageFileLen(int j) => 32 + Align(_payloads[j].Length);

        internal int TotalFileSize
        {
            get
            {
                int sum = 0;
                for (int j = 0; j < PageCount; j++) sum += PageFileLen(j);
                return sum;
            }
        }
    }

    public static byte[] Build(DwgVersion version, byte[] originalFile, IReadOnlyList<SectionEntry> rawSections)
    {
        var sections = Prepare(rawSections, version);
        foreach (var e in sections) e.PreparePages();

        int S = sections.Count;
        int totalPages = sections.Sum(e => e.PageCount);

        int pn = 1;
        foreach (var e in sections)
        {
            e.RecordNumber = pn;
            pn += e.PageCount;
        }

        int lastId = totalPages;
        int infoNum = lastId + 2, mapNum = lastId + 4;
        int numSectionsField = lastId + 2;

        long pos = 0x100;
        foreach (var e in sections)
        {
            e.FileAddress = pos;
            e.FileSize = e.TotalFileSize;
            pos += e.FileSize;
        }

        int infoDecomp = 20 + (S + 1) * 96 + totalPages * 16;
        int mapDecomp = (totalPages + 2) * 8;
        int infoFileSize = SystemPageHeaderSize + RsEncoder.StoredSize(infoDecomp);
        int mapFileSize = SystemPageHeaderSize + RsEncoder.StoredSize(mapDecomp);

        long infoAddr = Align(pos);
        pos = Align(infoAddr + infoFileSize);
        long mapAddr = pos;
        pos = mapAddr + Align(mapFileSize);
        long secondHdrPos = Align(pos);
        long fileSize = secondHdrPos + 20 + HeaderFieldSize;

        var dec = new byte[HeaderFieldSize];
        if (originalFile.Length >= 0x80 + HeaderFieldSize)
            LcgDecrypt(originalFile, 0x80, dec, HeaderFieldSize);
        EnsureHeaderDefaults(dec);

        var infoContent = new byte[infoDecomp];
        BuildInfoContent(infoContent, sections, mapDecomp);
        var infoComp = RsEncoder.Store(infoContent);

        var mapContent = new byte[mapDecomp];
        BuildMapRecords(mapContent, sections, infoNum, infoFileSize, mapNum, mapFileSize);
        var mapComp = RsEncoder.Store(mapContent);

        var file = new byte[fileSize];
        Buffer.BlockCopy(originalFile, 0, file, 0, Math.Min(0x80, originalFile.Length));
        WriteSignature(file, version);
        OverridePlaintextHeaderByVersion(file, version);

        foreach (var e in sections)
            WriteDataPages(file, e);

        WriteSystemPage(file, infoAddr, InfoMagic, infoContent, infoComp);
        WriteSystemPage(file, mapAddr, MapMagic, mapContent, mapComp);

        PatchAddr(file, 0x0D, PayloadAddr(sections, "AcDb:Preview"));
        PatchAddr(file, 0x20, PayloadAddr(sections, "AcDb:SummaryInfo"));

        EncryptHeader(dec, originalFile, file, version, infoNum, mapNum, numSectionsField,
                      lastId, mapAddr, mapFileSize, secondHdrPos);
        WriteSecondHeader(file, secondHdrPos, file, 0x80);

        return file;
    }

    private static List<SectionEntry> Prepare(IReadOnlyList<SectionEntry> raw, DwgVersion target)
    {
        var byName = new Dictionary<string, SectionEntry>(StringComparer.OrdinalIgnoreCase);
        var order = new List<string>();
        foreach (var e in raw)
        {
            if (e.Data.Length == 0) continue;
            string name = CanonicalName(e.Name);
            if (name == "AcDb:AcDsPrototype_1b" && target < DwgVersion.AC1027)
                continue;
            if (!byName.TryGetValue(name, out var old))
                order.Add(name);
            int typeId = ResolveTypeId(name, e.TypeId);
            byName[name] = new SectionEntry(name, e.Data, typeId)
            {
                Capacity = PageCapacity(name, e.Data.Length),
                CompType = IsRawSection(name) ? 1 : 2,
            };
        }
        var result = new List<SectionEntry>(order.Count);
        foreach (var n in order) result.Add(byName[n]);
        result.Sort((a, b) => b.TypeId != a.TypeId
            ? b.TypeId.CompareTo(a.TypeId)
            : string.CompareOrdinal(a.Name, b.Name));
        return result;
    }

    private static int PageCapacity(string name, int contentSize)
    {
        if (name is "AcDb:FileDepList" or "AcDb:AppInfo" or "AcDb:Preview" or "AcDb:SummaryInfo")
            return Math.Max(128, (contentSize + 127) & ~127);
        return MaxDecompSize;
    }

    private static bool IsRawSection(string name)
        => name is "AcDb:FileDepList" or "AcDb:AppInfo" or "AcDb:Preview" or "AcDb:SummaryInfo";

    private static string CanonicalName(string name) => name switch
    {
        "AcDb:Objects" => "AcDb:AcDbObjects",
        "Objects" => "AcDb:AcDbObjects",
        _ => name.StartsWith("AcDb:", StringComparison.Ordinal) ? name : "AcDb:" + name,
    };

    private static int ResolveTypeId(string name, int given) => name switch
    {
        "AcDb:Header" => 1,
        "AcDb:AuxHeader" => 2,
        "AcDb:Classes" => 3,
        "AcDb:Handles" => 4,
        "AcDb:Template" => 5,
        "AcDb:ObjFreeSpace" => 6,
        "AcDb:AcDbObjects" => 7,
        "AcDb:RevHistory" => 8,
        "AcDb:SummaryInfo" => 9,
        "AcDb:Preview" => 10,
        "AcDb:AppInfo" => 11,
        "AcDb:FileDepList" => 12,
        "AcDb:AcDsPrototype_1b" => 13,
        "AcDb:Security" => 14,
        "AcDb:VBAProject" => 15,
        "AcDb:Signature" => 16,
        "AcDb:AppInfoHistory" => 17,
        _ => given != 0 ? given : 0x20,
    };

    private static void BuildInfoContent(byte[] buf, List<SectionEntry> sections, int mapDecomp)
    {
        int q = 0;
        int total = sections.Count + 1;
        W32(buf, ref q, (uint)total);
        W32(buf, ref q, 2);
        W32(buf, ref q, MaxDecompSize);
        W32(buf, ref q, 0);
        W32(buf, ref q, (uint)total);

        WriteDesc(buf, ref q, 0, 0, MaxDecompSize, 1, 2, 0, 0, "");

        foreach (var e in sections)
        {
            int pages = e.PageCount;
            WriteDesc(buf, ref q, e.ContentSize, pages, e.Capacity, 1, e.CompType, e.TypeId, 0, e.Name);
            for (int j = 0; j < pages; j++)
            {
                W32(buf, ref q, (uint)e.RecordNumber + (uint)j);
                W32(buf, ref q, (uint)e.PageDataSize(j));
                W64(buf, ref q, (ulong)(j * e.Capacity));
            }
        }
    }

    private static void WriteDesc(byte[] buf, ref int q,
        long size, int numPages, int maxDecomp, int unknown, int compType,
        int typeId, int encrypted, string name)
    {
        W64(buf, ref q, (ulong)size);
        W32(buf, ref q, (uint)numPages);
        W32(buf, ref q, (uint)maxDecomp);
        W32(buf, ref q, (uint)unknown);
        W32(buf, ref q, (uint)compType);
        W32(buf, ref q, (uint)typeId);
        W32(buf, ref q, (uint)encrypted);
        int nameLen = Math.Min(name.Length, 63);
        Encoding.ASCII.GetBytes(name, 0, nameLen, buf, q);
        q += 64;
    }

    private static void BuildMapRecords(byte[] buf, List<SectionEntry> sections,
        int infoNum, int infoFileSize, int mapNum, int mapFileSize)
    {
        int q = 0;
        foreach (var e in sections)
        {
            int pages = e.PageCount;
            for (int j = 0; j < pages; j++)
            {
                W32(buf, ref q, (uint)e.RecordNumber + (uint)j);
                W32(buf, ref q, (uint)e.PageFileLen(j));
            }
        }
        W32(buf, ref q, (uint)infoNum);
        W32(buf, ref q, (uint)Align(infoFileSize));
        W32(buf, ref q, (uint)mapNum);
        W32(buf, ref q, (uint)Align(mapFileSize));
    }

    private static void WriteDataPages(byte[] file, SectionEntry e)
    {
        long addr = e.FileAddress;
        int pages = e.PageCount;
        for (int j = 0; j < pages; j++)
        {
            var payload = e.Payload(j);
            int dataLen = payload.Length;
            int pageFileLen = 32 + Align(dataLen);
            uint dataCrc = R2004Checksum.Compute(0, payload, false);

            var hdr = new uint[8];
            hdr[0] = PageType;
            hdr[1] = (uint)e.TypeId;
            hdr[2] = (uint)dataLen;
            hdr[3] = (uint)pageFileLen;
            hdr[4] = (uint)(j * e.Capacity);
            hdr[5] = 0;
            hdr[7] = dataCrc;

            var hdrBuf = new byte[32];
            WriteUints(hdr, hdrBuf);
            hdr[6] = R2004Checksum.Compute(dataCrc, hdrBuf, false);
            WriteUints(hdr, hdrBuf);

            uint mask = PageXorMask ^ (uint)addr;
            for (int n = 0; n < 8; n++)
                SetU32(hdrBuf, n * 4, hdr[n] ^ mask);
            Buffer.BlockCopy(hdrBuf, 0, file, (int)addr, 32);
            Buffer.BlockCopy(payload, 0, file, (int)addr + 32, dataLen);

            addr += pageFileLen;
        }
        System.Diagnostics.Debug.Assert(addr == e.FileAddress + e.FileSize);
    }

    private static void WriteSystemPage(byte[] file, long addr, uint magic,
        byte[] content, byte[] comp)
    {
        int a = (int)addr;
        var hdr = new byte[20];
        SetU32(hdr, 0, magic);
        SetU32(hdr, 4, (uint)content.Length);
        SetU32(hdr, 8, (uint)comp.Length);
        SetU32(hdr, 12, 2);
        uint cs1 = R2004Checksum.Compute(0, hdr, true);
        uint cs2 = R2004Checksum.Compute(cs1, comp, false);
        SetU32(hdr, 16, cs2);
        Buffer.BlockCopy(hdr, 0, file, a, 20);
        Buffer.BlockCopy(comp, 0, file, a + 20, comp.Length);
    }

    private static void WriteSecondHeader(byte[] file, long pos, byte[] srcAt80, int srcOffset)
    {
        int a = (int)pos;
        file[a] = 0x3b; file[a + 1] = 0x0e; file[a + 2] = 0x63; file[a + 3] = 0x41;
        file[a + 12] = 0x02;
        if (srcOffset + HeaderFieldSize <= srcAt80.Length && a + 20 + HeaderFieldSize <= file.Length)
            Buffer.BlockCopy(srcAt80, srcOffset, file, a + 20, HeaderFieldSize);
    }

    private static void EnsureHeaderDefaults(byte[] dec)
    {
        SetU32(dec, 0x0C, 0);
        SetU32(dec, 0x10, 108);
        SetU32(dec, 0x14, 4);
        SetU32(dec, 0x24, 1);
        SetU32(dec, 0x44, 0x20);
        SetU32(dec, 0x48, 0x80);
        SetU32(dec, 0x4C, 0x40);
    }

    private static void EncryptHeader(byte[] dec, byte[] originalFile, byte[] outFile,
        DwgVersion targetVersion, int infoNum, int mapNum, int numSectionsField, int lastId,
        long mapAddr, int mapFileSize, long secondHdrPos)
    {
        SetU32(dec, 0x28, (uint)mapNum);
        SetU64(dec, 0x2C, (ulong)(mapAddr + Align(mapFileSize) - 0x100));
        SetU64(dec, 0x34, (ulong)(secondHdrPos + 20));
        SetU32(dec, 0x3C, 0);
        SetU32(dec, 0x40, (uint)numSectionsField);
        SetU32(dec, 0x50, (uint)mapNum);
        SetU64(dec, 0x54, (ulong)(mapAddr - 0x100));
        SetU32(dec, 0x5C, (uint)infoNum);
        SetU32(dec, 0x60, (uint)mapNum);
        SetU32(dec, 0x64, 0);
        dec[0x68] = dec[0x69] = dec[0x6A] = dec[0x6B] = 0;
        SetU32(dec, 0x68, Crc.Compute32(dec, 0, 0x6C));

        uint rseed = 1;
        for (int i = 0; i < HeaderFieldSize; i++)
        {
            rseed = (uint)((ulong)rseed * 0x343FD + 0x269EC3);
            outFile[0x80 + i] = (byte)(dec[i] ^ (rseed >> 0x10));
        }
        if (originalFile.Length >= 0x100)
            Buffer.BlockCopy(originalFile, 0xEC, outFile, 0xEC, 0x14);
    }

    private static void OverridePlaintextHeaderByVersion(byte[] file, DwgVersion version)
    {
        if (file.Length < 0x14) return;
        file[0x0B] = version switch
        {
            DwgVersion.AC1024 => 0x06,
            DwgVersion.AC1027 => 0x69,
            DwgVersion.AC1032 => 0x37,
            _ => 0x00,
        };
        file[0x11] = 0x21;
        file[0x12] = 0x37;
        file[0x13] = 0x27;
    }

    private static int Align(int v) => (v + 31) & ~31;
    private static long Align(long v) => (v + 31) & ~31L;

    private static void LcgDecrypt(byte[] src, int offset, byte[] dst, int len)
    {
        uint s = 1;
        for (int i = 0; i < len; i++)
        {
            s = (uint)((ulong)s * 0x343FD + 0x269EC3);
            dst[i] = (byte)(src[offset + i] ^ (s >> 0x10));
        }
    }

    private static void WriteSignature(byte[] file, DwgVersion v)
    {
        string sig = v switch
        {
            DwgVersion.AC1018 => "AC1018", DwgVersion.AC1021 => "AC1021",
            DwgVersion.AC1024 => "AC1024", DwgVersion.AC1027 => "AC1027",
            DwgVersion.AC1032 => "AC1032", _ => "AC1018",
        };
        Encoding.ASCII.GetBytes(sig, 0, 6, file, 0);
    }

    private static void PatchAddr(byte[] f, int off, uint val) => SetU32(f, off, val);

    private static uint PayloadAddr(List<SectionEntry> s, string name)
    {
        foreach (var e in s)
            if (string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase))
                return (uint)(e.FileAddress + 32);
        return 0;
    }

    private static void WriteUints(uint[] h, byte[] b) { for (int i = 0; i < 8; i++) SetU32(b, i * 4, h[i]); }
    private static void SetU32(byte[] b, int o, uint v) { b[o] = (byte)v; b[o + 1] = (byte)(v >> 8); b[o + 2] = (byte)(v >> 16); b[o + 3] = (byte)(v >> 24); }
    private static void SetU64(byte[] b, int o, ulong v) { SetU32(b, o, (uint)v); SetU32(b, o + 4, (uint)(v >> 32)); }
    private static void W32(byte[] b, ref int q, uint v) { SetU32(b, q, v); q += 4; }
    private static void W64(byte[] b, ref int q, ulong v) { SetU64(b, q, v); q += 8; }
}
