namespace DwgParser.Core.Objects.Entities.Codecs;

internal static class EntityCodecBootstrap
{
    internal static void RegisterAll(EntityCodecRegistry registry)
    {
        registry.Register(new LineCodec());
        registry.Register(new CircleCodec());
        registry.Register(new ArcCodec());
        registry.Register(new EllipseCodec());
        registry.Register(new PointCodec());
        registry.Register(new RayCodec());
        registry.Register(new XLineCodec());
        registry.Register(new SplineCodec());
        registry.Register(new LwPolylineCodec());
        registry.Register(new Polyline2DCodec());
        registry.Register(new Vertex2DCodec());
        registry.Register(new TextCodec());
        registry.Register(new AttributeCodec());
        registry.Register(new AttDefCodec());
        registry.Register(new MTextCodec());
        registry.Register(new DimensionCodec());
        registry.Register(new LeaderCodec());
        registry.Register(new ToleranceCodec());
        registry.Register(new MLeaderCodec());
        registry.Register(new InsertCodec());
        registry.Register(new BlockCodec());
        registry.Register(new EndBlkCodec());
        registry.Register(new SeqEndCodec());
        registry.Register(new SolidCodec());
        registry.Register(new TraceCodec());
        registry.Register(new FaceCodec());
        registry.Register(new RegionCodec());
        registry.Register(new Solid3dCodec());
        registry.Register(new BodyCodec());
        registry.Register(new MLineCodec());
        registry.Register(new ViewportCodec());
        registry.Register(new HatchCodec());
        registry.Register(new TableCodec());
    }
}
