using DwgParser.Core.IO;
using DwgParser.Core.Objects.Records;

namespace DwgParser.Core.Objects;

public static partial class ObjectFactory
{
    public static ISortentsTable? CreateSortentsTable(ulong handle, DwgVersion version,
        DwgObjectRecord record, int objStart)
        => version switch
        {
            DwgVersion.AC1015 => new SortentsTableAcR2000(handle, record, objStart),
            DwgVersion.AC1018 => new SortentsTableAcR2004(handle, record, objStart),
            DwgVersion.AC1021 => new SortentsTableAcR2007(handle, record, objStart),
            DwgVersion.AC1024 => new SortentsTableAcR2010(handle, record, objStart),
            DwgVersion.AC1027 => new SortentsTableAcR2013(handle, record, objStart),
            DwgVersion.AC1032 => new SortentsTableAcR2018(handle, record, objStart),
            _ => null,
        };

    public static void Decode(ISortentsTable target, BitReader br)
    {
        switch (target)
        {
            case SortentsTableRecord st:
                st.Read(br);
                break;
            default:
                throw new System.NotSupportedException(
                    $"ObjectFactory 暂不支持解码该 {nameof(ISortentsTable)} 实现：{target?.GetType().Name}");
        }
    }

    public static IRasterVariables? CreateRasterVariables(ulong handle, DwgVersion version,
        DwgObjectRecord record, int objStart)
        => version switch
        {
            DwgVersion.AC1015 => new RasterVariablesAcR2000(handle, record, objStart),
            DwgVersion.AC1018 => new RasterVariablesAcR2004(handle, record, objStart),
            DwgVersion.AC1021 => new RasterVariablesAcR2007(handle, record, objStart),
            DwgVersion.AC1024 => new RasterVariablesAcR2010(handle, record, objStart),
            DwgVersion.AC1027 => new RasterVariablesAcR2013(handle, record, objStart),
            DwgVersion.AC1032 => new RasterVariablesAcR2018(handle, record, objStart),
            _ => null,
        };

    public static void Decode(IRasterVariables target, BitReader br)
    {
        switch (target)
        {
            case RasterVariablesRecord rv:
                rv.Read(br);
                break;
            default:
                throw new System.NotSupportedException(
                    $"ObjectFactory 暂不支持解码该 {nameof(IRasterVariables)} 实现：{target?.GetType().Name}");
        }
    }

    public static ISun? CreateSun(ulong handle, DwgVersion version,
        DwgObjectRecord record, int objStart)
        => version switch
        {
            DwgVersion.AC1015 => new SunAcR2000(handle, record, objStart),
            DwgVersion.AC1018 => new SunAcR2004(handle, record, objStart),
            DwgVersion.AC1021 => new SunAcR2007(handle, record, objStart),
            DwgVersion.AC1024 => new SunAcR2010(handle, record, objStart),
            DwgVersion.AC1027 => new SunAcR2013(handle, record, objStart),
            DwgVersion.AC1032 => new SunAcR2018(handle, record, objStart),
            _ => null,
        };

    public static void Decode(ISun target, BitReader br)
    {
        switch (target)
        {
            case SunRecord sun:
                sun.Read(br);
                break;
            default:
                throw new System.NotSupportedException(
                    $"ObjectFactory 暂不支持解码该 {nameof(ISun)} 实现：{target?.GetType().Name}");
        }
    }

    public static ITableGeometry? CreateTableGeometry(ulong handle, DwgVersion version,
        DwgObjectRecord record, int objStart)
        => version switch
        {
            DwgVersion.AC1018 => new TableGeometryAcR2004(handle, record, objStart),
            DwgVersion.AC1021 => new TableGeometryAcR2007(handle, record, objStart),
            DwgVersion.AC1024 => new TableGeometryAcR2010(handle, record, objStart),
            DwgVersion.AC1027 => new TableGeometryAcR2013(handle, record, objStart),
            DwgVersion.AC1032 => new TableGeometryAcR2018(handle, record, objStart),
            _ => null,
        };

    public static void Decode(ITableGeometry target, BitReader br)
    {
        switch (target)
        {
            case TableGeometryRecord tg:
                tg.Read(br);
                break;
            default:
                throw new System.NotSupportedException(
                    $"ObjectFactory 暂不支持解码该 {nameof(ITableGeometry)} 实现：{target?.GetType().Name}");
        }
    }

    public static IFieldList? CreateFieldList(ulong handle, DwgVersion version,
        DwgObjectRecord record, int objStart)
        => version switch
        {
            DwgVersion.AC1015 => new FieldListAcR2000(handle, record, objStart),
            DwgVersion.AC1018 => new FieldListAcR2004(handle, record, objStart),
            DwgVersion.AC1021 => new FieldListAcR2007(handle, record, objStart),
            DwgVersion.AC1024 => new FieldListAcR2010(handle, record, objStart),
            DwgVersion.AC1027 => new FieldListAcR2013(handle, record, objStart),
            DwgVersion.AC1032 => new FieldListAcR2018(handle, record, objStart),
            _ => null,
        };

    public static void Decode(IFieldList target, BitReader br)
    {
        switch (target)
        {
            case FieldListRecord fl:
                fl.Read(br);
                break;
            default:
                throw new System.NotSupportedException(
                    $"ObjectFactory 暂不支持解码该 {nameof(IFieldList)} 实现：{target?.GetType().Name}");
        }
    }

    public static IField? CreateField(ulong handle, DwgVersion version,
        DwgObjectRecord record, int objStart)
        => version switch
        {
            DwgVersion.AC1015 => new FieldAc1015(handle, record, objStart),
            DwgVersion.AC1018 => new FieldAc1018(handle, record, objStart),
            DwgVersion.AC1021 => new FieldAcR2007(handle, record, objStart),
            DwgVersion.AC1024 => new FieldAcR2010(handle, record, objStart),
            DwgVersion.AC1027 => new FieldAcR2013(handle, record, objStart),
            DwgVersion.AC1032 => new FieldAcR2018(handle, record, objStart),
            _ => null,
        };

    public static void Decode(IField target, BitReader br)
    {
        switch (target)
        {
            case FieldRecord f:
                f.Read(br);
                break;
            default:
                throw new System.NotSupportedException(
                    $"ObjectFactory 暂不支持解码该 {nameof(IField)} 实现：{target?.GetType().Name}");
        }
    }

    public static IUnderlayDefinition? CreateUnderlayDefinition(ulong handle, DwgVersion version,
        DwgObjectRecord record, int objStart)
        => version switch
        {
            DwgVersion.AC1015 => new UnderlayDefinitionAcR2000(handle, record, objStart),
            DwgVersion.AC1018 => new UnderlayDefinitionAcR2004(handle, record, objStart),
            _ => null,
        };

    public static void Decode(IUnderlayDefinition target, BitReader br)
    {
        switch (target)
        {
            case UnderlayDefinitionRecord ud:
                ud.Read(br);
                break;
            default:
                throw new System.NotSupportedException(
                    $"ObjectFactory 暂不支持解码该 {nameof(IUnderlayDefinition)} 实现：{target?.GetType().Name}");
        }
    }
}
