using DwgParser.Core;

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

    public static InsertEntity CreateInsert(ulong handle, bool r2000)
    {
        InsertEntity ins = r2000 ? new InsertAcR2000() : new InsertAcR2004Plus();
        ins.Handle = handle;
        return ins;
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

    public static PointEntity CreatePoint(ulong handle) => new() { Handle = handle };

    public static SplineEntity CreateSpline(ulong handle, DwgVersion version)
        => version >= DwgVersion.AC1027
            ? new SplineAcR2013Plus { Handle = handle }
            : new SplineAcPreR2013 { Handle = handle };

    public static RayEntity CreateRay(ulong handle) => new() { Handle = handle };

    public static XLineEntity CreateXLine(ulong handle) => new() { Handle = handle };
}
