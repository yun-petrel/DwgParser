namespace DwgParser.Core.Objects;

public sealed class EntityCollection : IEntityCollection
{
    private readonly List<IEntity> _items;
    private readonly IReadOnlyList<IEntity>? _attached;
    private int _attachedCount;
    private readonly Dictionary<ulong, IEntity> _byHandle = new();
    private Dictionary<ulong, List<IEntity>>? _byOwner;

    public EntityCollection() { _items = new(); }

    public EntityCollection(IEnumerable<IEntity> seed)
    {
        _items = new();
        foreach (var e in seed)
            Add(e);
    }

    private EntityCollection(IReadOnlyList<IEntity> attached)
    {
        _attached = attached;
        _items = null!;
    }

    internal static EntityCollection Attach(IReadOnlyList<IEntity> liveBacking) => new(liveBacking);

    public int Count => _attached?.Count ?? _items.Count;

    public IReadOnlyList<IEntity> Items => _attached ?? _items;

    public IEntity? Find(ulong handle)
    {
        SyncAttached();
        return _byHandle.TryGetValue(handle, out var e) ? e : null;
    }

    public IEnumerable<IEntity> OfOwner(ulong ownerHandle)
    {
        SyncAttached();
        if (_byOwner is null)
            BuildOwnerGroups();
        return _byOwner!.TryGetValue(ownerHandle, out var list)
            ? list
            : (IEnumerable<IEntity>)Array.Empty<IEntity>();
    }

    private void SyncAttached()
    {
        if (_attached is null || _attached.Count == _attachedCount) return;
        _byHandle.Clear();
        _byOwner = null;
        for (int i = 0; i < _attached.Count; i++)
        {
            var e = _attached[i];
            if (e.Handle != 0) _byHandle.TryAdd(e.Handle, e);
        }
        _attachedCount = _attached.Count;
    }

    public void Add(IEntity entity)
    {
        if (_attached is not null)
            throw new InvalidOperationException("附着模式的集合只读，变异须走源列表");
        if (entity is null) return;
        _items.Add(entity);
        if (entity.Handle != 0) _byHandle.TryAdd(entity.Handle, entity);
        _byOwner = null;
    }

    public bool Remove(ulong handle)
    {
        if (_attached is not null)
            throw new InvalidOperationException("附着模式的集合只读，变异须走源列表");
        if (!_byHandle.TryGetValue(handle, out var entity)) return false;
        _byHandle.Remove(handle);
        _items.Remove(entity);
        _byOwner = null;
        return true;
    }

    public void Invalidate()
    {
        _attachedCount = 0;
        _byHandle.Clear();
        _byOwner = null;
    }

    private void BuildOwnerGroups()
    {
        var groups = new Dictionary<ulong, List<IEntity>>();
        foreach (var e in Items)
        {
            if (e is null || e.OwnerHandle == 0) continue;
            if (!groups.TryGetValue(e.OwnerHandle, out var list))
                groups[e.OwnerHandle] = list = new List<IEntity>();
            list.Add(e);
        }
        _byOwner = groups;
    }
}
