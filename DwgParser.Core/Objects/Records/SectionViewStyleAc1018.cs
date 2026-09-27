using DwgParser.Core.IO;

namespace DwgParser.Core.Objects.Records;

internal sealed class SectionViewStyleAc1018 : ViewStyleRecord
{
    public SectionViewStyleAc1018(ulong handle, DwgObjectRecord rec, int objStart)
        : base(handle, rec, objStart) { }

    public override string Family => "SectionViewStyleAc1018";

    protected override bool ReadVariant(BitReader br, long hs, long ownStart,
        List<(long Start, long End, string Val)> slots, List<ViewStyleCapture.NumField> nums)
    {
        if (!ReadVsCmc(br, hs, nums, "arrow_symbol_color")) return false;
        if (!ReadVsBd(br, hs, nums, "arrow_symbol_size")) return false;
        if (!ReadInlineTv(br, hs, ownStart, slots, "exclude")) return false;
        if (!ReadVsBd(br, hs, nums, "n1")) return false;
        if (!ReadVsBl(br, hs, nums, "n2")) return false;
        if (!ReadVsCmc(br, hs, nums, "n3")) return false;
        if (!ReadVsBl(br, hs, nums, "n4")) return false;
        if (!ReadVsCmc(br, hs, nums, "n5")) return false;
        if (!ReadVsBd(br, hs, nums, "n6")) return false;
        if (!ReadVsBd(br, hs, nums, "n7")) return false;
        if (!ReadVsCmc(br, hs, nums, "n8")) return false;
        if (!ReadVsBd(br, hs, nums, "n9")) return false;
        if (!ReadVsBl(br, hs, nums, "n10")) return false;
        if (!ReadVsBd(br, hs, nums, "n11")) return false;
        if (!ReadVsBl(br, hs, nums, "n12")) return false;
        if (!ReadInlineTv(br, hs, ownStart, slots, "pattern")) return false;
        if (!ReadVsCmc(br, hs, nums, "hatch_color")) return false;
        if (!ReadVsCmc(br, hs, nums, "hatch_bg_color")) return false;
        if (!ReadInlineTv(br, hs, ownStart, slots, "hatch")) return false;
        return true;
    }
}
