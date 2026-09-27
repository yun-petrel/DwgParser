using DwgParser.Core.IO;

namespace DwgParser.Core.Objects;

internal sealed class CommonEntityDataAcR2000 : CommonEntityData
{
    public override void Read(BitReader br, DwgEntity entity, DwgObjectRecord? record,
        uint bitsize, int objStart, int objEnd)
    {
        Bitsize = bitsize;
        bool previewExists = br.ReadB();
        if (previewExists)
            ReadPreviewPayload(br, record, br.ReadRL(), objEnd);

        EntMode = br.ReadBB();
        NumReactors = (uint)br.ReadBL();
        NoLinks = br.ReadB();
        long cmcStart = br.BitPosition;
        int cmc = br.ReadBS();
        entity.ColorIndex = (short)Math.Clamp(cmc & 0x1FF, 0, 256);
        if (record != null)
        {
            record.ColorStartBit = cmcStart;
            record.ColorBitLen = (int)(br.BitPosition - cmcStart);
        }
        LinetypeScale = br.ReadBD();
        LtypeFlags = br.ReadBB();
        PlotStyleFlags = br.ReadBB();
        Invisible = br.ReadBS();
        Linewt = br.ReadRC();

        Commit(entity, record);
    }
}
