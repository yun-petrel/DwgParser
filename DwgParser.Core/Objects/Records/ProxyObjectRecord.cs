using DwgParser.Core.Format;
using DwgParser.Core.IO;

namespace DwgParser.Core.Objects.Records;

public abstract class ProxyObjectRecord : IRecord
{
    public const short TypeCodeValue = 499;

    protected readonly int ObjStart;

    protected readonly DwgObjectRecord Rec;

    protected readonly ulong Handle;

    protected readonly IReadOnlyList<R2000Class>? Classes;

    protected ProxyObjectRecord(ulong handle, DwgObjectRecord rec, int objStart,
        IReadOnlyList<R2000Class>? classes = null)
    {
        Handle = handle;
        Rec = rec;
        ObjStart = objStart;
        Classes = classes;
        Cap = rec.ProxyObject ??= new ProxyObjectCapture();
        Cap.AttachIdentity(rec);
    }

    public ProxyObjectCapture Cap { get; }

    public abstract string Family { get; }

    ulong IRecord.Handle => Handle;
    public short TypeCode => TypeCodeValue;
    public DwgVersion SourceVersion => Rec.SourceVersion;
    public bool Captured => Cap.Captured;

    protected virtual bool HasXdicMissing => true;

    protected virtual bool HasInlineName => true;

    protected virtual bool RequiresCentralClassName => false;

    protected virtual bool HasDsData => false;

    protected virtual bool TryStartFrame(BitReader br, out long hs)
    {
        uint bitsize = br.ReadRL();
        hs = (long)ObjStart * 8 + bitsize;
        return true;
    }

    protected virtual long ReadVersion(BitReader br) => br.ReadBL();

    protected virtual bool TryReadCentralClassName(byte[] data, long dataStart, long marker,
        out string name, out string why)
    {
        name = string.Empty;
        why = "本档无集中串名形";
        return false;
    }

    protected void Fail(BitReader br, string why)
    {
        Cap.FailReason = why;
        Cap.FailBit = br.BitPosition - Rec.CarryStartBit;
    }

