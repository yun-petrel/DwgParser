using DwgParser.Core.IO;
using DwgParser.Core.Objects.Records;

namespace DwgParser.Core.Objects;

public static partial class ObjectFactory
{

    public static IDimStyle CreateDimStyle(ulong handle, DwgVersion version,
        DwgObjectReader.WalkResult result, DwgObjectRecord record, int objStart)
    {
        if (version.IsR2010Plus())
        {
            DimStyleAcR2010Plus r2010 = version switch
            {
                DwgVersion.AC1024 => new DimStyleAcR2010(handle, record, result, objStart),
                DwgVersion.AC1027 => new DimStyleAcR2013(handle, record, result, objStart),
                _ => new DimStyleAcR2018(handle, record, result, objStart),
            };
            return r2010;
        }
        return version switch
        {
            DwgVersion.AC1015 => new DimStyleAcR2000(handle, record, result, objStart),
            DwgVersion.AC1018 => new DimStyleAcR2004(handle, record, result, objStart),
            _ => new DimStyleAcR2007(handle, record, result, objStart),
        };
    }

    public static void Decode(IDimStyle target, BitReader br)
    {
        switch (target)
        {
            case DimStyleRecord dim:
                dim.Read(br);
                dim.CommitHandleRefs();
                break;
            default:
                throw new System.NotSupportedException(
                    $"ObjectFactory 暂不支持解码该 {nameof(IDimStyle)} 实现：{target?.GetType().Name}");
        }
    }

    public static IVisualStyle? CreateVisualStyle(ulong handle, DwgVersion version,
        DwgObjectRecord record, int objStart)
    {
        if (version.IsR2010Plus()) return new VisualStyleAcR2010Plus(handle, version, record, objStart);
        if (version == DwgVersion.AC1021) return new VisualStyleAcR2007(handle, version, record, objStart);
        if (version == DwgVersion.AC1018) return new VisualStyleAcR2004(handle, version, record, objStart);
        if (version == DwgVersion.AC1015) return new VisualStyleAcR2000(handle, version, record, objStart);
        return null;
    }

    public static bool Decode(IVisualStyle target, BitReader br)
    {
        switch (target)
        {
            case VisualStyleRecord vs:
                return vs.TryCapture(br);
            default:
                throw new System.NotSupportedException(
                    $"ObjectFactory 暂不支持解码该 {nameof(IVisualStyle)} 实现：{target?.GetType().Name}");
        }
    }

    public static IStyle CreateStyle(ulong handle, DwgVersion version,
        DwgObjectReader.WalkResult result, DwgObjectRecord record, int objStart)
    {
        if (version.IsR2010Plus())
            return version switch
            {
                DwgVersion.AC1024 => new StyleAcR2010(handle, record, result, objStart),
                DwgVersion.AC1027 => new StyleAcR2013(handle, record, result, objStart),
                _ => new StyleAcR2018(handle, record, result, objStart),
            };
        return version switch
        {
            DwgVersion.AC1015 => new StyleAcR2000(handle, record, result, objStart),
            DwgVersion.AC1018 => new StyleAcR2004(handle, record, result, objStart),
            _ => new StyleAcR2007(handle, record, result, objStart),
        };
    }

    public static void Decode(IStyle target, BitReader br)
    {
        switch (target)
        {
            case StyleRecord sty:
                sty.Read(br);
                break;
            default:
                throw new System.NotSupportedException(
                    $"ObjectFactory 暂不支持解码该 {nameof(IStyle)} 实现：{target?.GetType().Name}");
        }
    }

    public static ILtype? CreateLtype(ulong handle, DwgVersion version,
        DwgObjectRecord record, int objStart)
    {
        return version switch
        {
            DwgVersion.AC1015 => new LtypeAcR2000(handle, record, objStart),
            DwgVersion.AC1018 => new LtypeAcR2004(handle, record, objStart),
            DwgVersion.AC1021 => new LtypeAcR2007(handle, record, objStart),
            DwgVersion.AC1024 => new LtypeAcR2010(handle, record, objStart),
            DwgVersion.AC1027 => new LtypeAcR2013(handle, record, objStart),
            DwgVersion.AC1032 => new LtypeAcR2018(handle, record, objStart),
            _ => null,
        };
    }

    public static void Decode(ILtype target, BitReader br)
    {
        switch (target)
        {
            case LtypeRecord lt:
                lt.Read(br);
                break;
            default:
                throw new System.NotSupportedException(
                    $"ObjectFactory 暂不支持解码该 {nameof(ILtype)} 实现：{target?.GetType().Name}");
        }
    }

    public static ILayer CreateLayer(ulong handle, DwgVersion version,
        DwgObjectRecord record, DwgObjectReader.WalkResult result, int objStart)
        => CreateLayerRecord(handle, version, record, result, objStart);

