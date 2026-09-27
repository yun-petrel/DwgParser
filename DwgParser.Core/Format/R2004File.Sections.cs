using DwgParser.Core.Compression;
using DwgParser.Core.IO;
using DwgParser.Core.Objects;

namespace DwgParser.Core.Format;

public partial class R2004File
{
    private bool DecryptHeader()
    {
        if (_raw.Length < 0x80 + 120)
        {
            _diagnostics.Add("File too small for R2004+ header");
            return false;
        }
        var dec = new byte[120];
        uint rseed = 1;
        for (int i = 0; i < 120; i++)
        {
            rseed = (uint)((ulong)rseed * 0x343fd + 0x269ec3);
            dec[i] = (byte)(_raw[0x80 + i] ^ (rseed >> 0x10));
        }
        if (System.Text.Encoding.ASCII.GetString(dec, 0, 8) != "AcFssFcA")
        {
            _diagnostics.Add("R2004+ header decryption failed (magic mismatch)");
            return false;
        }


        NumGaps = BitConverter.ToUInt32(dec, 0x3C);
        NumSections = BitConverter.ToUInt32(dec, 0x40);
        SectionMapId = BitConverter.ToUInt32(dec, 0x50);
        SectionMapAddress = BitConverter.ToUInt64(dec, 0x54);
        SectionInfoId = BitConverter.ToUInt32(dec, 0x5C);
        SectionArraySize = BitConverter.ToUInt32(dec, 0x60);
        LastSectionAddress = BitConverter.ToUInt64(dec, 0x2C);

        return true;
    }

    private bool ReadSectionMap()
    {
        if (_raw.Length < 0x120 || SectionMapAddress > (ulong)(_raw.Length - 0x120))
        {
            _diagnostics.Add("Section page map header out of range");
            return false;
        }
        int mapHeader = (int)(SectionMapAddress + 0x100);
        uint stype = BitConverter.ToUInt32(_raw, mapHeader);
        uint decSize = BitConverter.ToUInt32(_raw, mapHeader + 4);
        uint compSize = BitConverter.ToUInt32(_raw, mapHeader + 8);
        uint compType = BitConverter.ToUInt32(_raw, mapHeader + 12);

        if (stype != 0x41630E3Bu)
        {
            _diagnostics.Add($"Invalid section page map type: 0x{stype:X8}");
            return false;
        }

        bool compressed = compType == 2 || compType == 4;
        byte[] mapData;
        if (compressed)
        {
            if (!RsDecoder.TryDecompress(_raw, mapHeader + 0x14, (int)compSize, (int)decSize, out mapData!))
            {
                _diagnostics.Add("Section page map decompression failed");
                return false;
            }
        }
        else
        {
            if (!TryReadRawBlock(mapHeader + 0x14, compSize, "Section page map", out mapData!))
                return false;
        }

        ulong addr = 0x100;
        int pos = 0;
        while (pos + 8 <= mapData.Length)
        {
            int number = BitConverter.ToInt32(mapData, pos);
            uint size = BitConverter.ToUInt32(mapData, pos + 4);
            pos += 8;
            if (number >= 0)
                _sectionRecords.Add(new SectionRecord { Number = number, Size = size, Address = addr });
            if (number <= (int)SectionArraySize)
                addr += size;
            if (number < 0)
                pos += 16;
        }

        return true;
    }

    private bool TryReadRawBlock(int srcOffset, uint declaredSize, string what, out byte[] data)
    {
        data = Array.Empty<byte>();
        if (srcOffset < 0 || srcOffset > _raw.Length || declaredSize > (uint)(_raw.Length - srcOffset))
        {
            _diagnostics.Add($"{what} size {declaredSize} out of file at 0x{srcOffset:X}");
            return false;
        }
        data = new byte[(int)declaredSize];
        Buffer.BlockCopy(_raw, srcOffset, data, 0, (int)declaredSize);
        return true;
    }

