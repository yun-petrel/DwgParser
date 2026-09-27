using DwgParser.Core.Format;
using DwgParser.Core.IO;

namespace DwgParser.Core.Objects.Records;

public abstract class ViewStyleRecord : IViewStyle
{
    protected readonly DwgObjectRecord Rec;
    protected readonly int ObjStart;
    protected readonly ulong Handle;

    protected ViewStyleRecord(ulong handle, DwgObjectRecord rec, int objStart)
    {
        Handle = handle;
        Rec = rec;
        ObjStart = objStart;
        Cap = rec.ViewStyle ??= new ViewStyleCapture();
        Cap.AttachIdentity(rec);
    }

    public ViewStyleCapture Cap { get; }

    public abstract string Family { get; }

    ulong IRecord.Handle => Handle;
    public short TypeCode => Rec.TypeCode;
    public DwgVersion SourceVersion => Rec.SourceVersion;
    public IReadOnlyList<string> InlineStrings => Cap.Values;
    public bool Captured => Cap.Captured;

    internal virtual void Read(BitReader br)
    {
        DwgObjectRecord.SeekTo(br, Rec.CarryStartBit);
        uint bitsize = br.ReadRL();
        long hs = (long)ObjStart * 8 + bitsize;
        if (hs <= br.BitPosition || hs > (long)Rec.BodyEnd * 8) { Fail(br, "帧界(RL vs BodyEnd)"); return; }
        long ownStart = br.BitPosition;
        br.ReadHandle();
        if (!DwgObjectReader.TrySkipEed(br)) { Fail(br, "EED"); return; }
        int nr = br.ReadBL();
        if (nr is < 0 or > 8) { Fail(br, $"num_reactors={nr}"); return; }
        br.ReadB();
        int mdocClassVersion = br.ReadBS();
        if (mdocClassVersion != 0) { Fail(br, $"mdoc_class_version={mdocClassVersion}"); return; }
        var slots = new List<(long Start, long End, string Val)>();
        var nums = new List<ViewStyleCapture.NumField>();
        if (!ReadInlineTv(br, hs, ownStart, slots, "desc")) { Fail(br, "内联串 desc"); return; }
        br.ReadB();
        int classVersion = br.ReadBS();
        if (classVersion != 0) { Fail(br, $"class_version={classVersion}"); return; }
        if (!ReadVsBl(br, hs, nums, "flags")) { Fail(br, "flags"); return; }
        if (!ReadVsCmc(br, hs, nums, "identifier_color")) { Fail(br, "identifier_color"); return; }
        if (!ReadVsBd(br, hs, nums, "identifier_height")) { Fail(br, "identifier_height"); return; }

        if (!ReadVariant(br, hs, ownStart, slots, nums)) { Fail(br, "差异段(ReadVariant)"); return; }

        if (System.Environment.GetEnvironmentVariable("DWGDBG") == "1")
            System.Console.Error.WriteLine(
                $"VSR h={Rec.Handle:X} t={Rec.TypeCode} {Family} hs={hs} own={ownStart} slots=["
                + string.Join(", ", slots.Select(s => $"'{s.Val}'[{s.Start - ownStart}..{s.End - ownStart}]")) + "]");

        Rec.HandleStreamStartBit = hs;
        Rec.HasHandleStream = true;
        Cap.HandleStreamStartBit = hs;
        foreach (var s in slots)
        {
            Rec.StringFields.Add(new DwgObjectRecord.StringField(s.Start, (int)(s.End - s.Start), s.Val));
            Cap.Slots.Add(new ViewStyleCapture.Slot(s.Start, s.End, s.Val));
        }
        Cap.Nums.AddRange(nums);
        Cap.Captured = true;
    }

    protected void Fail(BitReader br, string why)
    {
        Cap.FailReason = why;
        Cap.FailBit = br.BitPosition - Rec.CarryStartBit;
    }

    protected virtual bool ReadVariant(BitReader br, long hs, long ownStart,
        List<(long Start, long End, string Val)> slots, List<ViewStyleCapture.NumField> nums) => false;



    private static readonly System.Globalization.CultureInfo Inv =
        System.Globalization.CultureInfo.InvariantCulture;

    private static void AddNum(List<ViewStyleCapture.NumField> nums,
        string tag, string kind, long raw, string text)
        => nums.Add(new ViewStyleCapture.NumField(tag, kind, raw, text));


    protected static bool ReadInlineTv(BitReader br, long hs, long ownStart,
        List<(long Start, long End, string Val)> slots, string tag)
    {
        long start = br.BitPosition;
        if (start < ownStart || start >= hs) return false;
        string v = br.ReadTV();
        long end = br.BitPosition;
        if (end <= start || end > hs) return false;
        if (v.Length == 0 && end - start != 2) return false;
        slots.Add((start, end, v));
        return true;
    }

    protected static bool ReadVsCmc(BitReader br, long hs)
        => ReadVsCmc(br, hs, out _, out _, out _);

    protected static bool ReadVsCmc(BitReader br, long hs, out short idx, out uint rgb, out byte flag)
    {
        idx = br.ReadBS();
        rgb = unchecked((uint)br.ReadBL());
        flag = br.ReadRC();
        if (idx < 0 || idx > 256 || flag != 0) return false;
        return br.BitPosition < hs;
    }

    protected static bool ReadVsCmc(BitReader br, long hs, List<ViewStyleCapture.NumField> nums, string tag)
    {
        if (!ReadVsCmc(br, hs, out short idx, out uint rgb, out byte flag)) return false;
        AddNum(nums, tag, "CMC",
            ((long)idx << 40) | ((long)flag << 32) | rgb,
            $"idx={idx},rgb=0x{rgb:X8},flag={flag}");
        return true;
    }

    protected static bool ReadVsBd(BitReader br, long hs) => ReadVsBd(br, hs, out _);

    protected static bool ReadVsBd(BitReader br, long hs, out double v)
    {
        v = br.ReadBD();
        return !double.IsNaN(v) && !double.IsInfinity(v) && br.BitPosition < hs;
    }

    protected static bool ReadVsBd(BitReader br, long hs, List<ViewStyleCapture.NumField> nums, string tag)
    {
        if (!ReadVsBd(br, hs, out double v)) return false;
        AddNum(nums, tag, "BD", BitConverter.DoubleToInt64Bits(v), v.ToString("R", Inv));
        return true;
    }

    protected static bool ReadVsBl(BitReader br, long hs) => ReadVsBl(br, hs, out _);

    protected static bool ReadVsBl(BitReader br, long hs, out int v)
    {
        v = br.ReadBL();
        return v >= 0 && br.BitPosition < hs;
    }

    protected static bool ReadVsBl(BitReader br, long hs, List<ViewStyleCapture.NumField> nums, string tag)
    {
        if (!ReadVsBl(br, hs, out int v)) return false;
        AddNum(nums, tag, "BL", v, v.ToString(Inv));
        return true;
    }

    protected static bool ReadVsRc(BitReader br, long hs) => ReadVsRc(br, hs, out _);

    protected static bool ReadVsRc(BitReader br, long hs, out byte v)
    {
        v = br.ReadRC();
        return br.BitPosition < hs;
    }

    protected static bool ReadVsRc(BitReader br, long hs, List<ViewStyleCapture.NumField> nums, string tag)
    {
        if (!ReadVsRc(br, hs, out byte v)) return false;
        AddNum(nums, tag, "RC", v, v.ToString(Inv));
        return true;
    }
}
