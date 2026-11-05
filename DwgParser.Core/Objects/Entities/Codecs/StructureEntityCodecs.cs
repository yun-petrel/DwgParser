using DwgParser.Core.IO;

namespace DwgParser.Core.Objects.Entities.Codecs;

internal sealed class InsertCodec : IEntityCodec
{
    public bool CanDecode(short typeCode, DwgVersion version)
        => typeCode is DwgObjectReader.TypeInsert or DwgObjectReader.TypeMInsert;

    public IEntity Create(ulong handle, short typeCode, DwgVersion version)
        => EntityFactory.CreateInsert(handle, version.IsR2000());

    public bool Decode(IEntity entity, BitReader reader, DwgObjectRecord? record, in EntityWindow window)
        => ((InsertEntity)entity).ReadGeometry(reader, record);
}

internal sealed class BlockCodec : IEntityCodec
{
    public bool CanDecode(short typeCode, DwgVersion version) => typeCode == DwgObjectReader.TypeBlockEntity;
    public IEntity Create(ulong handle, short typeCode, DwgVersion version) => EntityFactory.CreateBlock(handle);
    public bool Decode(IEntity entity, BitReader reader, DwgObjectRecord? record, in EntityWindow window)
    {
        ((BlockEntity)entity).ReadBody(reader, record, window.Version, window.Handle, window.BodyStart);
        return true;
    }
}

internal sealed class EndBlkCodec : IEntityCodec
{
    public bool CanDecode(short typeCode, DwgVersion version) => typeCode == DwgObjectReader.TypeEndBlk;
    public IEntity Create(ulong handle, short typeCode, DwgVersion version) => EntityFactory.CreateEndBlk(handle);
    public bool Decode(IEntity entity, BitReader reader, DwgObjectRecord? record, in EntityWindow window)
    {
        ((EndBlkEntity)entity).ReadBody(reader);
        return true;
    }
}

internal sealed class SeqEndCodec : IEntityCodec
{
    public bool CanDecode(short typeCode, DwgVersion version) => typeCode == DwgObjectReader.TypeSeqEnd;
    public IEntity Create(ulong handle, short typeCode, DwgVersion version) => EntityFactory.CreateSeqEnd(handle);
    public bool Decode(IEntity entity, BitReader reader, DwgObjectRecord? record, in EntityWindow window)
    {
        ((SeqEndEntity)entity).ReadBody(reader);
        return true;
    }
}

internal sealed class SolidCodec : IEntityCodec
{
    public bool CanDecode(short typeCode, DwgVersion version) => typeCode == DwgObjectReader.TypeSolid;
    public IEntity Create(ulong handle, short typeCode, DwgVersion version) => EntityFactory.CreateSolid(handle);
    public bool Decode(IEntity entity, BitReader reader, DwgObjectRecord? record, in EntityWindow window)
    {
        ((SolidEntity)entity).ReadBody(reader);
        return true;
    }
}

internal sealed class TraceCodec : IEntityCodec
{
    public bool CanDecode(short typeCode, DwgVersion version) => typeCode == DwgObjectReader.TypeTrace;
    public IEntity Create(ulong handle, short typeCode, DwgVersion version) => EntityFactory.CreateTrace(handle);
    public bool Decode(IEntity entity, BitReader reader, DwgObjectRecord? record, in EntityWindow window)
    {
        ((TraceEntity)entity).ReadBody(reader);
        return true;
    }
}

internal sealed class FaceCodec : IEntityCodec
{
    public bool CanDecode(short typeCode, DwgVersion version) => typeCode == DwgObjectReader.Type3dFace;
    public IEntity Create(ulong handle, short typeCode, DwgVersion version) => EntityFactory.CreateFace(handle);
    public bool Decode(IEntity entity, BitReader reader, DwgObjectRecord? record, in EntityWindow window)
    {
        ((FaceEntity)entity).ReadBody(reader);
        return true;
    }
}

internal sealed class RegionCodec : IEntityCodec
{
    public bool CanDecode(short typeCode, DwgVersion version) => typeCode == DwgObjectReader.TypeRegion;
    public IEntity Create(ulong handle, short typeCode, DwgVersion version) => EntityFactory.CreateRegion(handle);
    public bool Decode(IEntity entity, BitReader reader, DwgObjectRecord? record, in EntityWindow window)
    {
        ((RegionEntity)entity).ReadBody(reader, record);
        return true;
    }
}

internal sealed class Solid3dCodec : IEntityCodec
{
    public bool CanDecode(short typeCode, DwgVersion version) => typeCode == DwgObjectReader.TypeSolid3d;
    public IEntity Create(ulong handle, short typeCode, DwgVersion version) => EntityFactory.CreateSolid3d(handle);
    public bool Decode(IEntity entity, BitReader reader, DwgObjectRecord? record, in EntityWindow window)
    {
        ((Solid3dEntity)entity).ReadBody(reader, record);
        return true;
    }
}

internal sealed class BodyCodec : IEntityCodec
{
    public bool CanDecode(short typeCode, DwgVersion version) => typeCode == DwgObjectReader.TypeBody;
    public IEntity Create(ulong handle, short typeCode, DwgVersion version) => EntityFactory.CreateBody(handle);
    public bool Decode(IEntity entity, BitReader reader, DwgObjectRecord? record, in EntityWindow window)
    {
        ((BodyEntity)entity).ReadBody(reader, record);
        return true;
    }
}

internal sealed class MLineCodec : IEntityCodec
{
    public bool CanDecode(short typeCode, DwgVersion version) => typeCode == DwgObjectReader.TypeMline;
    public IEntity Create(ulong handle, short typeCode, DwgVersion version) => EntityFactory.CreateMLine(handle);
    public bool Decode(IEntity entity, BitReader reader, DwgObjectRecord? record, in EntityWindow window)
    {
        ((MLineEntity)entity).ReadBody(reader, record, window.Version, window.Handle, window.BodyStart);
        return true;
    }
}

internal sealed class ViewportCodec : IEntityCodec
{
    public bool CanDecode(short typeCode, DwgVersion version) => typeCode == DwgObjectReader.TypeViewport;
    public IEntity Create(ulong handle, short typeCode, DwgVersion version)
        => EntityFactory.CreateViewport(handle, version);
    public bool Decode(IEntity entity, BitReader reader, DwgObjectRecord? record, in EntityWindow window)
    {
        ((ViewportEntity)entity).ReadBody(reader);
        return true;
    }
}

internal sealed class HatchCodec : IEntityCodec
{
    public bool CanDecode(short typeCode, DwgVersion version) => typeCode == DwgObjectReader.TypeHatch;
    public IEntity Create(ulong handle, short typeCode, DwgVersion version)
        => EntityFactory.CreateHatch(handle, version);
    public bool Decode(IEntity entity, BitReader reader, DwgObjectRecord? record, in EntityWindow window)
    {
        ((HatchEntity)entity).ReadBody(reader, record);
        return true;
    }
}

internal sealed class TableCodec : IEntityCodec
{
    public bool CanDecode(short typeCode, DwgVersion version) => typeCode == DwgObjectReader.TypeTable;
    public IEntity Create(ulong handle, short typeCode, DwgVersion version)
        => EntityFactory.CreateTable(handle, version.IsR2010Plus());
    public bool Decode(IEntity entity, BitReader reader, DwgObjectRecord? record, in EntityWindow window)
    {
        ((TableEntity)entity).ReadBody(reader, window.Version, record);
        return true;
    }
}
