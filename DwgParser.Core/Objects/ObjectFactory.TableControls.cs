using DwgParser.Core.IO;
using DwgParser.Core.Objects.Records;

namespace DwgParser.Core.Objects;

public static partial class ObjectFactory
{
    public static ITableControl? CreateTableControl(ulong handle, DwgVersion version,
        DwgObjectRecord record, int objStart)
        => version switch
        {
            DwgVersion.AC1015 => new TableControlAcR2000(handle, record, objStart),
            DwgVersion.AC1018 => new TableControlAcR2004(handle, record, objStart),
            DwgVersion.AC1021 => new TableControlAcR2007(handle, record, objStart),
            DwgVersion.AC1024 => new TableControlAcR2010(handle, record, objStart),
            DwgVersion.AC1027 or DwgVersion.AC1032 => new TableControlAcR2013Plus(handle, record, objStart),
            _ => null,
        };

    public static void Decode(ITableControl target, BitReader br)
    {
        switch (target)
        {
            case TableControlRecord tc:
                tc.Read(br);
                break;
            default:
                throw new System.NotSupportedException(
                    $"ObjectFactory 暂不支持解码该 {nameof(ITableControl)} 实现：{target?.GetType().Name}");
        }
    }
}
