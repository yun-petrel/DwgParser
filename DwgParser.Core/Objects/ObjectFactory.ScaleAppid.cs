using DwgParser.Core.IO;
using DwgParser.Core.Objects.Records;

namespace DwgParser.Core.Objects;

public static partial class ObjectFactory
{

    public static IScale? CreateScale(ulong handle, DwgVersion version,
        DwgObjectRecord record, int objStart)
        => version switch
        {
            DwgVersion.AC1015 => new ScaleAcR2000(handle, record, objStart),
            DwgVersion.AC1018 => new ScaleAcR2004(handle, record, objStart),
            DwgVersion.AC1021 => new ScaleAcR2007(handle, record, objStart),
            DwgVersion.AC1024 => new ScaleAcR2010(handle, record, objStart),
            DwgVersion.AC1027 => new ScaleAcR2013(handle, record, objStart),
            DwgVersion.AC1032 => new ScaleAcR2018(handle, record, objStart),
            _ => null,
        };

    public static void Decode(IScale target, BitReader br)
    {
        switch (target)
        {
            case ScaleRecord sc:
                sc.Read(br);
                break;
            default:
                throw new System.NotSupportedException(
                    $"ObjectFactory 暂不支持解码该 {nameof(IScale)} 实现：{target?.GetType().Name}");
        }
    }

    public static IAppid? CreateAppid(ulong handle, DwgVersion version,
        DwgObjectRecord record, int objStart)
        => version switch
        {
            DwgVersion.AC1015 => new AppidAcR2000(handle, record, objStart),
            DwgVersion.AC1018 => new AppidAcR2004(handle, record, objStart),
            DwgVersion.AC1021 => new AppidAcR2007(handle, record, objStart),
            DwgVersion.AC1024 => new AppidAcR2010(handle, record, objStart),
            DwgVersion.AC1027 => new AppidAcR2013(handle, record, objStart),
            DwgVersion.AC1032 => new AppidAcR2018(handle, record, objStart),
            _ => null,
        };

    public static void Decode(IAppid target, BitReader br)
    {
        switch (target)
        {
            case AppidRecord ap:
                ap.Read(br);
                break;
            default:
                throw new System.NotSupportedException(
                    $"ObjectFactory 暂不支持解码该 {nameof(IAppid)} 实现：{target?.GetType().Name}");
        }
    }
}
