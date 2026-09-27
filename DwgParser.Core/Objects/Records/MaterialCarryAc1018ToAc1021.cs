using DwgParser.Core.IO;

namespace DwgParser.Core.Objects.Records;

internal sealed class MaterialCarryAc1018ToAc1021 : MaterialCarryPlan
{
    private const uint UpgradeTailBitsDefault = 0xAA5FE;
    private const int UpgradeTailLen = 20;

    public override string Name => "Material:pre-R2007→AC1021";

    public override bool IsCaptured(DwgObjectRecord rec) => rec.Material is { Captured: true };

    internal override void WriteUpgradeTail(BitWriter body, DwgObjectRecord rec,
        DwgObjectStreamWriter.Target t)
    {
        var cap = rec.Material;
        if (cap is null || !cap.HasMapWalk)
        {
            body.WriteBits(UpgradeTailBitsDefault, UpgradeTailLen);
            return;
        }
        body.WriteBits(0b10, 2);
        body.WriteBits(0b10, 2);
        body.WriteBits(0b10, 2);
        body.WriteBits(0b10, 2);
        body.WriteBL(cap.ChannelFlags);
        body.WriteBits(0b10, 2);
    }
}
