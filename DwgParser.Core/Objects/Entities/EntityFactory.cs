using System.Collections.Generic;
using DwgParser.Core;
using DwgParser.Core.IO;

namespace DwgParser.Core.Objects;

public static partial class EntityFactory
{
    public static LineEntity CreateLine(ulong handle)
    {
        var line = new LineAcR2000Plus();
        line.Handle = handle;
        return line;
    }

    public static TableEntity CreateTable(ulong handle, bool r2010Plus)
    {
        TableEntity table = r2010Plus ? new TableAcR2010Plus() : new TableAcPreR2010();
        table.Handle = handle;
        return table;
    }

    public static LwPolylineEntity CreateLwPolyline(ulong handle)
    {
        LwPolylineEntity pline = new LwPolylineAcR2000Plus();
        pline.Handle = handle;
        return pline;
    }

    public static TextEntity CreateText(ulong handle)
    {
        TextEntity text = new TextAcR2000Plus();
        text.Handle = handle;
        return text;
    }

    public static AttributeEntity CreateAttribute(ulong handle, bool r2007Plus,
        bool r2010Plus, bool r2018Plus)
    {
        AttributeEntity a = new AttributeAcR2000Plus();
        a.Handle = handle;
        a.HasLockPosition = r2007Plus;
        a.HasKeepDuplicate = false;
        a.HasLockedInBlock = r2010Plus;
        a.HasMtextType = r2018Plus;
        return a;
    }

    public static AttDefEntity CreateAttDef(ulong handle, bool r2007Plus,
        bool r2010Plus, bool r2018Plus)
    {
        AttDefEntity a = new AttDefAcR2000Plus();
        a.Handle = handle;
        a.HasLockPosition = r2007Plus;
        a.HasKeepDuplicate = r2010Plus;
        a.HasLockedInBlock = r2010Plus;
        a.HasMtextType = r2018Plus;
        return a;
    }

    public static MLeaderEntity CreateMLeader(ulong handle, DwgVersion version)
    {
        if (version >= DwgVersion.AC1024)
        {
            var ml = new MLeaderAcR2010Plus { Handle = handle, IsR2013Plus = version >= DwgVersion.AC1027 };
            return ml;
        }
        return new MLeaderAcPreR2010 { Handle = handle };
    }

    public static SolidEntity CreateSolid(ulong handle) => new() { Handle = handle };

    public static TraceEntity CreateTrace(ulong handle) => new() { Handle = handle };

    public static FaceEntity CreateFace(ulong handle)
    {
        FaceEntity f = new FaceAcR2000Plus();
        f.Handle = handle;
        return f;
    }

    public static LeaderEntity CreateLeader(ulong handle) => new() { Handle = handle };

    public static MLineEntity CreateMLine(ulong handle) => new() { Handle = handle };

    public static ToleranceEntity CreateTolerance(ulong handle, bool r2007Plus = false) => r2007Plus
        ? new ToleranceAcR2007Plus { Handle = handle }
        : new ToleranceAcR2000Plus { Handle = handle };

    public static RegionEntity CreateRegion(ulong handle) => new() { Handle = handle };

    public static Solid3dEntity CreateSolid3d(ulong handle) => new() { Handle = handle };

    public static BodyEntity CreateBody(ulong handle) => new() { Handle = handle };

    public static BlockEntity CreateBlock(ulong handle) => new() { Handle = handle };

    public static EndBlkEntity CreateEndBlk(ulong handle) => new() { Handle = handle };

    public static SeqEndEntity CreateSeqEnd(ulong handle) => new() { Handle = handle };

    public static HatchEntity CreateHatch(ulong handle, DwgVersion version)
    {
        var h = new HatchEntity
        {
            Handle = handle,
            HasGradientBlock = version.IsR2004Plus(),
            HasSplineFitpoints = version.IsR2010Plus(),
        };
        return h;
    }

    public static ViewportEntity CreateViewport(ulong handle, DwgVersion version)
        => new ViewportAcR2000Plus
        {
            Handle = handle,
            HasGridMajor = version >= DwgVersion.AC1021,
            HasShadePlot = version >= DwgVersion.AC1018,
            HasLightingBlock = version >= DwgVersion.AC1021,
        };

    public static MTextEntity CreateMText(ulong handle, DwgVersion version)
    {
        MTextEntity mtext =
            version == DwgVersion.AC1015 ? new MTextAcR2000() :
            version == DwgVersion.AC1018 ? new MTextAcR2004() :
            version < DwgVersion.AC1032 ? new MTextAcR2007() :
            new MTextAcR2018Plus();
        mtext.Handle = handle;
        return mtext;
    }

