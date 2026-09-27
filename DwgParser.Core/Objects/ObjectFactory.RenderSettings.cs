using DwgParser.Core.IO;
using DwgParser.Core.Objects.Records;

namespace DwgParser.Core.Objects;

public static partial class ObjectFactory
{
    public static IRenderSettings? CreateRenderSettings(ulong handle, DwgVersion version,
        string? className, DwgObjectRecord record, int objStart)
    {
        string kind = RenderSettingsRecord.KindOf(className);
        if (kind.Length == 0) return null;
        return (version, kind) switch
        {
            (DwgVersion.AC1015, "MENTALRAY") => new MentalRayRenderSettingsAcR2000(handle, record, objStart),
            (DwgVersion.AC1018, "MENTALRAY") => new MentalRayRenderSettingsAcR2004(handle, record, objStart),
            (DwgVersion.AC1021, "MENTALRAY") => new MentalRayRenderSettingsAcR2007(handle, record, objStart),
            (DwgVersion.AC1015, "RAPIDRT") => new RapidRtRenderSettingsAcR2000(handle, record, objStart),
            (DwgVersion.AC1018, "RAPIDRT") => new RapidRtRenderSettingsAcR2004(handle, record, objStart),
            (DwgVersion.AC1021, "RAPIDRT") => new RapidRtRenderSettingsAcR2007(handle, record, objStart),
            _ => null,
        };
    }

    public static void Decode(IRenderSettings target, BitReader br)
    {
        switch (target)
        {
            case RenderSettingsRecord rs:
                rs.Read(br);
                break;
            default:
                throw new System.NotSupportedException(
                    $"ObjectFactory 暂不支持解码该 {nameof(IRenderSettings)} 实现：{target?.GetType().Name}");
        }
    }
}