    public static LayerRecord CreateLayerRecord(ulong handle, DwgVersion version,
        DwgObjectRecord record, DwgObjectReader.WalkResult result, int objStart)
    {
        return version switch
        {
            DwgVersion.AC1018 => new LayerAcR2004(handle, record, result, objStart),
            DwgVersion.AC1021 => new LayerAcR2007(handle, record, result, objStart),
            DwgVersion.AC1024 => new LayerAcR2010(handle, record, result, objStart),
            DwgVersion.AC1027 => new LayerAcR2013(handle, record, result, objStart),
            DwgVersion.AC1032 => new LayerAcR2018(handle, record, result, objStart),
            _ => new LayerAcR2000(handle, record, result, objStart),
        };
    }

    public static bool Decode(ILayer target, BitReader br)
    {
        return target switch
        {
            LayerRecord lr => lr.Read(br),
            _ => throw new System.NotSupportedException(
                $"ObjectFactory 暂不支持解码该 {nameof(ILayer)} 实现：{target?.GetType().Name}"),
        };
    }

    public static IDictWdflt? CreateDictWdflt(ulong handle, DwgVersion version,
        DwgObjectRecord record, int objStart)
    {
        return version switch
        {
            DwgVersion.AC1015 => new DictWdfltAcR2000(handle, record, objStart),
            DwgVersion.AC1018 => new DictWdfltAcR2004(handle, record, objStart),
            DwgVersion.AC1021 => new DictWdfltAcRlForm(handle, record, objStart),
            DwgVersion.AC1024 or DwgVersion.AC1027 or DwgVersion.AC1032
                => new DictWdfltAcBotForm(handle, record, objStart),
            _ => null,
        };
    }

    public static void Decode(IRecord target, BitReader br)
    {
        switch (target)
        {
            case ProxyObjectRecord px:
                px.Read(br);
                break;
            default:
                throw new System.NotSupportedException(
                    $"ObjectFactory 暂不支持解码该 {nameof(IRecord)} 实现：{target?.GetType().Name}");
        }
    }

    public static void Decode(IDictWdflt target, BitReader br)
    {
        switch (target)
        {
            case DictWdfltRecord dw:
                dw.Read(br);
                break;
            default:
                throw new System.NotSupportedException(
                    $"ObjectFactory 暂不支持解码该 {nameof(IDictWdflt)} 实现：{target?.GetType().Name}");
        }
    }

    public static IRecord? CreateProxyObject(ulong handle, DwgVersion version,
        DwgObjectRecord record, int objStart,
        IReadOnlyList<DwgParser.Core.Format.R2000Class>? classes = null)
    {
        return version switch
        {
            DwgVersion.AC1015 => new ProxyObjectAcR2000(handle, record, objStart, classes),
            DwgVersion.AC1018 => new ProxyObjectAcR2004(handle, record, objStart, classes),
            >= DwgVersion.AC1021 => new ProxyObjectAcR2007Plus(handle, record, objStart, classes),
            _ => null,
        };
    }

    internal static ProxyObjectCarryPlan? CreateProxyObjectCarry(DwgObjectRecord rec,
        DwgObjectStreamWriter.Target t)
    {
        if (rec.SourceVersion == DwgVersion.AC1018 && t.Version == DwgVersion.AC1021)
            return new ProxyObjectCarryAc1018ToAc1021();
        return null;
    }

    public static IMaterial? CreateMaterial(ulong handle, DwgVersion version,
        DwgObjectRecord record, int objStart)
    {
        if (record.LogicalType != MaterialRecord.TypeCodeValue) return null;
        return version switch
        {
            DwgVersion.AC1018 => new MaterialAcR2004(handle, record, objStart),
            DwgVersion.AC1015 => new MaterialAcR2000(handle, record, objStart),
            DwgVersion.AC1021 => new MaterialAcRlForm(handle, record, objStart),
            DwgVersion.AC1024 or DwgVersion.AC1027 or DwgVersion.AC1032
                => new MaterialAcBotForm(handle, record, objStart),
            _ => null,
        };
    }

    public static void Decode(IMaterial target, BitReader br)
    {
        switch (target)
        {
            case MaterialRecord mr:
                mr.Read(br);
                break;
            default:
                throw new System.NotSupportedException(
                    $"ObjectFactory 暂不支持解码该 {nameof(IMaterial)} 实现：{target?.GetType().Name}");
        }
    }

