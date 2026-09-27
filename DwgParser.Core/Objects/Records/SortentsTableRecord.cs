using DwgParser.Core.IO;

namespace DwgParser.Core.Objects.Records;

public abstract class SortentsTableRecord : ISortentsTable
{
    internal const long MaxEntries = 50_000;

    protected readonly DwgObjectRecord Rec;
    protected readonly int ObjStart;
    protected readonly ulong Handle;

    protected SortentsTableRecord(ulong handle, DwgObjectRecord rec, int objStart)
    {
        Handle = handle;
        Rec = rec;
        ObjStart = objStart;
        Cap = rec.SortentsTable ??= new SortentsTableCapture();
        Cap.AttachIdentity(rec);
    }

    public SortentsTableCapture Cap { get; }

    public abstract string Family { get; }

    ulong IRecord.Handle => Handle;
    public short TypeCode => Rec.LogicalType;
    public DwgVersion SourceVersion => Rec.SourceVersion;
    public long NumEntities { get; protected set; } = -1;
    public IReadOnlyList<ulong> SortEntityHandles => Cap.SortEntityHandles;
    public ulong BlockOwnerHandle { get; protected set; }
    public IReadOnlyList<ulong> EntityHandles => Cap.EntityHandles;
    public bool StringStreamPresent { get; protected set; }
    public bool Captured => Cap.Captured;
    public bool EntriesCaptured => Cap.EntriesCaptured;

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

        long numEnts = br.ReadBL();
        if (numEnts is < 0 or > MaxEntries) { Fail("num_ents 越界"); return; }
        var sortEnts = new List<ulong>((int)numEnts);
        for (long i = 0; i < numEnts; i++)
            sortEnts.Add(br.ReadHandle().Resolve(Rec.Handle));

        bool strings = false;
        if (HasTrailingStrings) strings = br.ReadB();

        if (br.BitPosition != hs) { Fail($"停点 {br.BitPosition - hs}"); return; }

        Cap.NumEntities = numEnts;
        Cap.SortEntityHandles = sortEnts;
        Cap.StringStreamPresent = strings;
        Cap.WalkEndBit = br.BitPosition;
        Cap.FailReason = "";
        Cap.Captured = true;
        NumEntities = numEnts;
        StringStreamPresent = strings;

        ReadHandleStream(br, hs, numReactors, xdicMissing, numEnts);
    }

    private void ReadHandleStream(BitReader br, long hs, long numReactors, bool xdicMissing, long numEnts)
    {
        try
        {
            var hb = new BitReader(br.UnderlyingData()) { UnicodeStrings = br.UnicodeStrings };
            hb.SetPosition((int)(hs >> 3), (int)(hs & 7));
            hb.ReadHandle();
            for (long i = 0; i < numReactors; i++) hb.ReadHandle();
            if (!HasXdicMissing || !xdicMissing) hb.ReadHandle();
            ulong blockOwner = hb.ReadHandle().Resolve(Rec.Handle);
            var ents = new List<ulong>((int)numEnts);
            for (long i = 0; i < numEnts; i++) ents.Add(hb.ReadHandle().Resolve(Rec.Handle));
            if (hb.BitPosition > (long)Rec.BodyEnd * 8) { FailEntries("ents 越帧"); return; }

            Cap.BlockOwnerHandle = blockOwner;
            Cap.EntityHandles = ents;
            Cap.EntriesCaptured = true;
            Cap.EntriesFailReason = "";
            BlockOwnerHandle = blockOwner;
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
