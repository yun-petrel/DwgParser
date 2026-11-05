using DwgParser.Core.Render;

namespace DwgParser.Core.Objects;

public enum DwgEntityType
{
    Unknown, Text, Insert, Vertex2D, Attrib, SeqEnd, Polyline2D, Arc,
    Circle, Line, Point, Solid, Trace, Ellipse, LwPolyline, BlockHeader, EndBlk, Block,
    Dimension, Spline, Ray, XLine, Leader, Hatch, MText, MLine, Tolerance, Viewport, Region, Solid3d, Body,
    AttDef, Table, MLeader,

    PointSequence,
}

public readonly record struct Vec2(double X, double Y);

public readonly record struct Vec3(double X, double Y, double Z);

public class DwgEntity : EntityBase, IEntity
{
    EntityKind IEntity.Kind => ToEntityKind(Kind);

    private LineGeometry? _lineGeo;
    private RadialGeometry? _radialGeo;
    private PolylineGeometry? _polyGeo;
    private CornersGeometry? _cornersGeo;
    private TextGeometry? _textGeo;
    private DimensionGeometry? _dimGeo;
    private TableGeometry? _tableGeo;
    private InsertGeometry? _insertGeo;
    private LeaderGeometry? _leaderGeo;
    private ViewportGeometry? _viewportGeo;

    private LineGeometry LineGeo => _lineGeo ??= new();
    private RadialGeometry RadialGeo => _radialGeo ??= new();
    private PolylineGeometry PolyGeo => _polyGeo ??= new();
    private CornersGeometry CornersGeo => _cornersGeo ??= new();
    private TextGeometry TextGeo => _textGeo ??= new();
    private DimensionGeometry DimGeo => _dimGeo ??= new();
    private TableGeometry TableGeo => _tableGeo ??= new();
    private InsertGeometry InsertGeo => _insertGeo ??= new();
    private LeaderGeometry LeaderGeo => _leaderGeo ??= new();
    private ViewportGeometry ViewportGeo => _viewportGeo ??= new();

    public Vec2 Start { get => LineGeo.Start; set => LineGeo.Start = value; }
    public Vec2 End { get => LineGeo.End; set => LineGeo.End = value; }

    public double StartZ { get => LineGeo.StartZ; set => LineGeo.StartZ = value; }

    public double EndZ { get => LineGeo.EndZ; set => LineGeo.EndZ = value; }

    public Vec2 Center { get => RadialGeo.Center; set => RadialGeo.Center = value; }
    public double Radius { get => RadialGeo.Radius; set => RadialGeo.Radius = value; }
    public double StartAngle { get => RadialGeo.StartAngle; set => RadialGeo.StartAngle = value; }
    public double EndAngle { get => RadialGeo.EndAngle; set => RadialGeo.EndAngle = value; }

    public string TextValue { get => TextGeo.TextValue; set => TextGeo.TextValue = value; }
    public double Height { get; set; }
    public double Rotation { get; set; }
    public double WidthFactor { get; set; } = 1.0;

    public List<Vec2> Points => PolyGeo.Points;
    public List<double> Bulges => PolyGeo.Bulges;
    public ulong BlockHeaderHandle { get => InsertGeo.BlockHeaderHandle; set => InsertGeo.BlockHeaderHandle = value; }
    public bool HasAttributes { get => InsertGeo.HasAttributes; set => InsertGeo.HasAttributes = value; }
    public double ScaleX { get => InsertGeo.ScaleX; set => InsertGeo.ScaleX = value; }
    public double ScaleY { get => InsertGeo.ScaleY; set => InsertGeo.ScaleY = value; }
    public double ScaleZ { get => InsertGeo.ScaleZ; set => InsertGeo.ScaleZ = value; }

    public Matrix InsertPlacement()
        => Matrix.Translation(Start.X, Start.Y)
           * Matrix.Rotation(Rotation)
           * Matrix.Scale(ScaleX, ScaleY);

    public double ConstantWidth { get => PolyGeo.ConstantWidth; set => PolyGeo.ConstantWidth = value; }

    public List<Vec2> Corners => CornersGeo.Corners;

