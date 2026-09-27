using DwgParser.Core.IO;

namespace DwgParser.Core.Objects.Records;

internal static class CentralStrings
{
    public static bool TryDataBits(DwgObjectRecord rec, int objStart, BitReader br,
        out uint bitsize, out long dataEnd)
    {
        bitsize = 0;
        dataEnd = 0;
        if (rec.SourceVersion.IsR2010Plus())
        {
            long end = rec.HandleStreamStartBit;
            if (end <= (long)objStart * 8 || end > (long)rec.BodyEnd * 8) return false;
            long bits = end - (long)objStart * 8;
            if (bits <= 0 || bits > uint.MaxValue) return false;
            bitsize = (uint)bits;
            dataEnd = end;
            return true;
        }
        uint rl = br.ReadRL();
        long e = (long)objStart * 8 + rl;
        if (e <= br.BitPosition || e > (long)rec.BodyEnd * 8) return false;
        bitsize = rl;
        dataEnd = e;
        return true;
    }

    public static bool TryMeasure(DwgObjectRecord rec, byte[] data, int objStart, uint bitsize,
        out BitReader strings, out long areaStart, out long areaEnd)
    {
        strings = null!;
        areaStart = 0;
        areaEnd = 0;
        long dataEnd = (long)objStart * 8 + bitsize;
        long flagBit = dataEnd - 1;
        if (flagBit < 0 || flagBit >= (long)data.Length * 8) return false;

        var probe = new BitReader(data) { UnicodeStrings = true };
        probe.SetPosition((int)(flagBit >> 3), (int)(flagBit & 7));
        if (!probe.ReadB()) return false;

        uint sizeFieldBits = 17;
        long rsPos = dataEnd - 17;
        if (rsPos < 0 || rsPos >= (long)data.Length * 8) return false;
        probe.SetPosition((int)(rsPos >> 3), (int)(rsPos & 7));
        uint dataSize = probe.ReadRS();
        if ((dataSize & 0x8000) != 0)
        {
            long hiPos = dataEnd - 33;
            if (hiPos < 0 || hiPos >= (long)data.Length * 8) return false;
            probe.SetPosition((int)(hiPos >> 3), (int)(hiPos & 7));
            dataSize = (dataSize & 0x7FFF) | ((uint)probe.ReadRS() << 15);
            sizeFieldBits = 33;
        }
        if (dataSize == 0 || dataSize >= bitsize) return false;

        long start = dataEnd - sizeFieldBits - dataSize;
        if (start < (long)objStart * 8 || start >= (long)data.Length * 8) return false;
        var str = new BitReader(data) { UnicodeStrings = true };
        str.SetPosition((int)(start >> 3), (int)(start & 7));
        strings = str;
        areaStart = start;
        areaEnd = start + dataSize;
        return true;
    }

    public static bool ReadAll(BitReader strings, long areaEnd, int cap, out List<string> got)
    {
        got = new List<string>();
        for (int i = 0; i < cap; i++)
        {
            if (strings.BitPosition >= areaEnd) break;
            got.Add(strings.ReadTV());
        }
        return strings.BitPosition == areaEnd;
    }
}

internal abstract class MaterialCentralRecord : MaterialRecord
{
    protected MaterialCentralRecord(ulong handle, DwgObjectRecord rec, int objStart)
        : base(handle, rec, objStart) { }

    internal override void Read(BitReader br)
    {
        DwgObjectRecord.SeekTo(br, Rec.CarryStartBit);
        if (!CentralStrings.TryDataBits(Rec, ObjStart, br, out uint bitsize, out _)) return;
        if (!CentralStrings.TryMeasure(Rec, br.UnderlyingData(), ObjStart, bitsize,
                out BitReader str, out long areaStart, out long areaEnd)) return;
        if (!CentralStrings.ReadAll(str, areaEnd, 24, out var strings)) return;
        if (strings.Count < 2) return;
        string name = strings[0], desc = strings[1];
        if (name.Length == 0) return;

        Cap.Name = name;
        Cap.Description = desc;
        Cap.Captured = true;
        Name = name;
        Description = desc;

        TryWalkNumeric(br, areaStart);
    }

    private void TryWalkNumeric(BitReader br, long ss)
    {
        var reg = new MaterialRegion { EndBit = ss };
        var since = new MaterialSince();
        try
        {
            DwgObjectRecord.SeekTo(br, Rec.CarryStartBit);
            if (!Rec.SourceVersion.IsR2010Plus()) br.ReadRL();
            br.ReadHandle();
            if (!DwgObjectReader.TrySkipEed(br)) return;
            if (br.ReadBL() is < 0 or > 8) return;
            br.ReadB();
            if (Rec.SourceVersion >= DwgVersion.AC1027) br.ReadB();
            MaterialRecord.WalkRegion(br, reg, inlineFilenames: false);
            MaterialRecord.WalkSinceR2007(br, since);
        }
        catch (Exception)
        {
            return;
        }
        if (!reg.Closed || br.BitPosition != ss) return;

        MaterialRecord.CommitRegion(Cap, reg);
        Cap.Translucence = since.Translucence;
        Cap.SelfIllumination = since.SelfIllumination;
        Cap.Reflectivity = since.Reflectivity;
        Cap.IlluminationModel = since.IlluminationModel;
        Cap.DiskChannelFlags = since.ChannelFlags;
        Cap.Mode = since.Mode;
        Cap.SinceCaptured = true;
    }
}

