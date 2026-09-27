using System.Text;
using DwgParser.Core.Compression;

namespace DwgParser.Core.Format;

public static partial class R2007Writer
{
    const int DataAreaStart = R2007Container.DataAreaStart;
    const int SysDataBytes = 239;
    const int DataDataBytes = 251;
    const int RsN = 255;
    const int HeaderPageSize = 0x400;
    const int HeaderRsFactor = 3;
    const int HeaderRsBytes = HeaderRsFactor * RsN;
    const int HeaderRsData = 717;
    const int CheckDataSize = 0x28;
    const int MetadataSize = 0x110;

    public sealed class SectionInput
    {
        public string Name = "";
        public byte[] Content = Array.Empty<byte>();
        public long MaxSize;
        public long Encrypted;
        public long Hashcode;
        public long Unknown;
        public long Encoded;
    }

    sealed class PagePlan
    {
        public long Id;
        public ulong Offset;
        public byte[] Uncomp = Array.Empty<byte>();
        public ulong CompSize;
        public ulong Checksum;
        public ulong Crc;
        public byte[] Disk = Array.Empty<byte>();
        public ulong Address;
    }

    sealed class SectionPlan
    {
        public SectionInput Input = new();
        public readonly List<PagePlan> Pages = new();
    }


    public static bool TryBuildFromSource(byte[] source, out byte[] output, out string? error)
    {
        output = Array.Empty<byte>();
        error = null;

        if (!R2007Container.TryLoad(source, out var model) || model is null || model.Sections.Count == 0)
        {
            error = model is null
                ? "R2007Container 解析失败"
                : model.Sections.Count == 0 ? "R2007 容器无节目录" : string.Join("; ", model.Diagnostics);
            return false;
        }

        var sections = new List<SectionInput>();
        foreach (var src in model.Sections)
        {
            var content = R2007Container.ReadSection(source, model, src.Name);
            if (content == null)
            {
                error = $"节 {src.Name} 解压失败，无法真写出";
                return false;
            }
            sections.Add(new SectionInput
            {
                Name = src.Name,
                Content = content,
                MaxSize = src.MaxSize,
                Encrypted = src.Encrypted,
                Hashcode = src.Hashcode,
                Unknown = src.Unknown,
                Encoded = src.Encoded,
            });
        }

        ulong previewOld = GetFirstPageAddress(model, "AcDb:Preview");
        return TryBuildFromSections(sections, source,
            model.Header.RandomSeed, model.Header.CrcSeed, previewOld, out output, out error);
    }

    public static bool TryBuildCrossVersion(IList<(string Name, byte[] Data)> sections,
        out byte[] output, out string? error)
    {
        output = Array.Empty<byte>();
        error = null;
        if (sections.Count == 0) { error = "跨版本→AC1021：无节可写"; return false; }

        var inputs = R2007Template.BuildInputs(sections);
        if (inputs.Count == 0) { error = "跨版本→AC1021：节内容为空，无法装配容器"; return false; }
        return TryBuildFromSections(inputs, R2007Template.PlainHeader,
            DeriveSeed(inputs), 0, previewOldAddr: 0, out output, out error);
    }

    static ulong DeriveSeed(List<SectionInput> inputs)
    {
        ulong h = 0xCBF29CE484222325UL;
        void Mix(ulong v)
        {
            for (int i = 0; i < 8; i++) { h ^= (byte)(v >> (8 * i)); h *= 0x100000001B3UL; }
        }
        foreach (var s in inputs)
        {
            foreach (var b in System.Text.Encoding.UTF8.GetBytes(s.Name)) { h ^= b; h *= 0x100000001B3UL; }
            Mix((ulong)s.Content.Length);
            Mix((ulong)s.Encoded);
            for (int i = 0; i < s.Content.Length; i += 4093) { h ^= s.Content[i]; h *= 0x100000001B3UL; }
        }
        h ^= h >> 30; h *= 0xBF58476D1CE4E5B9UL;
        h ^= h >> 27; h *= 0x94D049BB133111EBUL;
        return (h ^ (h >> 31)) | 1;
    }

