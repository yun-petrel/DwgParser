using System.Text;
using DwgParser.Core.Compression;

namespace DwgParser.Core.Format;

public static partial class R2007Writer
{

    static byte[] BuildSectionsMap(List<SectionPlan> plans)
    {
        long size = 0;
        foreach (var p in plans)
        {
            size += 64 + (p.Input.Name.Length + 1) * 2 + p.Pages.Count * 56;
        }
        size += 64;
        var buf = new byte[size];
        int q = 0;

        foreach (var p in plans)
        {
            var nameBytes = Encoding.Unicode.GetBytes(p.Input.Name + "\0");
            PutU64(buf, q, (ulong)p.Input.Content.Length);
            PutU64(buf, q + 8, (ulong)p.Input.MaxSize);
            PutU64(buf, q + 16, (ulong)p.Input.Encrypted);
            PutU64(buf, q + 24, (ulong)p.Input.Hashcode);
            PutU64(buf, q + 32, (ulong)nameBytes.Length);
            PutU64(buf, q + 40, (ulong)p.Input.Unknown);
            PutU64(buf, q + 48, (ulong)p.Input.Encoded);
            PutU64(buf, q + 56, (ulong)p.Pages.Count);
            q += 64;
            Buffer.BlockCopy(nameBytes, 0, buf, q, nameBytes.Length);
            q += nameBytes.Length;
            foreach (var pg in p.Pages)
            {
                PutU64(buf, q, pg.Offset);
                PutU64(buf, q + 8, (ulong)p.Input.MaxSize);
                PutU64(buf, q + 16, (ulong)pg.Id);
                PutU64(buf, q + 24, (ulong)pg.Uncomp.Length);
                PutU64(buf, q + 32, pg.CompSize);
                PutU64(buf, q + 40, pg.Checksum);
                PutU64(buf, q + 48, pg.Crc);
                q += 56;
            }
        }

        PutU64(buf, q + 8, 0xF800);
        PutU64(buf, q + 48, 4);
        return buf;
    }

    static byte[] BuildPagesMap(List<SectionPlan> plans, long smPageSize,
        long pm1Id, long pm2Id, long sm1Id, long sm2Id)
    {
        long dataPages = CountPages(plans, 0, plans.Count);
        long pmPageSize = SystemPageSize((dataPages + 4) * 16);
        var buf = new byte[(dataPages + 4) * 16];
        int q = 0;
        void Entry(long sz, long id) { PutU64(buf, q, (ulong)sz); PutU64(buf, q + 8, (ulong)id); q += 16; }
        Entry(pmPageSize, pm1Id);
        Entry(pmPageSize, pm2Id);
        foreach (var pg in EnumeratePages(plans))
            Entry(pg.Disk.Length, pg.Id);
        Entry(smPageSize, sm1Id);
        Entry(smPageSize, sm2Id);
        return buf;
    }


    static byte[] BuildHeaderPage(byte[] meta, CrcRandomEncoder rng, byte[] checkData)
    {
        byte[] compr = Lz77Ac21Compress.Compress(meta);
        bool useComp = compr.Length < meta.Length;
        byte[] comprData = useComp ? compr : meta;
        int comprLen = useComp ? comprData.Length : -meta.Length;
        ulong comprCrc = R2007Crc64.NormalCrc64(0, (uint)comprData.Length, comprData);

        ulong v1 = rng.NextU64();
        ulong v2 = EncodeValue(v1, v1);
        var seqBytes = new byte[16];
        PutU64(seqBytes, 0, v1);
        PutU64(seqBytes, 8, v2);
        ulong seqCrc = R2007Crc64.NormalCrc64Seed1(0, 16, seqBytes);

        var block = new List<byte>(64);
        void AddU64(ulong v) { block.AddRange(BitConverter.GetBytes(v)); }
        AddU64(seqCrc);
        AddU64(v1);
        AddU64(comprCrc);
        block.AddRange(BitConverter.GetBytes(comprLen));
        block.AddRange(BitConverter.GetBytes(0));
        block.AddRange(comprData);
        int padded = (int)Align8(block.Count);
        if (block.Count < padded)
        {
            var pad = new byte[padded - block.Count];
            rng.FillRandom(pad);
            block.AddRange(pad);
        }

        var preRs = new byte[HeaderRsData];
        int copies = HeaderRsData / block.Count;
        int offset = 0;
        for (int i = 0; i < copies; i++)
        {
            block.CopyTo(0, preRs, offset, block.Count);
            offset += block.Count;
        }
        if (offset < HeaderRsData)
        {
            var fill = new byte[HeaderRsData - offset];
            rng.FillRandom(fill);
            Buffer.BlockCopy(fill, 0, preRs, offset, fill.Length);
        }

        var rs = ReedSolomonAc21.Encode(preRs, HeaderRsFactor, SysDataBytes, ReedSolomonAc21.SystemPrimPoly);
        var page = new byte[HeaderPageSize];
        Buffer.BlockCopy(rs, 0, page, 0, Math.Min(rs.Length, HeaderRsBytes));
        var mid = new byte[HeaderPageSize - CheckDataSize - HeaderRsBytes];
        rng.FillRandom(mid);
        Buffer.BlockCopy(mid, 0, page, HeaderRsBytes, mid.Length);
        Buffer.BlockCopy(checkData, 0, page, HeaderPageSize - CheckDataSize, CheckDataSize);
        return page;
    }

    static byte[] BuildCheckData(ulong r1, ulong r2, ulong seedEnc)
    {
        var nb = new ulong[8];
        nb[0] = EncodeValue(r1, r2);
        nb[1] = EncodeValue(nb[0], nb[0]);
        nb[2] = EncodeValue(r2, nb[1]);
        nb[3] = EncodeValue(nb[2], nb[2]);
        nb[4] = EncodeValue(r1, nb[3]);
        nb[5] = EncodeValue(nb[4], nb[4]);
        nb[6] = EncodeValue(nb[5], nb[5]);
        nb[7] = EncodeValue(nb[6], nb[6]);
        ulong normalCrc = R2007Crc64.CheckDataNormalCrc64(r2, ToBytes(nb));

        var mb = new ulong[8];
        mb[0] = EncodeValue(r1, r2);
        mb[1] = EncodeValue(normalCrc, nb[0]);
        mb[2] = EncodeValue(r2, mb[1]);
        mb[3] = EncodeValue(normalCrc, mb[2]);
        mb[4] = EncodeValue(r1, mb[3]);
        mb[5] = EncodeValue(normalCrc, mb[4]);
        mb[6] = EncodeValue(r2, mb[5]);
        mb[7] = EncodeValue(mb[6], mb[6]);
        ulong mirroredCrc = R2007Crc64.CheckDataMirroredCrc64(r1, ToBytes(mb));

        var data = new byte[CheckDataSize];
        PutU64(data, 0, normalCrc);
        PutU64(data, 8, mirroredCrc);
        PutU64(data, 16, r1);
        PutU64(data, 24, r2);
        PutU64(data, 32, seedEnc);
        return data;
    }

    static ulong EncodeValue(ulong value, ulong control)
    {
        uint shift = (uint)(control & 0x1F);
        return shift == 0 ? value : (value << (int)shift) | (value >> (64 - (int)shift));
    }

    static byte[] ToBytes(ulong[] values)
    {
        var bytes = new byte[values.Length * 8];
        for (int i = 0; i < values.Length; i++)
            PutU64(bytes, i * 8, values[i]);
        return bytes;
    }
}
