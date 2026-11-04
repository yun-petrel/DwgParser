namespace DwgParser.Core.Objects;

public interface IEntityCollection
{
    int Count { get; }

    IReadOnlyList<IEntity> Items { get; }

    IEntity? Find(ulong handle);

    IEnumerable<IEntity> OfOwner(ulong ownerHandle);

    void Add(IEntity entity);

    bool Remove(ulong handle);

    void Invalidate();
}