    public static bool TryBuildFromSections(IList<SectionInput> sections, byte[] plaintext,
        ulong randomSeed, ulong crcSeed, ulong previewOldAddr, out byte[] output, out string? error,
        bool rawStorage = false)
    {
        output = Array.Empty<byte>();
        error = null;
        if (sections.Count == 0) { error = "无节可写"; return false; }

        var rng = new CrcRandomEncoder(randomSeed);

        var plans = new List<SectionPlan>();
        long nextId = 3;
        foreach (var input in sections)
        {
            var plan = new SectionPlan { Input = input };
            BuildSectionPages(plan, nextId, rng, out long used, rawStorage);
            nextId += used;
            plans.Add(plan);
        }

        ComputeLayout(plans, out long pmPageSize0, out long smPageSize0,
            out ulong previewAddr0, out _, out _, out _, out _);
        int previewIdx = FindSection(plans, "AcDb:Preview");
        if (previewIdx >= 0 && previewOldAddr != 0 && previewAddr0 != previewOldAddr)
        {
            PatchPreviewDescriptors(plans[previewIdx].Input.Content, previewOldAddr, previewAddr0);
            var previewPlan = plans[previewIdx];
            previewPlan.Pages.Clear();
            BuildSectionPages(previewPlan, 3 + CountPages(plans, 0, previewIdx), rng, out _, rawStorage);
        }

        byte[] smUncomp = BuildSectionsMap(plans);
        var smPage = BuildSystemPage(smUncomp, rng);

        long dataPages = CountPages(plans, 0, plans.Count);
        long sm1Id = 3 + dataPages, sm2Id = sm1Id + 1, pm1Id = sm1Id + 4, pm2Id = pm1Id + 1;
        byte[] pmUncomp = BuildPagesMap(plans, smPage.PageSize, pm1Id, pm2Id, sm1Id, sm2Id);
        var pmPage = BuildSystemPage(pmUncomp, rng);

        ComputeLayout(plans, out long pmPageSize, out long smPageSize,
            out ulong previewAddr, out ulong summaryAddr, out ulong appInfoAddr,
            out ulong headerCopyAddr, out long fileSize);
        pmPage.Address1 = DataAreaStart;
        pmPage.Address2 = DataAreaStart + (ulong)pmPageSize;
        smPage.Address1 = headerCopyAddr - (ulong)(smPageSize * 2);
        smPage.Address2 = smPage.Address1 + (ulong)smPageSize;

        ulong smSeed = rng.EncodeCrcSeed(crcSeed);
        ulong pmSeed = rng.EncodeCrcSeed(crcSeed);
        ulong r1 = rng.NextU64();
        ulong r2 = rng.NextU64();
        ulong checkSeedEnc = rng.EncodeCrcSeed(crcSeed);
        ulong crcSeedEnc = rng.EncodeCrcSeed(crcSeed);
        byte[] checkData = BuildCheckData(r1, r2, checkSeedEnc);

        var meta = new byte[MetadataSize];
        PutU64(meta, 0x00, 0x70);
        PutU64(meta, 0x08, (ulong)fileSize);
        PutU64(meta, 0x10, pmPage.CrcComp);
        PutU64(meta, 0x18, (ulong)pmPage.Correction);
        PutU64(meta, 0x20, pmSeed);
        PutU64(meta, 0x28, (ulong)pmPage.Address2 - DataAreaStart);
        PutU64(meta, 0x30, (ulong)pm2Id);
        PutU64(meta, 0x38, 0);
        PutU64(meta, 0x40, (ulong)pm1Id);
        PutU64(meta, 0x48, headerCopyAddr - DataAreaStart);
        PutU64(meta, 0x50, (ulong)pmPage.Comp);
        PutU64(meta, 0x58, (ulong)pmPage.Uncomp);
        PutU64(meta, 0x60, (ulong)(dataPages + 4));
        PutU64(meta, 0x68, (ulong)pm2Id);
        PutU64(meta, 0x70, 0x20);
        PutU64(meta, 0x78, 0x40);
        PutU64(meta, 0x80, pmPage.CrcUncomp);
        PutU64(meta, 0x88, 0xF800);
        PutU64(meta, 0x90, 4);
        PutU64(meta, 0x98, 1);
        PutU64(meta, 0xA0, (ulong)(plans.Count + 1));
        PutU64(meta, 0xA8, smPage.CrcUncomp);
        PutU64(meta, 0xB0, (ulong)smPage.Comp);
        PutU64(meta, 0xB8, (ulong)sm2Id);
        PutU64(meta, 0xC0, (ulong)sm1Id);
        PutU64(meta, 0xC8, (ulong)smPage.Uncomp);
        PutU64(meta, 0xD0, smPage.CrcComp);
        PutU64(meta, 0xD8, (ulong)smPage.Correction);
        PutU64(meta, 0xE0, smSeed);
        PutU64(meta, 0xE8, 0x60100);
        PutU64(meta, 0xF0, crcSeed);
        PutU64(meta, 0xF8, crcSeedEnc);
        PutU64(meta, 0x100, randomSeed);
        PutU64(meta, 0x108, R2007Crc64.HeaderCrc64(meta));

        byte[] headerPage = BuildHeaderPage(meta, rng, checkData);

        var plain = new byte[0x80];
        Buffer.BlockCopy(plaintext, 0, plain, 0, Math.Min(0x80, plaintext.Length));
        PutU32(plain, 0x0D, (uint)previewAddr);
        PutU32(plain, 0x20, (uint)summaryAddr);
        PutU32(plain, 0x24, 0);
        PutU32(plain, 0x2C, (uint)appInfoAddr);

        var file = new byte[fileSize];
        Buffer.BlockCopy(plain, 0, file, 0, 0x80);
        Buffer.BlockCopy(headerPage, 0, file, 0x80, HeaderPageSize);
        Buffer.BlockCopy(pmPage.Page!, 0, file, (int)pmPage.Address1, pmPage.Page!.Length);
        Buffer.BlockCopy(pmPage.Page, 0, file, (int)pmPage.Address2, pmPage.Page.Length);
        foreach (var p in EnumeratePages(plans))
            Buffer.BlockCopy(p.Disk, 0, file, (int)p.Address, p.Disk.Length);
        Buffer.BlockCopy(smPage.Page!, 0, file, (int)smPage.Address1, smPage.Page!.Length);
        Buffer.BlockCopy(smPage.Page, 0, file, (int)smPage.Address2, smPage.Page.Length);
        Buffer.BlockCopy(headerPage, 0, file, (int)headerCopyAddr, HeaderPageSize);

        output = file;
        return true;
    }


