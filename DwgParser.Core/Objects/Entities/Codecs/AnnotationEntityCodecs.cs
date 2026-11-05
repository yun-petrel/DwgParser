using DwgParser.Core.IO;

namespace DwgParser.Core.Objects.Entities.Codecs;

internal sealed class DimensionCodec : IEntityCodec
{
    public bool CanDecode(short typeCode, DwgVersion version)
        => typeCode is >= DwgObjectReader.TypeDimOrdinate and <= DwgObjectReader.TypeDimDiameter;

    public IEntity Create(ulong handle, short typeCode, DwgVersion version)
        => EntityFactory.CreateDimension(handle, typeCode);

    public bool Decode(IEntity entity, BitReader reader, DwgObjectRecord? record, in EntityWindow window)
    {
        List<DwgObjectReader.DimFieldEntry>? tl = DwgObjectReader.BeginDimensionTrace(record);
        ((DimensionEntity)entity).ReadGeometry(reader, record, tl, window.Version);
        return true;
    }
}

internal sealed class LeaderCodec : IEntityCodec
{
    public bool CanDecode(short typeCode, DwgVersion version) => typeCode == DwgObjectReader.TypeLeader;
    public IEntity Create(ulong handle, short typeCode, DwgVersion version) => EntityFactory.CreateLeader(handle);
    public bool Decode(IEntity entity, BitReader reader, DwgObjectRecord? record, in EntityWindow window)
    {
        ((LeaderEntity)entity).ReadBody(reader, record);
        return true;
    }
}

internal sealed class ToleranceCodec : IEntityCodec
{
    public bool CanDecode(short typeCode, DwgVersion version) => typeCode == DwgObjectReader.TypeTolerance;
    public IEntity Create(ulong handle, short typeCode, DwgVersion version)
        => EntityFactory.CreateTolerance(handle, version.IsR2007Plus());
    public bool Decode(IEntity entity, BitReader reader, DwgObjectRecord? record, in EntityWindow window)
    {
        ((ToleranceEntity)entity).ReadBody(reader, record, window.Version, window.Handle, window.BodyStart);
        return true;
    }
}

internal sealed class MLeaderCodec : IEntityCodec
{
    public bool CanDecode(short typeCode, DwgVersion version) => typeCode == DwgObjectReader.TypeMLeader;
    public IEntity Create(ulong handle, short typeCode, DwgVersion version)
        => EntityFactory.CreateMLeader(handle, version);
    public bool Decode(IEntity entity, BitReader reader, DwgObjectRecord? record, in EntityWindow window)
    {
        ((MLeaderEntity)entity).ReadBody(reader, record);
        return true;
    }
}