    public bool SolidFill { get => CornersGeo.SolidFill; set => CornersGeo.SolidFill = value; }

    public string Name { get; set; } = "";


    public bool Closed { get => PolyGeo.Closed; set => PolyGeo.Closed = value; }

    public List<double> VertexStartWidths => PolyGeo.VertexStartWidths;

    public List<double> VertexEndWidths => PolyGeo.VertexEndWidths;

    public bool PlineGen { get => PolyGeo.PlineGen; set => PolyGeo.PlineGen = value; }

    public string Tag { get => TextGeo.Tag; set => TextGeo.Tag = value; }

    public string Prompt { get => TextGeo.Prompt; set => TextGeo.Prompt = value; }

    public byte AttFlags { get => TextGeo.AttFlags; set => TextGeo.AttFlags = value; }

    public short AttFieldLength { get => TextGeo.AttFieldLength; set => TextGeo.AttFieldLength = value; }

    public int AttIsLockedInBlock { get => TextGeo.AttIsLockedInBlock; set => TextGeo.AttIsLockedInBlock = value; }

    public int AttKeepDuplicateRecords { get => TextGeo.AttKeepDuplicateRecords; set => TextGeo.AttKeepDuplicateRecords = value; }

    public int AttMTextType { get => TextGeo.AttMTextType; set => TextGeo.AttMTextType = value; }

    public short TextAttachment { get => TextGeo.TextAttachment; set => TextGeo.TextAttachment = value; }

    public double RectWidth { get => TextGeo.RectWidth; set => TextGeo.RectWidth = value; }

    public double RectHeight { get => TextGeo.RectHeight; set => TextGeo.RectHeight = value; }

    public short LineSpacingStyle { get => TextGeo.LineSpacingStyle; set => TextGeo.LineSpacingStyle = value; }
    public double LineSpacingFactor { get => TextGeo.LineSpacingFactor; set => TextGeo.LineSpacingFactor = value; }

    public Vec2 AlignmentPoint { get => TextGeo.AlignmentPoint; set => TextGeo.AlignmentPoint = value; }

    public double ObliqueAngle { get => TextGeo.ObliqueAngle; set => TextGeo.ObliqueAngle = value; }
    public short TextGenerationFlags { get => TextGeo.TextGenerationFlags; set => TextGeo.TextGenerationFlags = value; }

    public short HorizMode { get => TextGeo.HorizMode; set => TextGeo.HorizMode = value; }
    public short VertMode { get => TextGeo.VertMode; set => TextGeo.VertMode = value; }

    public ulong TextStyleHandle { get => TextGeo.TextStyleHandle; set => TextGeo.TextStyleHandle = value; }

    public Vec2 Dir { get => LeaderGeo.Dir; set => LeaderGeo.Dir = value; }

    public bool ArrowheadOn { get => LeaderGeo.ArrowheadOn; set => LeaderGeo.ArrowheadOn = value; }

    public short ArrowheadType { get => LeaderGeo.ArrowheadType; set => LeaderGeo.ArrowheadType = value; }

    public double VportWidth { get => ViewportGeo.VportWidth; set => ViewportGeo.VportWidth = value; }
    public Vec2 VportViewCenter { get => ViewportGeo.VportViewCenter; set => ViewportGeo.VportViewCenter = value; }
    public double VportViewSize { get => ViewportGeo.VportViewSize; set => ViewportGeo.VportViewSize = value; }
    public double VportTwist { get => ViewportGeo.VportTwist; set => ViewportGeo.VportTwist = value; }
    public double VportSnapAngle { get => ViewportGeo.VportSnapAngle; set => ViewportGeo.VportSnapAngle = value; }
    public Vec2 VportViewDirection { get => ViewportGeo.VportViewDirection; set => ViewportGeo.VportViewDirection = value; }
    public double VportFrontClipZ { get => ViewportGeo.VportFrontClipZ; set => ViewportGeo.VportFrontClipZ = value; }
    public double VportBackClipZ { get => ViewportGeo.VportBackClipZ; set => ViewportGeo.VportBackClipZ = value; }
    public Vec2 VportViewTarget { get => ViewportGeo.VportViewTarget; set => ViewportGeo.VportViewTarget = value; }

