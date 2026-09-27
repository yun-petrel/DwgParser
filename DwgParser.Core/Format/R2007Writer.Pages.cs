using System.Text;
using DwgParser.Core.Compression;

namespace DwgParser.Core.Format;

public static partial class R2007Writer
{
    static void BuildSectionPages(SectionPlan plan, long startId, CrcRandomEncoder rng,
        out long used, bool rawStorage = false)
    {
        used = 0;
        long capacity = plan.Input.MaxSize > 0 ? plan.Input.MaxSize : 63488;
        long id = startId;
        for (long off = 0; off < plan.Input.Content.Length; off += capacity)
        {
            int len = (int)Math.Min(capacity, plan.Input.Content.Length - off);
            var slice = new byte[len];
            Buffer.BlockCopy(plan.Input.Content, (int)off, slice, 0, len);
            plan.Pages.Add(BuildDataPage(slice, id++, (ulong)off, rng, plan.Input.Encoded, rawStorage));
            used++;
        }
    }

    static PagePlan BuildDataPage(byte[] uncomp, long id, ulong offset, CrcRandomEncoder rng,
        long encoded, bool rawStorage = false)
    {
        bool rs = encoded == 4;
        byte[] stream = (rs && !rawStorage) ? Lz77Ac21Compress.Compress(uncomp) : uncomp;
        bool store = stream.Length >= uncomp.Length;
        byte[] stored = store ? uncomp : stream;
        long comp = stored.Length;
        byte[] disk;

        if (!rs)
        {
            disk = new byte[Align32(comp)];
            Buffer.BlockCopy(stored, 0, disk, 0, (int)comp);
        }
        else
        {
            long a8 = Align8(comp);
            long factor = (a8 + DataDataBytes - 1) / DataDataBytes;
            var pre = new byte[factor * DataDataBytes];
            Buffer.BlockCopy(stored, 0, pre, 0, (int)comp);
            if (pre.Length > a8)
            {
                var fill = new byte[pre.Length - a8];
                rng.FillRandom(fill);
                Buffer.BlockCopy(fill, 0, pre, (int)a8, fill.Length);
            }
            var rsBytes = ReedSolomonAc21.Encode(pre, (int)factor, DataDataBytes, ReedSolomonAc21.DataPrimPoly);
            disk = new byte[Align32(rsBytes.Length)];
            Buffer.BlockCopy(rsBytes, 0, disk, 0, rsBytes.Length);
        }

        return new PagePlan
        {
            Id = id,
            Offset = offset,
            Uncomp = uncomp,
            CompSize = (ulong)comp,
            Checksum = R2007Crc64.PageChecksum(0, uncomp),
            Crc = R2007Crc64.MirroredCrc64(0, (uint)comp, stored),
            Disk = disk,
        };
    }

    sealed class SystemPage
    {
        public byte[] Page = Array.Empty<byte>();
        public long PageSize;
        public long Comp;
        public long Uncomp;
        public int Correction;
        public ulong CrcComp;
        public ulong CrcUncomp;
        public ulong Address1;
        public ulong Address2;
    }

    static SystemPage BuildSystemPage(byte[] uncomp, CrcRandomEncoder rng)
    {
        byte[] stream = Lz77Ac21Compress.Compress(uncomp);
        bool raw = stream.Length >= uncomp.Length;
        byte[] stored = raw ? uncomp : stream;
        long comp = stored.Length;
        long pageSize = SystemPageSize(uncomp.Length);
        long a8 = Align8(comp);
        long cf = Math.Max(2, (pageSize / RsN) * SysDataBytes / a8);
        long pesize = a8 * cf;
        long blocks = (pesize + SysDataBytes - 1) / SysDataBytes;

        var pre = new byte[blocks * SysDataBytes];
        for (long i = 0; i < cf; i++)
        {
            int dst = (int)(i * a8);
            int take = (int)Math.Min(comp, pre.Length - dst);
            if (take <= 0) break;
            Buffer.BlockCopy(stored, 0, pre, dst, take);
        }
        if (pre.Length > pesize)
        {
            var fill = new byte[pre.Length - pesize];
            rng.FillRandom(fill);
            Buffer.BlockCopy(fill, 0, pre, (int)pesize, fill.Length);
        }

        var rs = ReedSolomonAc21.Encode(pre, (int)blocks, SysDataBytes, ReedSolomonAc21.SystemPrimPoly);
        var page = new byte[pageSize];
        Buffer.BlockCopy(rs, 0, page, 0, Math.Min(rs.Length, page.Length));

        return new SystemPage
        {
            Page = page,
            PageSize = pageSize,
            Comp = comp,
            Uncomp = uncomp.Length,
            Correction = (int)cf,
            CrcComp = R2007Crc64.MirroredCrc64(0, (uint)comp, stored),
            CrcUncomp = R2007Crc64.MirroredCrc64(0, (uint)uncomp.Length, uncomp),
        };
    }

    static long SystemPageSize(long uncompLen)
    {
        long a8 = Align8(uncompLen);
        long need = (a8 * 2 + SysDataBytes - 1) / SysDataBytes * RsN;
        return Math.Max(0x400, Align32(need));
    }
}