    public static bool TryRebuildFromIndex(
        byte[] source, out byte[] output, out string? error)
    {
        output = Array.Empty<byte>();
        error = null;

        if (!R2007Container.TryLoad(source, out var model) || model == null)
        {
            error = model is null
                ? "R2007Container 解析失败"
                : string.Join("; ", model.Diagnostics);
            return false;
        }

        if (model.Pages.Count == 0 || model.Sections.Count == 0)
        {
            error = $"R2007 索引不完整：pages={model.Pages.Count} sections={model.Sections.Count}";
            return false;
        }

        int total = source.Length;
        var file = new byte[total];

        Buffer.BlockCopy(source, 0, file, 0, Math.Min(0x80, total));

        int das = R2007Container.DataAreaStart;
        if (das > 0x80 && das <= total)
            Buffer.BlockCopy(source, 0x80, file, 0x80, das - 0x80);

        long covered = das;
        foreach (var pg in model.Pages)
        {
            long start = (long)pg.Address;
            long size = pg.Size;
            if (start < das) continue;
            if (start + size > total) continue;
            if (start > covered)
                Buffer.BlockCopy(source, (int)covered, file, (int)covered,
                    (int)Math.Min(start - covered, total - covered));
            Buffer.BlockCopy(source, (int)start, file, (int)start, (int)size);
            covered = start + size;
        }
        if (covered < total)
            Buffer.BlockCopy(source, (int)covered, file, (int)covered, (int)(total - covered));

        output = file;
        return true;
    }

}
