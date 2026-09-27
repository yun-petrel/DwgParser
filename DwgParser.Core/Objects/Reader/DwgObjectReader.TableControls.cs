using DwgParser.Core.IO;
using DwgParser.Core.Objects.Records;

namespace DwgParser.Core.Objects;

public static partial class DwgObjectReader
{
    private const short TypeLtypeControl = 0x38;
    private const short TypeViewControl = 0x3C;
    private const short TypeUcsControl = 0x3E;
    private const short TypeVportControl = 0x40;
    private const short TypeAppidControl = 0x42;
    private const short TypeDimStyleControl = 0x44;
    private const short TypeVxControl = 0x46;

    private static void TryDecodeTableControl(BitReader br, ulong handle, DwgVersion version,
        DwgObjectRecord? record, int objStart)
    {
        if (record == null) return;
        var tc = ObjectFactory.CreateTableControl(handle, version, record, objStart);
        if (tc != null) ObjectFactory.Decode(tc, br);
    }

    private static void TryDecodeTableControlAtCarryStart(BitReader br, ulong handle, DwgVersion version,
        DwgObjectRecord? record, int objStart)
    {
        if (record == null) return;
        long savedBit = br.BitPosition;
        DwgObjectRecord.SeekTo(br, record.CarryStartBit);
        try { TryDecodeTableControl(br, handle, version, record, objStart); }
        catch {  }
        br.SetPosition((int)(savedBit >> 3), (int)(savedBit & 7));
    }

    private static short MemberCodeOf(short control) => control switch
    {
        TypeBlockControl => TypeBlockHeader,
        TypeLayerControl => TypeLayer,
        TypeStyleControl => TypeStyle,
        TypeLtypeControl => TypeLType,
        TypeViewControl => 0x3D,
        TypeUcsControl => 0x3F,
        TypeVportControl => TypeVport,
        TypeAppidControl => TypeAppid,
        TypeDimStyleControl => TypeDimStyle,
        TypeVxControl => 0x47,
        _ => (short)-1,
    };

    internal static void ValidateTableControlEntries(WalkResult result)
    {
        List<TableControlCapture>? pending = null;
        foreach (var rec in result.Records)
            if (rec.TableControl is { EntriesCaptured: true, EntriesValidated: false } cap)
                (pending ??= new List<TableControlCapture>()).Add(cap);
        if (pending is null) return;

        var typeByHandle = new Dictionary<ulong, short>(result.Records.Count);
        foreach (var rec in result.Records) typeByHandle[rec.Handle] = rec.LogicalType;

        foreach (var cap in pending)
        {
            short member = MemberCodeOf(cap.TypeCode);
            if (member < 0) { cap.EntriesFailReason = "非本族控制码"; continue; }
            string? bad = null;
            foreach (var h in cap.EntryHandles)
            {
                if (h == 0) continue;
                if (!typeByHandle.TryGetValue(h, out var t)) { bad = $"条目 {h:X} 不在对象图"; break; }
                if (t != member) { bad = $"条目 {h:X} 类型 0x{t:X2} ≠ 0x{member:X2}"; break; }
            }
            if (bad is null)
                foreach (var h in cap.MoreHandleRefs)
                {
                    if (h == 0) continue;
                    if (!typeByHandle.TryGetValue(h, out var t) || t != member)
                    { bad = $"附加引用 {h:X} 非本表所辖"; break; }
                }
            if (bad is not null) { cap.EntriesFailReason = bad; continue; }
            cap.EntriesValidated = true;
            cap.EntriesFailReason = "";
        }
    }
}
