using DwgParser.Core.IO;

namespace DwgParser.Core.Objects.Records;

public abstract class FieldListRecord : IFieldList
{
    internal const long MaxFields = 20_000;

    protected readonly DwgObjectRecord Rec;
    protected readonly int ObjStart;
    protected readonly ulong Handle;

    protected FieldListRecord(ulong handle, DwgObjectRecord rec, int objStart)
    {
        Handle = handle;
        Rec = rec;
        ObjStart = objStart;
        Cap = rec.FieldList ??= new FieldListCapture();
        Cap.AttachIdentity(rec);
    }

    public FieldListCapture Cap { get; }

    public abstract string Family { get; }

    ulong IRecord.Handle => Handle;
    public short TypeCode => Rec.LogicalType;
    public DwgVersion SourceVersion => Rec.SourceVersion;
    public long NumFields { get; protected set; } = -1;
    public bool UnknownFlag { get; protected set; }
    public IReadOnlyList<ulong> FieldHandles => Cap.FieldHandles;
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

        long numFields = br.ReadBL();
        if (numFields is < 0 or > MaxFields) { Fail("num_fields 越界"); return; }
        bool unknown = br.ReadB();

        bool strings = false;
        if (HasTrailingStrings) strings = br.ReadB();

        if (br.BitPosition != hs) { Fail($"停点 {br.BitPosition - hs}"); return; }

        Cap.NumFields = numFields;
        Cap.UnknownFlag = unknown;
        Cap.StringStreamPresent = strings;
        Cap.WalkEndBit = br.BitPosition;
        Cap.FailReason = "";
        Cap.Captured = true;
        NumFields = numFields;
        UnknownFlag = unknown;
        StringStreamPresent = strings;

        ReadHandleStream(br, hs, numReactors, xdicMissing, numFields);
    }

    private void ReadHandleStream(BitReader br, long hs, long numReactors, bool xdicMissing, long numFields)
    {
        try
        {
            var hb = new BitReader(br.UnderlyingData()) { UnicodeStrings = br.UnicodeStrings };
            hb.SetPosition((int)(hs >> 3), (int)(hs & 7));
            hb.ReadHandle();
            for (long i = 0; i < numReactors; i++) hb.ReadHandle();
            if (!HasXdicMissing || !xdicMissing) hb.ReadHandle();
            var fields = new List<ulong>((int)numFields);
            for (long i = 0; i < numFields; i++) fields.Add(hb.ReadHandle().Resolve(Rec.Handle));
            if (hb.BitPosition > (long)Rec.BodyEnd * 8) { FailEntries("fields 越帧"); return; }

            Cap.FieldHandles = fields;
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
