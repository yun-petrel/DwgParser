using System.Collections;

namespace DwgParser.Core.Objects.Records;

public sealed class LayerTable : IReadOnlyList<ILayer>
{
    private readonly List<ILayer> _layers;
    private readonly Dictionary<ulong, ILayer> _byHandle;
    private readonly Dictionary<string, ILayer> _byName;

    internal LayerTable(IEnumerable<ILayer> layers)
    {
        _layers = new List<ILayer>(layers);
        _byHandle = new Dictionary<ulong, ILayer>(_layers.Count);
        _byName = new Dictionary<string, ILayer>(StringComparer.Ordinal);
        foreach (var l in _layers)
        {
            _byHandle[l.Handle] = l;
            if (!_byName.ContainsKey(l.Name)) _byName[l.Name] = l;
        }
    }

    public const short LayerTypeCode = LayerRecord.TypeCodeValue;

    public int Count => _layers.Count;

    public ILayer this[int index] => _layers[index];

    public ILayer? Find(ulong handle)
        => _byHandle.TryGetValue(handle, out var l) ? l : null;

    public ILayer? Find(string name)
        => name != null && _byName.TryGetValue(name, out var l) ? l : null;

    public bool Contains(ulong handle) => _byHandle.ContainsKey(handle);

    public bool Contains(string name) => name != null && _byName.ContainsKey(name);

    public short? ColorOf(ulong handle) => Find(handle)?.LayerColor;

    public bool IsFrozen(ulong handle) => Find(handle)?.Frozen ?? false;

    public bool IsOff(ulong handle) => Find(handle)?.Off ?? false;

    public bool IsLocked(ulong handle) => Find(handle)?.Locked ?? false;

    public bool IsVisible(ulong handle)
    {
        var l = Find(handle);
        return l is null || (!l.Off && !l.Frozen);
    }

    public IEnumerable<string> Names => _layers.Select(l => l.Name);

    public IEnumerable<ulong> Handles => _layers.Select(l => l.Handle);

    public IEnumerator<ILayer> GetEnumerator() => _layers.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
