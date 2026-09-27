using DwgParser.Core.IO;

namespace DwgParser.Core.Objects;

public interface ICommonEntityData : IEntityHeadFlags
{
    bool HasDsData { get; }

    short Invisible { get; }

    byte Linewt { get; }

    double LinetypeScale { get; }

    uint Bitsize { get; }

    void Read(BitReader br, DwgEntity entity, DwgObjectRecord? record, uint bitsize, int objStart, int objEnd);
}

public abstract class CommonEntityData : ICommonEntityData
{
    public byte EntMode { get; protected set; }
    public uint NumReactors { get; protected set; }
    public bool NoLinks { get; protected set; } = true;
    public bool IsXdicMissing { get; protected set; }
    public bool HasDsData { get; protected set; }
    public byte LtypeFlags { get; protected set; }
    public byte PlotStyleFlags { get; protected set; }
    public byte MaterialFlags { get; protected set; }
    public byte ShadowFlags { get; protected set; }
    public short Invisible { get; protected set; }
    public byte Linewt { get; protected set; }
    public double LinetypeScale { get; protected set; } = 1.0;
    public bool HasFullVisualStyle { get; protected set; }
    public bool HasFaceVisualStyle { get; protected set; }
    public bool HasEdgeVisualStyle { get; protected set; }
    public uint Bitsize { get; protected set; }

    public abstract void Read(BitReader br, DwgEntity entity, DwgObjectRecord? record,
        uint bitsize, int objStart, int objEnd);

    protected void Commit(DwgEntity entity, DwgObjectRecord? record)
    {
        entity.EntMode = EntMode;
        if (record == null) return;
        record.NumReactors = NumReactors;
        record.NoLinks = NoLinks;
        record.IsXdicMissing = IsXdicMissing;
        record.HasDsData = HasDsData;
        record.Invisible = Invisible;
        record.LineWeight = Linewt;
        record.LtypeFlags = LtypeFlags;
        record.PlotStyleFlags = PlotStyleFlags;
        record.MaterialFlags = MaterialFlags;
        record.ShadowFlags = ShadowFlags;
        record.HasFullVisualStyle = HasFullVisualStyle;
        record.HasFaceVisualStyle = HasFaceVisualStyle;
        record.HasEdgeVisualStyle = HasEdgeVisualStyle;
    }

    protected void ReadPreviewPayload(BitReader br, DwgObjectRecord? record, ulong size, int objEnd)
    {
        if (record != null)
        {
            record.HasPreview = true;
            record.PreviewSize = size;
            record.PreviewPayloadStartBit = br.BitPosition;
        }
        if (size < (ulong)objEnd)
            for (ulong i = 0; i < size && !br.EndOfStream; i++) br.ReadRC();
        if (record != null)
            record.PreviewPayloadBitLen = (int)(br.BitPosition - record.PreviewPayloadStartBit);
    }
}
