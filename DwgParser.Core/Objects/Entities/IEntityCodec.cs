using DwgParser.Core.IO;

namespace DwgParser.Core.Objects;

public interface IEntityCodec
{
    bool CanDecode(short typeCode, DwgVersion version);

    IEntity Create(ulong handle, short typeCode, DwgVersion version);

    bool Decode(IEntity entity, BitReader reader, DwgObjectRecord? record, in EntityWindow window);
}

public readonly record struct EntityWindow(
    int BodyStart, int BodyEnd, uint Bitsize, DwgVersion Version, ulong Handle);

public sealed class EntityCodecRegistry
{
    public static EntityCodecRegistry Default { get; } = new();

    private readonly List<IEntityCodec> _codecs = new();

    static EntityCodecRegistry() => Entities.Codecs.EntityCodecBootstrap.RegisterAll(Default);

    public void Register(IEntityCodec codec) => _codecs.Add(codec);

    public IEntityCodec? Resolve(short typeCode, DwgVersion version)
    {
        for (int i = 0; i < _codecs.Count; i++)
            if (_codecs[i].CanDecode(typeCode, version))
                return _codecs[i];
        return null;
    }
}
