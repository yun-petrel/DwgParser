using DwgParser.Core.IO;

namespace DwgParser.Core.Objects;

internal abstract class CommonEntityDataAcR2004Plus : CommonEntityData
{
    protected abstract bool PreviewSizeIsBits64 { get; }

    protected abstract bool HasDsDataBit { get; }

    protected abstract bool HasMaterialShadow { get; }

    protected abstract bool HasVisualStyleFlags { get; }

    public override void Read(BitReader br, DwgEntity entity, DwgObjectRecord? record,
        uint bitsize, int objStart, int objEnd)
    {
        Bitsize = bitsize;
        bool previewExists = br.ReadB();
        if (previewExists)
        {
            ulong psz = PreviewSizeIsBits64 ? br.ReadBLL() : br.ReadRL();
            ReadPreviewPayload(br, record, psz, objEnd);
        }
        EntMode = br.ReadBB();
        NumReactors = (uint)br.ReadBL();
        IsXdicMissing = br.ReadB();
        if (HasDsDataBit)
            HasDsData = br.ReadB();
        long encColorStart = br.BitPosition;
        DwgObjectReader.ReadEncColor(br, entity, record);
        if (record != null)
        {
            record.ColorStartBit = encColorStart;
            record.ColorBitLen = (int)(br.BitPosition - encColorStart);
        }
        LinetypeScale = br.ReadBD();
        LtypeFlags = br.ReadBB();
        PlotStyleFlags = br.ReadBB();
        if (HasMaterialShadow)
        {
            MaterialFlags = br.ReadBB();
            ShadowFlags = br.ReadRC();
        }
        if (HasVisualStyleFlags)
        {
            HasFullVisualStyle = br.ReadB();
            HasFaceVisualStyle = br.ReadB();
            HasEdgeVisualStyle = br.ReadB();
        }
        Invisible = br.ReadBS();
        Linewt = br.ReadRC();

        Commit(entity, record);
    }
}

internal sealed class CommonEntityDataAcR2004 : CommonEntityDataAcR2004Plus
{
    protected override bool PreviewSizeIsBits64 => false;
    protected override bool HasDsDataBit => false;
    protected override bool HasMaterialShadow => false;
    protected override bool HasVisualStyleFlags => false;
}

internal sealed class CommonEntityDataAcR2007 : CommonEntityDataAcR2004Plus
{
    protected override bool PreviewSizeIsBits64 => false;
    protected override bool HasDsDataBit => false;
    protected override bool HasMaterialShadow => true;
    protected override bool HasVisualStyleFlags => false;
}

internal sealed class CommonEntityDataAcR2010 : CommonEntityDataAcR2004Plus
{
    protected override bool PreviewSizeIsBits64 => true;
    protected override bool HasDsDataBit => false;
    protected override bool HasMaterialShadow => true;
    protected override bool HasVisualStyleFlags => true;
}

internal sealed class CommonEntityDataAcR2013 : CommonEntityDataAcR2004Plus
{
    protected override bool PreviewSizeIsBits64 => true;
    protected override bool HasDsDataBit => true;
    protected override bool HasMaterialShadow => true;
    protected override bool HasVisualStyleFlags => true;
}
