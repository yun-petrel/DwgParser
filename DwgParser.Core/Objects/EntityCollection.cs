namespace DwgParser.Core.Objects;

public sealed class EntityCollection : IEntityCollection
{
    private readonly List<IEntity> _items = new();
    private readonly Dictionary<ulong, IEntity> _byHandle = new();
    private Dictionary<ulong, List<IEntity>>? _byOwner;

    public EntityCollection() { }

    public EntityCollection(IEnumerable<IEntity> seed)
    {
        foreach (var e in seed)
            Add(e);
    }

    public int Count => _items.Count;

    public IReadOnlyList<IEntity> Items => _items;

    public IEntity? Find(ulong handle)
        => _byHandle.TryGetValue(handle, out var e) ? e : null;

    public IEnumerable<IEntity> OfOwner(ulong ownerHandle)
    {
        if (_byOwner is null)
            BuildOwnerGroups();
        return _byOwner!.TryGetValue(ownerHandle, out var list)
            ? list
            : (IEnumerable<IEntity>)Array.Empty<IEntity>();
    }

    public void Add(IEntity entity)
    {
        if (entity is null) return;
        _items.Add(entity);
        if (entity.Handle != 0) _byHandle.TryAdd(entity.Handle, entity);
        _byOwner = null;
    }

    public bool Remove(ulong handle)
    {
        if (!_byHandle.TryGetValue(handle, out var entity)) return false;
        _byHandle.Remove(handle);
        _items.Remove(entity);
        _byOwner = null;
        return true;
    }

    public void Invalidate()
    {
        _byHandle.Clear();
        _byOwner = null;
    }

    private void BuildOwnerGroups()
    {
        var groups = new Dictionary<ulong, List<IEntity>>();
        foreach (var e in _items)
        {
            if (e is null || e.OwnerHandle == 0) continue;
            if (!groups.TryGetValue(e.OwnerHandle, out var list))
                groups[e.OwnerHandle] = list = new List<IEntity>();
            list.Add(e);
        }
        _byOwner = groups;
    }
}