    public static void Decode(ILine target, BitReader br)
    {
        switch (target)
        {
            case LineEntity line:
                line.ReadGeometry(br);
                break;
            default:
                throw new System.NotSupportedException(
                    $"EntityFactory 暂不支持解码该 {nameof(ILine)} 实现：{target?.GetType().Name}");
        }
    }

    public static void Decode(ITable target, BitReader br, DwgVersion version, DwgObjectRecord? record = null)
    {
        switch (target)
        {
            case TableEntity table:
                table.ReadBody(br, version, record);
                break;
            default:
                throw new System.NotSupportedException(
                    $"EntityFactory 暂不支持解码该 {nameof(ITable)} 实现：{target?.GetType().Name}");
        }
    }

    public static void Decode(ILwPolyline target, BitReader br)
    {
        switch (target)
        {
            case LwPolylineEntity pline:
                pline.ReadBody(br);
                break;
            default:
                throw new System.NotSupportedException(
                    $"EntityFactory 暂不支持解码该 {nameof(ILwPolyline)} 实现：{target?.GetType().Name}");
        }
    }

    public static void Decode(IText target, BitReader br, DwgObjectRecord? record)
    {
        switch (target)
        {
            case TextEntity text:
                text.ReadText(br, record);
                break;
            default:
                throw new System.NotSupportedException(
                    $"EntityFactory 暂不支持解码该 {nameof(IText)} 实现：{target?.GetType().Name}");
        }
    }

    public static void Decode(IAttribute target, BitReader br, DwgObjectRecord? record)
    {
        switch (target)
        {
            case AttributeEntity a:
                a.ReadBody(br, record);
                break;
            default:
                throw new System.NotSupportedException(
                    $"EntityFactory 暂不支持解码该 {nameof(IAttribute)} 实现：{target?.GetType().Name}");
        }
    }

    public static void Decode(IAttDef target, BitReader br, DwgObjectRecord? record)
    {
        switch (target)
        {
            case AttDefEntity a:
                a.ReadBody(br, record);
                break;
            default:
                throw new System.NotSupportedException(
                    $"EntityFactory 暂不支持解码该 {nameof(IAttDef)} 实现：{target?.GetType().Name}");
        }
    }

    public static void Decode(IMLeader target, BitReader br, DwgObjectRecord? record)
    {
        switch (target)
        {
            case MLeaderEntity ml:
                ml.ReadBody(br, record);
                break;
            default:
                throw new System.NotSupportedException(
                    $"EntityFactory 暂不支持解码该 {nameof(IMLeader)} 实现：{target?.GetType().Name}");
        }
    }

    public static void Decode(ISolid target, BitReader br)
    {
        switch (target)
        {
            case SolidEntity s:
                s.ReadBody(br);
                break;
            default:
                throw new System.NotSupportedException(
                    $"EntityFactory 暂不支持解码该 {nameof(ISolid)} 实现：{target?.GetType().Name}");
        }
    }

    public static void Decode(IMLine target, BitReader br, DwgObjectRecord? record,
        DwgVersion version, ulong handle, int objStart)
    {
        switch (target)
        {
            case MLineEntity m:
                m.ReadBody(br, record, version, handle, objStart);
                break;
            default:
                throw new System.NotSupportedException(
                    $"EntityFactory 暂不支持解码该 {nameof(IMLine)} 实现：{target?.GetType().Name}");
        }
    }

    public static void Decode(ITolerance target, BitReader br, DwgObjectRecord? record,
        DwgVersion version, ulong handle, int objStart)
    {
        switch (target)
        {
            case ToleranceEntity t:
                t.ReadBody(br, record, version, handle, objStart);
                break;
            default:
                throw new System.NotSupportedException(
                    $"EntityFactory 暂不支持解码该 {nameof(ITolerance)} 实现：{target?.GetType().Name}");
        }
    }

    public static void Decode(IRegion target, BitReader br, DwgObjectRecord? record)
    {
        switch (target)
        {
            case RegionEntity r:
                r.ReadBody(br, record);
                break;
            default:
                throw new System.NotSupportedException(
                    $"EntityFactory 暂不支持解码该 {nameof(IRegion)} 实现：{target?.GetType().Name}");
        }
    }

    public static void Decode(ISolid3d target, BitReader br, DwgObjectRecord? record)
    {
        switch (target)
        {
            case Solid3dEntity s:
                s.ReadBody(br, record);
                break;
            default:
                throw new System.NotSupportedException(
                    $"EntityFactory 暂不支持解码该 {nameof(ISolid3d)} 实现：{target?.GetType().Name}");
        }
    }

    public static void Decode(IBody target, BitReader br, DwgObjectRecord? record)
    {
        switch (target)
        {
            case BodyEntity b:
                b.ReadBody(br, record);
                break;
            default:
                throw new System.NotSupportedException(
                    $"EntityFactory 暂不支持解码该 {nameof(IBody)} 实现：{target?.GetType().Name}");
        }
    }

