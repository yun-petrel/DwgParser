using DwgParser.Core.IO;

namespace DwgParser.Core.Objects.Entities.Codecs;

internal sealed class TextCodec : IEntityCodec
{
    public bool CanDecode(short typeCode, DwgVersion version) => typeCode == DwgObjectReader.TypeText;
    public IEntity Create(ulong handle, short typeCode, DwgVersion version) => EntityFactory.CreateText(handle);
    public bool Decode(IEntity entity, BitReader reader, DwgObjectRecord? record, in EntityWindow window)
    {
        ((TextEntity)entity).ReadText(reader, record);
        return true;
    }
}

internal sealed class AttributeCodec : IEntityCodec
{
    public bool CanDecode(short typeCode, DwgVersion version) => typeCode == DwgObjectReader.TypeAttrib;
    public IEntity Create(ulong handle, short typeCode, DwgVersion version)
        => EntityFactory.CreateAttribute(handle,
            r2007Plus: version.IsR2007Plus(),
            r2010Plus: version.IsR2010Plus(),
            r2018Plus: version >= DwgVersion.AC1032);
    public bool Decode(IEntity entity, BitReader reader, DwgObjectRecord? record, in EntityWindow window)
    {
        ((AttributeEntity)entity).ReadBody(reader, record);
        return true;
    }
}

internal sealed class AttDefCodec : IEntityCodec
{
    public bool CanDecode(short typeCode, DwgVersion version) => typeCode == DwgObjectReader.TypeAttDef;
    public IEntity Create(ulong handle, short typeCode, DwgVersion version)
        => EntityFactory.CreateAttDef(handle,
            r2007Plus: version.IsR2007Plus(),
            r2010Plus: version.IsR2010Plus(),
            r2018Plus: version >= DwgVersion.AC1032);
    public bool Decode(IEntity entity, BitReader reader, DwgObjectRecord? record, in EntityWindow window)
    {
        ((AttDefEntity)entity).ReadBody(reader, record);
        return true;
    }
}

internal sealed class MTextCodec : IEntityCodec
{
    public bool CanDecode(short typeCode, DwgVersion version) => typeCode == DwgObjectReader.TypeMText;
    public IEntity Create(ulong handle, short typeCode, DwgVersion version) => EntityFactory.CreateMText(handle, version);
    public bool Decode(IEntity entity, BitReader reader, DwgObjectRecord? record, in EntityWindow window)
    {
        ((MTextEntity)entity).ReadBody(reader, record);
        return true;
    }
}
