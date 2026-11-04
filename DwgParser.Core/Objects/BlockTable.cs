using System.Collections;

namespace DwgParser.Core.Objects;

public sealed class BlockTable : IReadOnlyList<DwgObjectReader.BlockRegion>
{
    private readonly List<DwgObjectReader.BlockRegion> _blocks;
    private readonly Dictionary<ulong, DwgObjectReader.BlockRegion> _byHandle;
    private readonly Dictionary<string, List<DwgObjectReader.BlockRegion>> _byName;
    private readonly EntityCollection _entities;

    internal BlockTable(
        IEnumerable<DwgObjectReader.BlockRegion> regions,
        EntityCollection entities,
        ulong modelSpaceHandle,
        ulong paperSpaceHandle)
    {
        _blocks = regions.OrderBy(static r => r.HeaderHandle).ToList();
        _byHandle = new Dictionary<ulong, DwgObjectReader.BlockRegion>(_blocks.Count);
        _byName = new Dictionary<string, List<DwgObjectReader.BlockRegion>>(StringComparer.Ordinal);
        foreach (var r in _blocks)
        {
            if (r.HeaderHandle != 0) _byHandle[r.HeaderHandle] = r;
            if (string.IsNullOrEmpty(r.Name)) continue;
            if (!_byName.TryGetValue(r.Name, out var list)) _byName[r.Name] = list = new List<DwgObjectReader.BlockRegion>();
            list.Add(r);
        }

        _entities = entities;
        ModelSpaceHandle = modelSpaceHandle;
        PaperSpaceHandle = paperSpaceHandle;
    }

    public int Count => _blocks.Count;

    public DwgObjectReader.BlockRegion this[int index] => _blocks[index];

    public ulong ModelSpaceHandle { get; }

    public ulong PaperSpaceHandle { get; }

    public DwgObjectReader.BlockRegion? ModelSpace => Find(ModelSpaceHandle);

    public DwgObjectReader.BlockRegion? PaperSpace => Find(PaperSpaceHandle);

    public DwgObjectReader.BlockRegion? Find(ulong headerHandle)
        => _byHandle.TryGetValue(headerHandle, out var r) ? r : null;

    public DwgObjectReader.BlockRegion? Find(string name)
    {
        if (name is null) return null;
        return _byName.TryGetValue(name, out var list) ? list[0] : null;
    }

    public IReadOnlyList<DwgObjectReader.BlockRegion> FindAll(string name)
        => name is not null && _byName.TryGetValue(name, out var list) ? list : Array.Empty<DwgObjectReader.BlockRegion>();

    public bool Contains(ulong headerHandle) => _byHandle.ContainsKey(headerHandle);

    public bool Contains(string name) => name is not null && _byName.ContainsKey(name);

    public IReadOnlyList<IEntity> EntitiesOf(ulong blockHeaderHandle)
    {
        var src = _entities.OfOwner(blockHeaderHandle);
        return src as IReadOnlyList<IEntity> ?? src.ToArray();
    }

    public IReadOnlyList<IEntity> ModelSpaceEntities => EntitiesOf(ModelSpaceHandle);

    public IReadOnlyList<IEntity> PaperSpaceEntities => EntitiesOf(PaperSpaceHandle);

    public IEnumerable<ulong> Handles => _blocks.Select(static r => r.HeaderHandle);

    public IEnumerable<string> Names => _blocks.Select(static r => r.Name).Where(static n => !string.IsNullOrEmpty(n));

    public IEnumerator<DwgObjectReader.BlockRegion> GetEnumerator() => _blocks.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
