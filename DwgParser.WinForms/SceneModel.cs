using DwgParser.Core.Format;
using DwgParser.Core.Objects;
using DwgParser.Core.Objects.Records;
using DwgParser.Core.Render;
using DwgParser.Rendering;

namespace DwgParser.WinForms;

internal readonly record struct Bounds2(double MinX, double MinY, double MaxX, double MaxY)
{
    public double Width => MaxX - MinX;
    public double Height => MaxY - MinY;
    public bool IsFinite => double.IsFinite(MinX) && double.IsFinite(MinY)
                            && double.IsFinite(MaxX) && double.IsFinite(MaxY);
    public bool Contains(double x, double y, double pad = 0)
        => x >= MinX - pad && x <= MaxX + pad && y >= MinY - pad && y <= MaxY + pad;

    public double DistanceTo(double x, double y)
    {
        double dx = x < MinX ? MinX - x : x > MaxX ? x - MaxX : 0;
        double dy = y < MinY ? MinY - y : y > MaxY ? y - MaxY : 0;
        return Math.Sqrt(dx * dx + dy * dy);
    }
}

internal sealed class DwgScene
{
    private readonly List<DwgEntity> _all;
    private readonly List<DwgEntity> _visible = new();
    private readonly HashSet<ulong> _hiddenLayers = new();
    private readonly Dictionary<DwgEntity, Bounds2> _bounds = new();
    private readonly Dictionary<ulong, string> _layerNames = new();
    private RenderContext? _ctx;
    private IReadOnlyList<ShxFont>? _bypass;

    private DwgScene(DwgDocument document, DwgObjectReader.WalkResult walk,
                     List<DwgEntity> all, Bounds2 bounds)
    {
        Document = document;
        Walk = walk;
        _all = all;
        Bounds = bounds;
        RefreshVisible();
    }

    public DwgDocument Document { get; }
    public DwgObjectReader.WalkResult Walk { get; }
    public string FilePath => Document.FilePath;

    public string DisplayName => Path.GetFileName(FilePath);

    public IReadOnlyList<DwgEntity> AllEntities => _all;

    public IReadOnlyList<DwgEntity> VisibleEntities => _visible;

    public Bounds2 Bounds { get; }

    public int IndexOfEntity(DwgEntity entity) => _all.IndexOf(entity);

    public static DwgScene Load(string path)
    {
        var doc = DwgDocument.Load(path);
        var walk = doc.GetObjects();
        var all = FrameRenderer.CollectAllEntities(walk);

        var frame = FrameRenderer.DetectFrame(walk, all);
        double minX, minY, maxX, maxY;
        if (frame is { } f)
        {
            (minX, minY, maxX, maxY) = f;
        }
        else
        {
            FrameRenderer.ComputeBounds(all, out minX, out minY, out maxX, out maxY);
            if (FrameRenderer.IsBlankBounds(minX, minY, maxX, maxY))
                (minX, minY, maxX, maxY) = (0, 0, 100, 100);
        }
        return new DwgScene(doc, walk, all, new Bounds2(minX, minY, maxX, maxY));
    }


    public LayerTable Layers => Walk.Layers;

    public string LayerNameOf(ulong handle)
    {
        if (_layerNames.TryGetValue(handle, out var cached)) return cached;
        string name = Walk.Layers.Find(handle)?.Name is { Length: > 0 } n ? n : $"图层:{handle:X}";
        _layerNames[handle] = name;
        return name;
    }

    public bool IsLayerHidden(ulong handle) => _hiddenLayers.Contains(handle);

    public bool SetLayerHidden(ulong handle, bool hidden)
    {
        bool changed = hidden ? _hiddenLayers.Add(handle) : _hiddenLayers.Remove(handle);
        if (changed) RefreshVisible();
        return changed;
    }

    public void ShowAllLayers()
    {
        if (_hiddenLayers.Count == 0) return;
        _hiddenLayers.Clear();
        RefreshVisible();
    }

    public void ShowOnlyLayer(ulong? handle)
    {
        bool changed = false;
        var seen = new HashSet<ulong>();

        void Apply(ulong h)
        {
            if (!seen.Add(h)) return;
            bool hide = handle is { } only && h != only;
            changed |= hide ? _hiddenLayers.Add(h) : _hiddenLayers.Remove(h);
        }

        foreach (var e in _all) Apply(e.LayerHandle);
        foreach (var layer in Walk.Layers) Apply(layer.Handle);

        if (changed) RefreshVisible();
    }

    private void RefreshVisible()
    {
        _visible.Clear();
        foreach (var e in _all)
            if (!_hiddenLayers.Contains(e.LayerHandle))
                _visible.Add(e);
    }


    public Bounds2 BoundsOf(DwgEntity entity)
    {
        if (_bounds.TryGetValue(entity, out var b)) return b;
        b = MeasureBounds(entity);
        _bounds[entity] = b;
        return b;
    }

    private Bounds2 MeasureBounds(DwgEntity entity)
    {
        if (TextExtent.IsTextFamily(entity.Kind)
            && TextExtent.BoxOf(entity, Ctx, TextBackends.Current, BypassFonts) is { } tb
            && double.IsFinite(tb.MinX) && double.IsFinite(tb.MinY)
            && double.IsFinite(tb.MaxX) && double.IsFinite(tb.MaxY))
        {
            return new Bounds2(tb.MinX, tb.MinY, tb.MaxX, tb.MaxY);
        }
        var single = new List<DwgEntity>(1) { entity };
        FrameRenderer.ComputeBounds(single, out double minX, out double minY, out double maxX, out double maxY);
        return new Bounds2(minX, minY, maxX, maxY);
    }

    private RenderContext Ctx => _ctx ??= new RenderContext(Walk);

    private IReadOnlyList<ShxFont>? BypassFonts => _bypass ??= ShxFontResolver.BypassFonts(Walk.SourceData);

    public void InvalidateBounds(DwgEntity entity) => _bounds.Remove(entity);

    public void InvalidateAllBounds() => _bounds.Clear();

    public string Describe(DwgEntity e)
        => $"0x{e.Handle:X6}  {e.Kind}  色{e.ColorIndex}  {LayerNameOf(e.LayerHandle)}";
}