internal sealed class MaterialAcRlForm : MaterialCentralRecord
{
    public override string Family => "R2007";

    public MaterialAcRlForm(ulong handle, DwgObjectRecord rec, int objStart)
        : base(handle, rec, objStart) { }
}

internal sealed class MaterialAcBotForm : MaterialCentralRecord
{
    public override string Family => "R2010+";

    public MaterialAcBotForm(ulong handle, DwgObjectRecord rec, int objStart)
        : base(handle, rec, objStart) { }
}

internal abstract class MLineStyleCentralRecord : MLineStyleRecord
{
    protected MLineStyleCentralRecord(ulong handle, DwgObjectRecord rec, int objStart)
        : base(handle, rec, objStart) { }

    internal override void Read(BitReader br)
    {
        DwgObjectRecord.SeekTo(br, Rec.CarryStartBit);
        if (!CentralStrings.TryDataBits(Rec, ObjStart, br, out uint bitsize, out _)) return;
        if (!CentralStrings.TryMeasure(Rec, br.UnderlyingData(), ObjStart, bitsize,
                out BitReader str, out _, out long areaEnd)) return;
        if (!CentralStrings.ReadAll(str, areaEnd, 8, out var strings)) return;
        if (strings.Count != 2) return;
        string name = strings[0], desc = strings[1];
        if (name.Length == 0) return;

        Cap.Name = name;
        Cap.Description = desc;
        Cap.Captured = true;
        Name = name;
        Description = desc;
    }
}

internal sealed class MLineStyleAcRlForm : MLineStyleCentralRecord
{
    public override string Family => "R2007";

    public MLineStyleAcRlForm(ulong handle, DwgObjectRecord rec, int objStart)
        : base(handle, rec, objStart) { }
}

internal sealed class MLineStyleAcBotForm : MLineStyleCentralRecord
{
    public override string Family => "R2010+";

    public MLineStyleAcBotForm(ulong handle, DwgObjectRecord rec, int objStart)
        : base(handle, rec, objStart) { }
}

internal abstract class DictWdfltCentralRecord : DictWdfltRecord
{
    protected DictWdfltCentralRecord(ulong handle, DwgObjectRecord rec, int objStart)
        : base(handle, rec, objStart) { }

    internal override void Read(BitReader br)
    {
        DwgObjectRecord.SeekTo(br, Rec.CarryStartBit);
        if (!CentralStrings.TryDataBits(Rec, ObjStart, br, out uint bitsize, out _))
        { Fail(br, "集中流 bitsize 未定位"); return; }
        if (!CentralStrings.TryMeasure(Rec, br.UnderlyingData(), ObjStart, bitsize,
                out BitReader str, out long areaStart, out long areaEnd))
        { Fail(br, "集中流串区量不出"); return; }
        if (!CentralStrings.ReadAll(str, areaEnd, 4, out var strings))
        { Fail(br, "集中流 TU 逐枚读出越界"); return; }
        if (!TryWalkToHeaderEnd(br, areaStart, out long nent, out long dup, out int hardOwner))
            return;
        if (nent < 1 || nent != strings.Count)
        { Fail(br, $"字典头 nent={nent}≠串区枚数={strings.Count}"); return; }
        if (strings.Any(s => s.Length == 0))
        { Fail(br, "有空键名"); return; }

        Cap.NumEntries = nent;
        Cap.DuplicateCloning = dup;
        Cap.HardOwner = hardOwner;
        Cap.EntryNames = new List<string>(strings);
        Cap.DefaultEntryName = strings[^1];
        Cap.ValuesCommitted = true;

        if (strings.Count != 1)
        { Fail(br, $"串区枚数={strings.Count}≠1(值轴已提交)"); return; }
        string name = strings[0];
        if (name.Length == 0) { Fail(br, "条目名为空"); return; }

        Cap.EntryName = name;
        Cap.Captured = true;
        EntryName = name;
    }

    private bool TryWalkToHeaderEnd(BitReader br, long areaStart,
        out long numEntries, out long dupCloning, out int hardOwner)
    {
        numEntries = 0; dupCloning = 0; hardOwner = 0;
        DwgObjectRecord.SeekTo(br, Rec.CarryStartBit);
        if (!Rec.SourceVersion.IsR2010Plus()) br.ReadRL();
        br.ReadHandle();
        if (!DwgObjectReader.TrySkipEed(br)) { Fail(br, "EED"); return false; }
        int numReactors = br.ReadBL();
        br.ReadB();
        if (Rec.SourceVersion >= DwgVersion.AC1027) br.ReadB();
        if (numReactors is < 0 or > 8) { Fail(br, $"num_reactors={numReactors}"); return false; }
        numEntries = br.ReadBL();
        dupCloning = br.ReadBS();
        hardOwner = br.ReadRC();
        if (br.BitPosition != areaStart)
        {
            Fail(br, $"字典头末位不落串区起点(Δ={br.BitPosition - areaStart})");
            return false;
        }
        return true;
    }
}

internal sealed class DictWdfltAcRlForm : DictWdfltCentralRecord
{
    public override string Family => "R2007";

    public DictWdfltAcRlForm(ulong handle, DwgObjectRecord rec, int objStart)
        : base(handle, rec, objStart) { }
}

internal sealed class DictWdfltAcBotForm : DictWdfltCentralRecord
{
    public override string Family => "R2010+";

    public DictWdfltAcBotForm(ulong handle, DwgObjectRecord rec, int objStart)
        : base(handle, rec, objStart) { }
}
