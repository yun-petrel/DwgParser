using DwgParser.Core.IO;

namespace DwgParser.Core.Objects;

public interface IEntityCodec
{
    bool CanDecode(DwgEntityType type, DwgVersion version);

    IEntity Create(ulong handle);

    void Decode(IEntity entity, BitReader reader, DwgObjectRecord record, in EntityWindow window);
}

public readonly record struct EntityWindow(int BodyStart, int BodyEnd, uint Bitsize);

public sealed class EntityCodecRegistry
{
    public static EntityCodecRegistry Default { get; } = new();

    private readonly List<IEntityCodec> _codecs = new();

    public void Register(IEntityCodec codec) => _codecs.Add(codec);

    public IEntityCodec? Resolve(DwgEntityType type, DwgVersion version)
    {
        for (int i = 0; i < _codecs.Count; i++)
            if (_codecs[i].CanDecode(type, version))
                return _codecs[i];
        return null;
    }
}