    private bool ReadSectionInfo()
    {
        var infoSec = _sectionRecords.FirstOrDefault(s => s.Number == (int)SectionInfoId);
        if (infoSec == null)
        {
            _diagnostics.Add("Section info not found in page map");
            return false;
        }
        if (infoSec.Address + 20 > (ulong)_raw.Length)
        {
            _diagnostics.Add("Section info header out of range");
            return false;
        }
        int sa = (int)infoSec.Address;
        uint siType = BitConverter.ToUInt32(_raw, sa);
        uint siDecomp = BitConverter.ToUInt32(_raw, sa + 4);
        uint siComp = BitConverter.ToUInt32(_raw, sa + 8);
        uint siCompType = BitConverter.ToUInt32(_raw, sa + 12);

        if (siType != 0x4163003Bu)
        {
            _diagnostics.Add($"Invalid section info type: 0x{siType:X8}");
            return false;
        }

        bool compressed = siCompType == 2 || siCompType == 4;
        byte[] secInfo;
        if (compressed)
        {
            if (!RsDecoder.TryDecompress(_raw, sa + 0x14, (int)siComp, (int)siDecomp, out secInfo!))
            {
                _diagnostics.Add("Section info decompression failed");
                return false;
            }
        }
        else
        {
            if (!TryReadRawBlock(sa + 0x14, siComp, "Section info", out secInfo!))
                return false;
        }

        if (secInfo.Length < 20)
        {
            _diagnostics.Add($"Section info stream too short: {secInfo.Length} < 20");
            return false;
        }
        int q = 0;
        uint numDesc = BitConverter.ToUInt32(secInfo, q); q += 4;
         BitConverter.ToUInt32(secInfo, q); q += 4;
         BitConverter.ToUInt32(secInfo, q); q += 4;
         BitConverter.ToUInt32(secInfo, q); q += 4;
         BitConverter.ToUInt32(secInfo, q); q += 4;

        for (int d = 0; d < (int)numDesc && q + 108 <= secInfo.Length; d++)
        {
            var desc = new SectionDescriptor();
            desc.Size = (long)BitConverter.ToUInt64(secInfo, q); q += 8;
            desc.NumPages = (int)BitConverter.ToUInt32(secInfo, q); q += 4;
            desc.MaxDecompSize = BitConverter.ToUInt32(secInfo, q); q += 4;
             BitConverter.ToUInt32(secInfo, q); q += 4;
            desc.CompressedType = (int)BitConverter.ToUInt32(secInfo, q); q += 4;
            desc.Type = BitConverter.ToUInt32(secInfo, q); q += 4;
             BitConverter.ToUInt32(secInfo, q); q += 4;
            int nameLen = 0;
            while (nameLen < 64 && secInfo[q + nameLen] != 0) nameLen++;
            desc.Name = System.Text.Encoding.ASCII.GetString(secInfo, q, nameLen);
            q += 64;

            for (int j = 0; j < desc.NumPages && q + 16 <= secInfo.Length; j++)
            {
                var p = new PageRecord();
                p.Number = BitConverter.ToInt32(secInfo, q);
                p.CompressedSize = BitConverter.ToUInt32(secInfo, q + 4);
                p.Offset = BitConverter.ToUInt64(secInfo, q + 8);
                q += 16;
                desc.Pages.Add(p);
            }
            _descriptors.Add(desc);
        }

        return true;
    }