    public static ILayout? CreateLayout(ulong handle, DwgVersion version,
        DwgObjectRecord record, int objStart)
    {
        if (version == DwgVersion.AC1018 && record.LogicalType == LayoutRecord.TypeCodeValue)
            return new LayoutAcR2004(handle, record, objStart);
        if (version == DwgVersion.AC1015 && record.LogicalType == LayoutRecord.TypeCodeValue)
            return new LayoutAcR2000(handle, record, objStart);
        if (version == DwgVersion.AC1021 && record.LogicalType == LayoutRecord.TypeCodeValue)
            return new LayoutAcR2007(handle, record, objStart);
        if (version.IsR2010Plus() && record.LogicalType == LayoutRecord.TypeCodeValue)
            return version switch
            {
                DwgVersion.AC1024 => new LayoutAcR2010(handle, record, objStart),
                DwgVersion.AC1027 => new LayoutAcR2013(handle, record, objStart),
                DwgVersion.AC1032 => new LayoutAcR2018(handle, record, objStart),
                _ => null,
            };
        return null;
    }

    public static void Decode(ILayout target, BitReader br)
    {
        switch (target)
        {
            case LayoutRecord lr:
                lr.Read(br);
                break;
            default:
                throw new System.NotSupportedException(
                    $"ObjectFactory 暂不支持解码该 {nameof(ILayout)} 实现：{target?.GetType().Name}");
        }
    }

    public static IMLineStyle? CreateMLineStyle(ulong handle, DwgVersion version,
        DwgObjectRecord record, int objStart)
    {
        return version switch
        {
            DwgVersion.AC1015 => new MLineStyleAcR2000(handle, record, objStart),
            DwgVersion.AC1018 => new MLineStyleAcR2004(handle, record, objStart),
            DwgVersion.AC1021 => new MLineStyleAcRlForm(handle, record, objStart),
            DwgVersion.AC1024 or DwgVersion.AC1027 or DwgVersion.AC1032
                => new MLineStyleAcBotForm(handle, record, objStart),
            _ => null,
        };
    }

    public static void Decode(IMLineStyle target, BitReader br)
    {
        switch (target)
        {
            case MLineStyleRecord mr:
                mr.Read(br);
                break;
            default:
                throw new System.NotSupportedException(
                    $"ObjectFactory 暂不支持解码该 {nameof(IMLineStyle)} 实现：{target?.GetType().Name}");
        }
    }

    public static IDictVar? CreateDictVar(ulong handle, DwgVersion version,
        DwgObjectRecord record, int objStart)
    {
        return version switch
        {
            DwgVersion.AC1015 => new DictVarAcR2000(handle, record, objStart),
            DwgVersion.AC1018 => new DictVarAcR2004(handle, record, objStart),
            DwgVersion.AC1021 => new DictVarAcR2007(handle, record, objStart),
            DwgVersion.AC1024 => new DictVarAcR2010(handle, record, objStart),
            DwgVersion.AC1027 => new DictVarAcR2013(handle, record, objStart),
            DwgVersion.AC1032 => new DictVarAcR2018(handle, record, objStart),
            _ => null,
        };
    }

    public static void Decode(IDictVar target, BitReader br)
    {
        switch (target)
        {
            case DictVarRecord dr:
                dr.Read(br);
                break;
            default:
                throw new System.NotSupportedException(
                    $"ObjectFactory 暂不支持解码该 {nameof(IDictVar)} 实现：{target?.GetType().Name}");
        }
    }

    public static IImageDef? CreateImageDef(ulong handle, DwgVersion version,
        DwgObjectRecord record, int objStart)
    {
        return version switch
        {
            DwgVersion.AC1015 => new ImageDefAcR2000(handle, record, objStart),
            DwgVersion.AC1018 => new ImageDefAcR2004(handle, record, objStart),
            DwgVersion.AC1021 => new ImageDefAcR2007(handle, record, objStart),
            DwgVersion.AC1024 => new ImageDefAcR2010(handle, record, objStart),
            DwgVersion.AC1027 => new ImageDefAcR2013(handle, record, objStart),
            DwgVersion.AC1032 => new ImageDefAcR2018(handle, record, objStart),
            _ => null,
        };
    }

    public static void Decode(IImageDef target, BitReader br)
    {
        switch (target)
        {
            case ImageDefRecord idr:
                idr.Read(br);
                break;
            default:
                throw new System.NotSupportedException(
                    $"ObjectFactory 暂不支持解码该 {nameof(IImageDef)} 实现：{target?.GetType().Name}");
        }
    }