    internal virtual void Read(BitReader br)
    {
        if (!TryStartFrame(br, out long hs))
        {
            if (string.IsNullOrEmpty(Cap.FailReason)) Fail(br, "定段:取不到帧长(无内联 RL)");
            return;
        }
        if (hs <= br.BitPosition) { Fail(br, $"定段:hs={hs}≤游标={br.BitPosition}(帧长非正)"); return; }
        if (hs > (long)Rec.BodyEnd * 8) { Fail(br, $"定段:hs={hs}越体末={Rec.BodyEnd * 8}"); return; }
        long dataStart = Rec.CarryStartBit;
        long ownStart = br.BitPosition;
        if (br.ReadHandle().Value == 0)
        {
            Fail(br, "own-handle=0(位流错位)");
            return;
        }
        long headStart = ownStart;
        if (!DwgObjectReader.TrySkipEed(br)) { Fail(br, "EED 段不闭合"); return; }
        long nr = br.ReadBL();
        if (nr is < 0 or > 8) { Fail(br, $"num_reactors={nr}越界"); return; }
        if (HasXdicMissing) br.ReadB();
        long dsBit = -1;
        if (HasDsData) { dsBit = br.BitPosition; br.ReadB(); }

        long classId = br.ReadBL();
        if (classId < 0) { Fail(br, $"class_id={classId}<0"); return; }
        long nameStart = br.BitPosition; int nameBits = 0; string name = string.Empty;
        if (HasInlineName)
        {
            name = br.ReadTV();
            if (name.Length == 0) { Fail(br, "内联类名 TV 为空"); return; }
            nameBits = (int)(br.BitPosition - nameStart);
        }
        long ver = ReadVersion(br);
        bool fromDxf = br.ReadB();
        long dataStartBit = br.BitPosition;
        if (dataStartBit >= hs) { Fail(br, $"数据区空:头末={dataStartBit}≥hs={hs}"); return; }
        long marker = LocateTailMarker(br.UnderlyingData(), dataStartBit, hs);
        Cap.TailMarkerRelBit = marker < 0 ? -1 : marker;
        if (RequiresCentralClassName
            && !TryReadCentralClassName(br.UnderlyingData(), dataStartBit, marker,
                out name, out string nameWhy))
        {
            string? cpp = ClassNameFromClasses(classId);
            if (cpp is null)
            {
                Fail(br, $"集中串类名不闭合:{nameWhy}");
                return;
            }
            Cap.ClassId = classId;
            Cap.Version = ver;
            Cap.FromDxf = fromDxf;
            Cap.TailMarkerRelBit = marker < 0 ? -1 : marker;
            Cap.DataStartBit = dataStart;
            Cap.HeadStartBit = headStart;
            Cap.HeadEndBit = dataStartBit;
            Cap.DataRegionStartBit = dataStartBit;
            Cap.DataRegionEndBit = hs;
            Cap.NameStartBit = nameStart;
            Cap.NameBitLen = nameBits;
            Cap.ClassName = "cn:" + cpp;
            Cap.ClassNameSource = $"CLASSES表反查(class_id={classId})";
            Cap.ValuesCommitted = true;
            Fail(br, $"手术侧未就绪:数据区形制未定谱(值轴已提交,类名='{Cap.ClassName}')");
            return;
        }

        Cap.DataStartBit = dataStart;
        Cap.HandleStreamStartBit = hs;
        Cap.HeadStartBit = headStart;
        Cap.HeadEndBit = dataStartBit;
        Cap.ClassId = classId;
        Cap.ClassName = name;
        Cap.ClassNameSource = HasInlineName ? "帧内内联TV" : "集中串区名块";
        Cap.NameStartBit = nameStart;
        Cap.NameBitLen = nameBits;
        Cap.Version = ver;
        Cap.FromDxf = fromDxf;
        Cap.DsBitPos = dsBit;
        Cap.DataRegionStartBit = dataStartBit;
        Cap.DataRegionEndBit = hs;
        Rec.ProxyObjectTargetTail = ReadBitsString(br.UnderlyingData(), hs - 16, 16);
        Cap.Captured = true;
        if (System.Environment.GetEnvironmentVariable("DWGDBG") == "1")
            System.Console.Error.WriteLine(
                $"PROXYREC h={Rec.Handle:X} hs={hs} own={ownStart - dataStart} cid={Cap.ClassId} "
                + $"name={Cap.ClassName} nameStart={Cap.NameStartBit} nameLen={Cap.NameBitLen} "
                + $"ver={Cap.Version} dxf={(Cap.FromDxf ? 1 : 0)} data=[{Cap.DataRegionStartBit},{Cap.DataRegionEndBit}) "
                + $"markerRel={Cap.TailMarkerRelBit}");

        Rec.HandleStreamStartBit = hs;
        Rec.HasHandleStream = true;
    }

    internal const string TailMarker = "00001001011010100111010";

    protected string? ClassNameFromClasses(long classId)
    {
        if (Classes is null || classId < 500) return null;
        foreach (var c in Classes)
            if (c.Number == (int)classId && !string.IsNullOrEmpty(c.CppName)) return c.CppName;
        return null;
    }

    private static long LocateTailMarker(byte[] data, long from, long to)
    {
        long len = to - from;
        if (len < TailMarker.Length) return -1;
        var win = new System.Text.StringBuilder(TailMarker.Length);
        long found = -1;
        for (long i = 0; i < len; i++)
        {
            win.Append(BitAt(data, from + i) ? '1' : '0');
            if (win.Length > TailMarker.Length) win.Remove(0, 1);
            if (win.Length == TailMarker.Length && win.ToString() == TailMarker)
            {
                if (found >= 0) return -2;
                found = i - TailMarker.Length + 1;
            }
        }
        return found;
    }

    internal static string MarkerWhy(long marker) => marker switch
    {
        -1 => "无 tail-marker",
        -2 => "tail-marker 多处出现(判据不唯一)",
        _ => "tail-marker 已定位",
    };

    private static string ReadBitsString(byte[] data, long from, int count)
    {
        var sb = new System.Text.StringBuilder(count);
        for (int i = 0; i < count; i++) sb.Append(BitAt(data, from + i) ? '1' : '0');
        return sb.ToString();
    }

    private static bool BitAt(byte[] data, long bit)
    {
        long idx = bit >> 3;
        if (idx < 0 || idx >= data.Length) return false;
        return ((data[idx] >> (int)(7 - (bit & 7))) & 1) != 0;
    }
}
