using System.Text;
using DwgParser.Core.Compression;

namespace DwgParser.Core.Format;

public static partial class R2007Writer
{

    static void ComputeLayout(List<SectionPlan> plans, out long pmPageSize, out long smPageSize,
        out ulong previewAddr, out ulong summaryAddr, out ulong appInfoAddr,
        out ulong headerCopyAddr, out long fileSize)
    {
        long dataPages = CountPages(plans, 0, plans.Count);
        pmPageSize = SystemPageSize((dataPages + 4) * 16);
        long smUncompLen = SectionsMapSize(plans);
        smPageSize = SystemPageSize(smUncompLen);

        ulong addr = DataAreaStart;
        addr += (ulong)(pmPageSize * 2);
        foreach (var p in EnumeratePages(plans))
        {
            p.Address = addr;
            addr += (ulong)p.Disk.Length;
        }
        addr += (ulong)(smPageSize * 2);
        headerCopyAddr = addr;
        fileSize = (long)addr + HeaderPageSize;

        previewAddr = GetFirstPageAddress(plans, "AcDb:Preview");
        summaryAddr = GetFirstPageAddress(plans, "AcDb:SummaryInfo");
        appInfoAddr = GetFirstPageAddress(plans, "AcDb:AppInfo");
    }

    static long SectionsMapSize(List<SectionPlan> plans)
    {
        long size = 64;
        foreach (var p in plans)
            size += 64 + (p.Input.Name.Length + 1) * 2 + p.Pages.Count * 56;
        return size;
    }

    static long CountPages(List<SectionPlan> plans, int from, int to)
    {
        long n = 0;
        for (int i = from; i < to; i++) n += plans[i].Pages.Count;
        return n;
    }

    static int FindSection(List<SectionPlan> plans, string name)
    {
        for (int i = 0; i < plans.Count; i++)
        {
            if (string.Equals(plans[i].Input.Name, name, StringComparison.OrdinalIgnoreCase))
                return i;
        }
        return -1;
    }

    static IEnumerable<PagePlan> EnumeratePages(List<SectionPlan> plans)
    {
        foreach (var p in plans)
            foreach (var pg in p.Pages)
                yield return pg;
    }

    static ulong GetFirstPageAddress(List<SectionPlan> plans, string name)
    {
        int i = FindSection(plans, name);
        return i >= 0 && plans[i].Pages.Count > 0 ? plans[i].Pages[0].Address : 0;
    }

    static ulong GetFirstPageAddress(R2007Container.Model model, string name)
    {
        foreach (var s in model.Sections)
        {
            if (!string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase) || s.Pages.Count == 0)
                continue;
            if (R2007Container.TryGetPageAddressPublic(model, s.Pages[0].Number, out ulong addr))
                return addr;
        }
        return 0;
    }

    static void PatchPreviewDescriptors(byte[] content, ulong oldAddr, ulong newAddr)
    {
        if (content.Length < 26) return;
        int count = content[20];
        long delta = (long)newAddr - (long)oldAddr;
        for (int i = 0; i < count; i++)
        {
            int o = 21 + i * 9;
            if (o + 9 > content.Length) break;
            long start = BitConverter.ToUInt32(content, o + 1);
            start += delta;
            PutU32(content, o + 1, (uint)start);
        }
    }

    static long Align8(long v) => (v + 7) & ~7L;
    static long Align32(long v) => (v + 31) & ~31L;

    static void PutU64(byte[] b, int o, ulong v) => BitConverter.GetBytes(v).CopyTo(b, o);
    static void PutU32(byte[] b, int o, uint v) => BitConverter.GetBytes(v).CopyTo(b, o);
}
