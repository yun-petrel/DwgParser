using DwgParser.Core.IO;

namespace DwgParser.Core.Objects.Entities.Codecs;

internal sealed class LineCodec : IEntityCodec
{
    public bool CanDecode(short typeCode, DwgVersion version) => typeCode == DwgObjectReader.TypeLine;
    public IEntity Create(ulong handle, short typeCode, DwgVersion version) => EntityFactory.CreateLine(handle);
    public bool Decode(IEntity entity, BitReader reader, DwgObjectRecord? record, in EntityWindow window)
    {
        ((LineEntity)entity).ReadGeometry(reader);
        return true;
    }
}

internal sealed class CircleCodec : IEntityCodec
{
    public bool CanDecode(short typeCode, DwgVersion version) => typeCode == DwgObjectReader.TypeCircle;
    public IEntity Create(ulong handle, short typeCode, DwgVersion version) => EntityFactory.CreateCircle(handle);
    public bool Decode(IEntity entity, BitReader reader, DwgObjectRecord? record, in EntityWindow window)
    {
        ((CircleEntity)entity).ReadGeometry(reader);
        return true;
    }
}

internal sealed class ArcCodec : IEntityCodec
{
    public bool CanDecode(short typeCode, DwgVersion version) => typeCode == DwgObjectReader.TypeArc;
    public IEntity Create(ulong handle, short typeCode, DwgVersion version) => EntityFactory.CreateArc(handle);
    public bool Decode(IEntity entity, BitReader reader, DwgObjectRecord? record, in EntityWindow window)
    {
        ((ArcEntity)entity).ReadGeometry(reader);
        return true;
    }
}

internal sealed class EllipseCodec : IEntityCodec
{
    public bool CanDecode(short typeCode, DwgVersion version) => typeCode == DwgObjectReader.TypeEllipse;
    public IEntity Create(ulong handle, short typeCode, DwgVersion version) => EntityFactory.CreateEllipse(handle);
    public bool Decode(IEntity entity, BitReader reader, DwgObjectRecord? record, in EntityWindow window)
    {
        ((EllipseEntity)entity).ReadGeometry(reader);
        return true;
    }
}

internal sealed class PointCodec : IEntityCodec
{
    public bool CanDecode(short typeCode, DwgVersion version) => typeCode == DwgObjectReader.TypePoint;
    public IEntity Create(ulong handle, short typeCode, DwgVersion version) => EntityFactory.CreatePoint(handle);
    public bool Decode(IEntity entity, BitReader reader, DwgObjectRecord? record, in EntityWindow window)
    {
        ((PointEntity)entity).ReadGeometry(reader);
        return true;
    }
}

internal sealed class RayCodec : IEntityCodec
{
    public bool CanDecode(short typeCode, DwgVersion version) => typeCode == DwgObjectReader.TypeRay;
    public IEntity Create(ulong handle, short typeCode, DwgVersion version) => EntityFactory.CreateRay(handle);
    public bool Decode(IEntity entity, BitReader reader, DwgObjectRecord? record, in EntityWindow window)
    {
        ((RayEntity)entity).ReadGeometry(reader);
        return true;
    }
}

internal sealed class XLineCodec : IEntityCodec
{
    public bool CanDecode(short typeCode, DwgVersion version) => typeCode == DwgObjectReader.TypeXLine;
    public IEntity Create(ulong handle, short typeCode, DwgVersion version) => EntityFactory.CreateXLine(handle);
    public bool Decode(IEntity entity, BitReader reader, DwgObjectRecord? record, in EntityWindow window)
    {
        ((XLineEntity)entity).ReadGeometry(reader);
        return true;
    }
}

internal sealed class SplineCodec : IEntityCodec
{
    public bool CanDecode(short typeCode, DwgVersion version) => typeCode == DwgObjectReader.TypeSpline;
    public IEntity Create(ulong handle, short typeCode, DwgVersion version) => EntityFactory.CreateSpline(handle, version);
    public bool Decode(IEntity entity, BitReader reader, DwgObjectRecord? record, in EntityWindow window)
    {
        ((SplineEntity)entity).ReadGeometry(reader);
        return true;
    }
}

internal sealed class LwPolylineCodec : IEntityCodec
{
    public bool CanDecode(short typeCode, DwgVersion version) => typeCode == DwgObjectReader.TypeLwPolyline;
    public IEntity Create(ulong handle, short typeCode, DwgVersion version) => EntityFactory.CreateLwPolyline(handle);
    public bool Decode(IEntity entity, BitReader reader, DwgObjectRecord? record, in EntityWindow window)
    {
        ((LwPolylineEntity)entity).ReadBody(reader);
        return true;
    }
}

internal sealed class Polyline2DCodec : IEntityCodec
{
    public bool CanDecode(short typeCode, DwgVersion version) => typeCode == DwgObjectReader.TypePolyline2D;
    public IEntity Create(ulong handle, short typeCode, DwgVersion version) => EntityFactory.CreatePolyline2D(handle);
    public bool Decode(IEntity entity, BitReader reader, DwgObjectRecord? record, in EntityWindow window)
    {
        ((Polyline2DEntity)entity).ReadGeometry(reader);
        return true;
    }
}

internal sealed class Vertex2DCodec : IEntityCodec
{
    public bool CanDecode(short typeCode, DwgVersion version) => typeCode == DwgObjectReader.TypeVertex2D;
    public IEntity Create(ulong handle, short typeCode, DwgVersion version) => EntityFactory.CreateVertex2D(handle);
    public bool Decode(IEntity entity, BitReader reader, DwgObjectRecord? record, in EntityWindow window)
    {
        ((Vertex2DEntity)entity).ReadGeometry(reader);
        return true;
    }
}