    public double Measurement { get => DimGeo.Measurement; set => DimGeo.Measurement = value; }
    public Vec2 DimTextMid { get => DimGeo.DimTextMid; set => DimGeo.DimTextMid = value; }
    public Vec2 DimXLine1 { get => DimGeo.DimXLine1; set => DimGeo.DimXLine1 = value; }
    public Vec2 DimXLine2 { get => DimGeo.DimXLine2; set => DimGeo.DimXLine2 = value; }
    public Vec2 DimDefPoint { get => DimGeo.DimDefPoint; set => DimGeo.DimDefPoint = value; }
    public Vec2 DimDefPoint2 { get => DimGeo.DimDefPoint2; set => DimGeo.DimDefPoint2 = value; }
    public Vec2 DimLinePoint { get => DimGeo.DimLinePoint; set => DimGeo.DimLinePoint = value; }
    public Vec2 DimCenter { get => DimGeo.DimCenter; set => DimGeo.DimCenter = value; }
    public ulong DimBlockHandle { get => DimGeo.DimBlockHandle; set => DimGeo.DimBlockHandle = value; }
    public ulong DimStyleHandle { get => DimGeo.DimStyleHandle; set => DimGeo.DimStyleHandle = value; }
    public string DimSubtype { get => DimGeo.DimSubtype; set => DimGeo.DimSubtype = value; }
    public double DimTextRotation { get => DimGeo.DimTextRotation; set => DimGeo.DimTextRotation = value; }
    public double DimLineRotation { get => DimGeo.DimLineRotation; set => DimGeo.DimLineRotation = value; }

    public short TableRows { get => TableGeo.TableRows; set => TableGeo.TableRows = value; }
    public short TableColumns { get => TableGeo.TableColumns; set => TableGeo.TableColumns = value; }

    public static EntityKind ToEntityKind(DwgEntityType k) => k switch
    {
        DwgEntityType.Line => EntityKind.Line,
        DwgEntityType.Circle => EntityKind.Circle,
        DwgEntityType.Arc => EntityKind.Arc,
        DwgEntityType.Point => EntityKind.Point,
        DwgEntityType.Text => EntityKind.Text,
        DwgEntityType.Insert => EntityKind.Insert,
        DwgEntityType.LwPolyline => EntityKind.LwPolyline,
        DwgEntityType.Polyline2D => EntityKind.Polyline2D,
        DwgEntityType.Vertex2D => EntityKind.Vertex2D,
        DwgEntityType.Solid => EntityKind.Solid,
        DwgEntityType.Trace => EntityKind.Trace,
        DwgEntityType.BlockHeader => EntityKind.BlockHeader,
        DwgEntityType.EndBlk => EntityKind.EndBlk,
        DwgEntityType.Block => EntityKind.Block,
        DwgEntityType.Attrib => EntityKind.Attrib,
        DwgEntityType.AttDef => EntityKind.AttDef,
        DwgEntityType.MText => EntityKind.MText,
        DwgEntityType.Ellipse => EntityKind.Ellipse,
        DwgEntityType.Spline => EntityKind.Spline,
        DwgEntityType.Leader => EntityKind.Leader,
        DwgEntityType.MLeader => EntityKind.MLeader,
        DwgEntityType.Hatch => EntityKind.Hatch,
        DwgEntityType.Dimension => EntityKind.Dimension,
        DwgEntityType.Viewport => EntityKind.Viewport,
        DwgEntityType.Table => EntityKind.Table,
        DwgEntityType.MLine => EntityKind.MLine,
        DwgEntityType.Tolerance => EntityKind.Tolerance,
        DwgEntityType.Ray => EntityKind.Ray,
        DwgEntityType.XLine => EntityKind.XLine,
        DwgEntityType.SeqEnd => EntityKind.SeqEnd,
        _ => EntityKind.Unknown,
    };


    public string ToSvg(RenderContext ctx, Matrix transform)
        => EntityRenderRegistry.TryRenderSvg(this, ctx, transform) ?? WriteSvg(ctx, transform);

    protected virtual string WriteSvg(RenderContext ctx, Matrix m) => "";
}