    public static void Decode(IBlock target, BitReader br, DwgObjectRecord? record,
        DwgVersion version, ulong handle, int objStart)
    {
        switch (target)
        {
            case BlockEntity b:
                b.ReadBody(br, record, version, handle, objStart);
                break;
            default:
                throw new System.NotSupportedException(
                    $"EntityFactory 暂不支持解码该 {nameof(IBlock)} 实现：{target?.GetType().Name}");
        }
    }

    public static void Decode(IEndBlk target, BitReader br)
    {
        switch (target)
        {
            case EndBlkEntity e:
                e.ReadBody(br);
                break;
            default:
                throw new System.NotSupportedException(
                    $"EntityFactory 暂不支持解码该 {nameof(IEndBlk)} 实现：{target?.GetType().Name}");
        }
    }

    public static void Decode(ISeqEnd target, BitReader br)
    {
        switch (target)
        {
            case SeqEndEntity s:
                s.ReadBody(br);
                break;
            default:
                throw new System.NotSupportedException(
                    $"EntityFactory 暂不支持解码该 {nameof(ISeqEnd)} 实现：{target?.GetType().Name}");
        }
    }

    public static void Decode(ITrace target, BitReader br)
    {
        switch (target)
        {
            case TraceEntity t:
                t.ReadBody(br);
                break;
            default:
                throw new System.NotSupportedException(
                    $"EntityFactory 暂不支持解码该 {nameof(ITrace)} 实现：{target?.GetType().Name}");
        }
    }

    public static void Decode(IFace target, BitReader br)
    {
        switch (target)
        {
            case FaceEntity f:
                f.ReadBody(br);
                break;
            default:
                throw new System.NotSupportedException(
                    $"EntityFactory 暂不支持解码该 {nameof(IFace)} 实现：{target?.GetType().Name}");
        }
    }

    public static void Decode(ILeader target, BitReader br, DwgObjectRecord? record = null)
    {
        switch (target)
        {
            case LeaderEntity l:
                l.ReadBody(br, record);
                break;
            default:
                throw new System.NotSupportedException(
                    $"EntityFactory 暂不支持解码该 {nameof(ILeader)} 实现：{target?.GetType().Name}");
        }
    }

    public static void Decode(IHatch target, BitReader br, DwgObjectRecord? record)
    {
        switch (target)
        {
            case HatchEntity h:
                h.ReadBody(br, record);
                break;
            default:
                throw new System.NotSupportedException(
                    $"EntityFactory 暂不支持解码该 {nameof(IHatch)} 实现：{target?.GetType().Name}");
        }
    }

    public static void Decode(IViewport target, BitReader br)
    {
        switch (target)
        {
            case ViewportEntity v:
                v.ReadBody(br);
                break;
            default:
                throw new System.NotSupportedException(
                    $"EntityFactory 暂不支持解码该 {nameof(IViewport)} 实现：{target?.GetType().Name}");
        }
    }

    public static void Decode(IMText target, BitReader br, DwgObjectRecord? record)
    {
        switch (target)
        {
            case MTextEntity mtext:
                mtext.ReadBody(br, record);
                break;
            default:
                throw new System.NotSupportedException(
                    $"EntityFactory 暂不支持解码该 {nameof(IMText)} 实现：{target?.GetType().Name}");
        }
    }

    public static DimensionEntity CreateDimension(ulong handle, short typeCode)
    {
        DimensionEntity dim = typeCode switch
        {
            0x14 => new DimensionOrdinate(),
            0x15 => new DimensionLinear(),
            0x16 => new DimensionAligned(),
            0x17 => new DimensionAng3pt(),
            0x18 => new DimensionAng2ln(),
            0x19 => new DimensionRadius(),
            0x1A => new DimensionDiameter(),
            _ => throw new System.NotSupportedException($"未知的 DIMENSION 子类型码 0x{typeCode:X}"),
        };
        dim.Handle = handle;
        return dim;
    }

    public static void Decode(IDimension target, BitReader br, DwgObjectRecord? record,
        List<DwgObjectReader.DimFieldEntry>? trace, DwgVersion version)
    {
        switch (target)
        {
            case DimensionEntity dim:
                dim.ReadGeometry(br, record, trace, version);
                break;
            default:
                throw new System.NotSupportedException(
                    $"EntityFactory 暂不支持解码该 {nameof(IDimension)} 实现：{target?.GetType().Name}");
        }
    }

    public static InsertEntity CreateInsert(ulong handle, bool r2000)
    {
        InsertEntity ins = r2000 ? new InsertAcR2000() : new InsertAcR2004Plus();
        ins.Handle = handle;
        return ins;
    }