    public virtual byte[]? DecompressSection(string name)
    {
        var desc = _descriptors.FirstOrDefault(d => d.Name == name);
        if (desc == null || desc.NumPages == 0)
            return null;

        const long maxSectionBytes = RsDecoder.MaxReasonableSize;
        long totalAlloc = (long)desc.NumPages * desc.MaxDecompSize;
        if (totalAlloc <= 0 || totalAlloc > maxSectionBytes)
            totalAlloc = desc.Size > 0 && desc.Size <= maxSectionBytes ? desc.Size : 65536;
        var output = new byte[(int)totalAlloc];
        int maxWritten = 0;
        Span<byte> header = stackalloc byte[32];

        foreach (var page in desc.Pages)
        {
            if (page.Number < 0) continue;
            var sec = _sectionRecords.FirstOrDefault(s => s.Number == page.Number);
            if (sec == null)
            {
                _diagnostics.Add($"{desc.Name}: page {page.Number} 无对应 section 记录，跳过");
                continue;
            }

            if (sec.Address + 32 > (ulong)_raw.Length)
            {
                _diagnostics.Add($"Page section {page.Number} address out of range");
                continue;
            }
            uint address = (uint)sec.Address;

            uint secMask = 0x4164536Bu ^ address;
            for (int i = 0; i < 32; i += 4)
            {
                uint val = BitConverter.ToUInt32(_raw, (int)address + i) ^ secMask;
                header[i + 0] = (byte)val;
                header[i + 1] = (byte)(val >> 8);
                header[i + 2] = (byte)(val >> 16);
                header[i + 3] = (byte)(val >> 24);
            }

            uint pageType = BitConverter.ToUInt32(header);
            uint dataSize = BitConverter.ToUInt32(header.Slice(8));
            uint pageSize = BitConverter.ToUInt32(header.Slice(12));
            uint pageAddr = BitConverter.ToUInt32(header.Slice(16));

            if (pageType != 0x4163043b && pageType != 0x41643b0b && (pageType & 0xFFFF0000) != 0x41630000)
            {
                _diagnostics.Add($"Warning: page_type=0x{pageType:X8} at 0x{address:X}");
            }

            int compOffset = (int)address + 32;
            int compLen = (int)dataSize;
            int outOffset = (int)pageAddr;

            int room = output.Length - outOffset;
            if (outOffset < 0 || room <= 0)
            {
                _diagnostics.Add($"Page output offset {outOffset} out of buffer {output.Length}");
                continue;
            }

            if (desc.CompressedType == 2 || desc.CompressedType == 4)
            {
                if (!RsDecoder.TryDecompressSection(_raw, compOffset, compLen,
                        room, out var pageData, out int produced))
                {
                    _diagnostics.Add($"Page decompression failed at 0x{address:X}");
                    continue;
                }
                int copyLen = Math.Min(produced, room);
                Buffer.BlockCopy(pageData, 0, output, outOffset, copyLen);
                maxWritten = Math.Max(maxWritten, outOffset + copyLen);
            }
            else
            {
                int copyLen = Math.Min(Math.Min(compLen, room), _raw.Length - compOffset);
                if (copyLen < 0)
                {
                    _diagnostics.Add($"Page {page.Number} uncompressed size {dataSize} out of file");
                    continue;
                }
                if (copyLen > 0)
                    Buffer.BlockCopy(_raw, compOffset, output, outOffset, copyLen);
                maxWritten = Math.Max(maxWritten, outOffset + copyLen);
            }
        }

        if (desc.Size > 0 && maxWritten < desc.Size)
            _diagnostics.Add($"{desc.Name}: 解压仅得 {maxWritten} 字节，声明 {desc.Size}");

        int finalSize = desc.Size > 0 ? (int)Math.Min(desc.Size, output.Length) : maxWritten;
        if (finalSize != output.Length)
        {
            var trimmed = new byte[finalSize];
            Buffer.BlockCopy(output, 0, trimmed, 0, finalSize);
            return trimmed;
        }
        return output;
    }

    public List<(string Name, byte[] Data)> GetAllSectionData()
    {
        var result = new List<(string, byte[])>();
        foreach (var desc in _descriptors)
        {
            var data = DecompressSection(desc.Name);
            if (data != null && data.Length > 0)
                result.Add((desc.Name, data));
        }
        return result;
    }


    public List<(ulong Handle, int Offset)> ParseHandlesSection(byte[] handlesData)
    {
        var result = new List<(ulong, int)>();
        if (handlesData == null || handlesData.Length < 4) return result;

        var br = new ByteReader(handlesData);
        br.Seek(2);
        br.Seek(4);

        while (br.Position + 1 < br.Length)
        {
            byte code = br.ReadByte();
            if (code == 0) break;

            ulong handle = 0;
            int bits = (code & 0x3F);
            if (bits == 0)
            {
                handle = br.ReadHandleBits(bits <= 0 ? 16 : bits);
            }
            else
            {
                handle = br.ReadHandleBits(bits);
            }

            result.Add((handle, 0));
        }

        int offsetStart = br.Position;
        int aligned = (offsetStart + 3) & ~3;
        if (aligned < br.Length)
        {
            br.Seek(aligned);
            for (int i = 0; i < result.Count && br.Position + 4 <= br.Length; i++)
            {
                int off = br.ReadInt32();
                result[i] = (result[i].Item1, off);
            }
        }

        return result;
    }

    private class SectionRecord
    {
        public int Number;
        public uint Size;
        public ulong Address;
    }

    public class SectionDescriptor
    {
        public string Name = "";
        public long Size;
        public int NumPages;
        public uint MaxDecompSize;
        public int CompressedType;
        public uint Type;
        public List<PageRecord> Pages = new();
    }

    public class PageRecord
    {
        public int Number;
        public uint CompressedSize;
        public ulong Offset;
    }
}
