using DwgParser.Core.Format;
using DwgParser.Core.IO;

namespace DwgParser.Core.Objects.Records;

public abstract class TableControlRecord : ITableControl
{
    public const short DimStyleControlType = 0x44;

    public const short LtypeControlType = 0x38;

    public const short BlockControlType = 0x30;

    protected readonly DwgObjectRecord Rec;

    protected readonly int ObjStart;

    protected readonly ulong Handle;

    protected TableControlRecord(ulong handle, DwgObjectRecord rec, int objStart)
    {
        Handle = handle;
        Rec = rec;
        ObjStart = objStart;
        Cap = rec.TableControl ??= new TableControlCapture();
        Cap.AttachIdentity(rec);
    }

    public TableControlCapture Cap { get; }

    public abstract string Family { get; }

    ulong IRecord.Handle => Handle;
    public short TypeCode => Rec.LogicalType;
    public DwgVersion SourceVersion => Rec.SourceVersion;
    public long NumEntries { get; protected set; } = -1;
    public int NumMoreHandles { get; protected set; } = -1;
    public bool StringStreamPresent { get; protected set; }
    public bool Captured => Cap.Captured;
    public IReadOnlyList<ulong> EntryHandles => Cap.EntryHandles;
    public IReadOnlyList<ulong> MoreHandleRefs => Cap.MoreHandleRefs;
    public bool EntriesCaptured => Cap.EntriesCaptured;
    public bool EntriesValidated => Cap.EntriesValidated;

    protected virtual bool HasInlineRl => true;
    protected virtual bool HasXdicMissing => false;
    protected virtual bool HasDsData => false;
    protected virtual bool HasTrailingStrings => false;

    internal virtual void Read(BitReader br)
    {
        long hs;
        if (HasInlineRl)
        {
            uint rl = br.ReadRL();
            hs = (long)ObjStart * 8 + rl;
        }
        else
        {
            hs = Rec.HandleStreamStartBit;
        }
        if (hs <= br.BitPosition || hs > (long)Rec.BodyEnd * 8) { Fail("帧界"); return; }

        br.ReadHandle();
        if (!DwgObjectReader.TrySkipEed(br)) { Fail("EED"); return; }
        long numReactors = br.ReadBL();
        if (numReactors is < 0 or > 100_000) { Fail("num_reactors"); return; }
        bool xdicMissing = false;
        if (HasXdicMissing) xdicMissing = br.ReadB();
        if (HasDsData) br.ReadB();

        long entries = br.ReadBL();
        if (entries is < 0 or > 1_000_000) { Fail("num_entries"); return; }

        int more = -1;
        if (Rec.LogicalType == DimStyleControlType)
        {
            more = br.ReadRC();
        }

        bool strings = false;
        if (HasTrailingStrings) strings = br.ReadB();

        if (br.BitPosition != hs) { Fail($"停点 {br.BitPosition - hs}"); return; }

        NumEntries = entries;
        NumMoreHandles = more;
        StringStreamPresent = strings;
        Cap.NumEntries = entries;
        Cap.NumMoreHandles = more;
        Cap.StringStreamPresent = strings;
        Cap.WalkEndBit = br.BitPosition;
        Cap.FailReason = "";
        Cap.Captured = true;

        ReadEntryHandles(br, hs, numReactors, xdicMissing, entries, more);
    }

    private void ReadEntryHandles(BitReader br, long hs, long numReactors, bool xdicMissing,
        long entries, int moreHandles)
    {
        if (entries is < 0 or > 65_536) { FailEntries("枚数越界"); return; }
        try
        {
            var hb = new BitReader(br.UnderlyingData()) { UnicodeStrings = br.UnicodeStrings };
            hb.SetPosition((int)(hs >> 3), (int)(hs & 7));
            hb.ReadHandle();
            for (long i = 0; i < numReactors; i++) hb.ReadHandle();
            if (!HasXdicMissing || !xdicMissing) hb.ReadHandle();
            var list = new List<ulong>((int)entries);
            for (long i = 0; i < entries; i++)
                list.Add(hb.ReadHandle().Resolve(Rec.Handle));
            if (hb.BitPosition > (long)Rec.BodyEnd * 8) { FailEntries("条目越帧"); return; }

            int trailing = Rec.LogicalType switch
            {
                DimStyleControlType => moreHandles,
                LtypeControlType => 2,
                BlockControlType => 2,
                _ => 0,
            };
            List<ulong>? extra = null;
            if (trailing is > 0 and <= 64)
            {
                extra = new List<ulong>(trailing);
                for (int i = 0; i < trailing; i++) extra.Add(hb.ReadHandle().Resolve(Rec.Handle));
                if (hb.BitPosition > (long)Rec.BodyEnd * 8) { FailEntries("附加引用越帧"); return; }
            }
            else if (trailing > 64) { FailEntries("附加引用枚数越界"); return; }

            Cap.EntryHandles = list;
            Cap.MoreHandleRefs = extra ?? new List<ulong>();
            Cap.EntriesCaptured = true;
            Cap.EntriesFailReason = "";
        }
        catch (Exception ex) { FailEntries(ex.GetType().Name); }
    }

    private void FailEntries(string why)
    {
        Cap.EntriesCaptured = false;
        Cap.EntriesFailReason = why;
    }

    private void Fail(string why)
    {
        Cap.FailReason = why;
        Cap.WalkEndBit = -1;
    }
}