    public static bool Decode(IInsert target, BitReader br, DwgObjectRecord? record)
    {
        switch (target)
        {
            case InsertEntity ins:
                return ins.ReadGeometry(br, record);
            default:
                throw new System.NotSupportedException(
                    $"EntityFactory 暂不支持解码该 {nameof(IInsert)} 实现：{target?.GetType().Name}");
        }
    }


    public static CircleEntity CreateCircle(ulong handle)
    {
        var c = new CircleEntity { Handle = handle };
        return c;
    }

    public static ArcEntity CreateArc(ulong handle)
    {
        var a = new ArcEntity { Handle = handle };
        return a;
    }

    public static EllipseEntity CreateEllipse(ulong handle)
    {
        var el = new EllipseEntity { Handle = handle };
        return el;
    }

    public static void Decode(ICircle target, BitReader br)
    {
        switch (target)
        {
            case CircleEntity c:
                c.ReadGeometry(br);
                break;
            default:
                throw new System.NotSupportedException(
                    $"EntityFactory 暂不支持解码该 {nameof(ICircle)} 实现：{target?.GetType().Name}");
        }
    }

    public static void Decode(IArc target, BitReader br)
    {
        switch (target)
        {
            case ArcEntity a:
                a.ReadGeometry(br);
                break;
            default:
                throw new System.NotSupportedException(
                    $"EntityFactory 暂不支持解码该 {nameof(IArc)} 实现：{target?.GetType().Name}");
        }
    }

    public static void Decode(IEllipse target, BitReader br)
    {
        switch (target)
        {
            case EllipseEntity el:
                el.ReadGeometry(br);
                break;
            default:
                throw new System.NotSupportedException(
                    $"EntityFactory 暂不支持解码该 {nameof(IEllipse)} 实现：{target?.GetType().Name}");
        }
    }

    public static Polyline2DEntity CreatePolyline2D(ulong handle)
    {
        var p = new Polyline2DEntity { Handle = handle };
        return p;
    }

    public static Vertex2DEntity CreateVertex2D(ulong handle)
    {
        var v = new Vertex2DEntity { Handle = handle };
        return v;
    }

    public static void Decode(IPolyline2D target, BitReader br)
    {
        switch (target)
        {
            case Polyline2DEntity p:
                p.ReadGeometry(br);
                break;
            default:
                throw new System.NotSupportedException(
                    $"EntityFactory 暂不支持解码该 {nameof(IPolyline2D)} 实现：{target?.GetType().Name}");
        }
    }

    public static void Decode(IVertex2D target, BitReader br)
    {
        switch (target)
        {
            case Vertex2DEntity v:
                v.ReadGeometry(br);
                break;
            default:
                throw new System.NotSupportedException(
                    $"EntityFactory 暂不支持解码该 {nameof(IVertex2D)} 实现：{target?.GetType().Name}");
        }
    }


    public static PointEntity CreatePoint(ulong handle) => new() { Handle = handle };

    public static SplineEntity CreateSpline(ulong handle, DwgVersion version)
        => version >= DwgVersion.AC1027
            ? new SplineAcR2013Plus { Handle = handle }
            : new SplineAcPreR2013 { Handle = handle };

    public static RayEntity CreateRay(ulong handle) => new() { Handle = handle };

    public static XLineEntity CreateXLine(ulong handle) => new() { Handle = handle };

    public static void Decode(IPoint target, BitReader br)
    {
        switch (target)
        {
            case PointEntity p:
                p.ReadGeometry(br);
                break;
            default:
                throw new System.NotSupportedException(
                    $"EntityFactory 暂不支持解码该 {nameof(IPoint)} 实现：{target?.GetType().Name}");
        }
    }

    public static void Decode(ISpline target, BitReader br)
    {
        switch (target)
        {
            case SplineEntity s:
                s.ReadGeometry(br);
                break;
            default:
                throw new System.NotSupportedException(
                    $"EntityFactory 暂不支持解码该 {nameof(ISpline)} 实现：{target?.GetType().Name}");
        }
    }

    public static void Decode(IRay target, BitReader br)
    {
        switch (target)
        {
            case RayEntity r:
                r.ReadGeometry(br);
                break;
            default:
                throw new System.NotSupportedException(
                    $"EntityFactory 暂不支持解码该 {nameof(IRay)} 实现：{target?.GetType().Name}");
        }
    }

    public static void Decode(IXLine target, BitReader br)
    {
        switch (target)
        {
            case XLineEntity x:
                x.ReadGeometry(br);
                break;
            default:
                throw new System.NotSupportedException(
                    $"EntityFactory 暂不支持解码该 {nameof(IXLine)} 实现：{target?.GetType().Name}");
        }
    }
}