    public static IViewStyle? CreateViewStyle(ulong handle, DwgVersion version,
        DwgObjectRecord record, int objStart)
    {
        if (version == DwgVersion.AC1018)
            return record.LogicalType switch
            {
                510 => new SectionViewStyleAc1018(handle, record, objStart),
                511 => new DetailViewStyleAc1018(handle, record, objStart),
                _ => null,
            };
        if (version >= DwgVersion.AC1021)
            return record.LogicalType switch
            {
                510 => new SectionViewStyleAcR2007Plus(handle, record, objStart),
                511 => new DetailViewStyleAcR2007Plus(handle, record, objStart),
                _ => null,
            };
        if (version == DwgVersion.AC1015)
            return record.LogicalType switch
            {
                510 => new SectionViewStyleAcR2000(handle, record, objStart),
                511 => new DetailViewStyleAcR2000(handle, record, objStart),
                _ => null,
            };
        return null;
    }

    public static IViewStyleTemplate? CreateViewStyleTemplate(ulong handle, DwgVersion version,
        DwgObjectRecord record, int objStart)
    {
        bool r2010Plus = version.IsR2010Plus();
        if (r2010Plus || !version.IsSupportedVersion()) return null;
        return record.LogicalType is not (510 or 511) ? null
            : version >= DwgVersion.AC1021
                ? new ViewStyleTemplateAcR2007(handle, record, objStart)
                : new ViewStyleTemplateAcPreR2007(handle, record, objStart);
    }

    public static void Decode(IViewStyle target, BitReader br)
    {
        switch (target)
        {
            case ViewStyleRecord vr:
                vr.Read(br);
                break;
            default:
                throw new System.NotSupportedException(
                    $"ObjectFactory 暂不支持解码该 {nameof(IViewStyle)} 实现：{target?.GetType().Name}");
        }
    }

    public static void Decode(IViewStyleTemplate target, BitReader br)
    {
        switch (target)
        {
            case ViewStyleTemplateRecord vr:
                vr.Read(br);
                break;
            default:
                throw new System.NotSupportedException(
                    $"ObjectFactory 暂不支持解码该 {nameof(IViewStyleTemplate)} 实现：{target?.GetType().Name}");
        }
    }

    public static IMLeaderStyle? CreateMLeaderStyle(ulong handle, DwgVersion version,
        DwgObjectRecord record, int objStart)
    {
        if (version.IsR2010Plus()) return new MLeaderStyleAcR2010Plus(handle, version, record, objStart);
        if (version == DwgVersion.AC1021) return new MLeaderStyleAcR2007(handle, version, record, objStart);
        if (version == DwgVersion.AC1018) return new MLeaderStyleAcPreR2007(handle, version, record, objStart);
        if (version == DwgVersion.AC1015) return new MLeaderStyleAcR2000(handle, version, record, objStart);
        return null;
    }

    public static bool Decode(IMLeaderStyle target, BitReader br)
    {
        switch (target)
        {
            case MLeaderStyleRecord ml:
                return ml.TryCapture(br);
            default:
                throw new System.NotSupportedException(
                    $"ObjectFactory 暂不支持解码该 {nameof(IMLeaderStyle)} 实现：{target?.GetType().Name}");
        }
    }

    public static ITableStyle? CreateTableStyle(ulong handle, DwgVersion version,
        DwgObjectRecord record, int objStart)
    {
        if (version.IsR2010Plus()) return new TableStyleAcR2010Plus(handle, version, record, objStart);
        if (version >= DwgVersion.AC1015 && version <= DwgVersion.AC1021)
            return new TableStyleAcR2000toR2007(handle, version, record, objStart);
        return null;
    }

    public static bool Decode(ITableStyle target, BitReader br)
    {
        switch (target)
        {
            case TableStyleRecord ts:
                return ts.TryCapture(br);
            default:
                throw new System.NotSupportedException(
                    $"ObjectFactory 暂不支持解码该 {nameof(ITableStyle)} 实现：{target?.GetType().Name}");
        }
    }

    public static IVport? CreateVport(ulong handle, DwgVersion version,
        DwgObjectRecord record, int objStart)
        => version switch
        {
            DwgVersion.AC1015 => new VportAcR2000(handle, version, record, objStart),
            DwgVersion.AC1018 => new VportAcR2004(handle, version, record, objStart),
            DwgVersion.AC1021 => new VportAcR2007(handle, version, record, objStart),
            DwgVersion.AC1024 => new VportAcR2010(handle, version, record, objStart),
            DwgVersion.AC1027 => new VportAcR2013(handle, version, record, objStart),
            DwgVersion.AC1032 => new VportAcR2018(handle, version, record, objStart),
            _ => null,
        };

    public static bool Decode(IVport target, BitReader br)
    {
        switch (target)
        {
            case VportRecord vp:
                return vp.TryCapture(br);
            default:
                throw new System.NotSupportedException(
                    $"ObjectFactory 暂不支持解码该 {nameof(IVport)} 实现：{target?.GetType().Name}");
        }
    }

}
