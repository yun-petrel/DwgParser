using DwgParser.Core.IO;

namespace DwgParser.Core.Objects.Records;

internal sealed class DetailViewStyleAc1018 : ViewStyleRecord
{
    public DetailViewStyleAc1018(ulong handle, DwgObjectRecord rec, int objStart)
        : base(handle, rec, objStart) { }

    public override string Family => "DetailViewStyleAc1018";

    protected override bool ReadVariant(BitReader br, long hs, long ownStart,
        List<(long Start, long End, string Val)> slots, List<ViewStyleCapture.NumField> nums)
    {
        if (!ReadInlineTv(br, hs, ownStart, slots, "exclude")) return false;
        if (!ReadVsBd(br, hs, nums, "identifier_offset")) return false;
        if (!ReadVsRc(br, hs, nums, "identifier_placement")) return false;
        if (!ReadVsCmc(br, hs, nums, "arrow_symbol_color")) return false;
        if (!ReadVsBd(br, hs, nums, "arrow_symbol_size")) return false;
        if (!ReadVsBl(br, hs, nums, "boundary_linewt")) return false;
        if (!ReadVsCmc(br, hs, nums, "boundary_line_color")) return false;
        if (!ReadVsCmc(br, hs, nums, "viewlabel_text_color")) return false;
        if (!ReadVsBd(br, hs, nums, "viewlabel_text_height")) return false;
        if (!ReadVsBl(br, hs, nums, "viewlabel_attachment")) return false;
        if (!ReadVsBd(br, hs, nums, "viewlabel_offset")) return false;
        if (!ReadVsBl(br, hs, nums, "viewlabel_alignment")) return false;
        if (!ReadInlineTv(br, hs, ownStart, slots, "pattern")) return false;
        return true;
    }
}
