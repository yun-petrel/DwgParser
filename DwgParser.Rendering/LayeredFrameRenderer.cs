using DwgParser.Core.Objects;

using DwgParser.Core.Render;
namespace DwgParser.Rendering;

public sealed class LayeredFrameRenderer
{
    private readonly Dictionary<ulong, LayerRender> _layers = new();
    private readonly List<ulong> _order = new();
    private readonly FrameRenderer _frame = new();

    private (int W, int H, int Thick, double MinX, double MinY, double MaxX, double MaxY, bool HasFrame)? _viewKey;

    public sealed class LayerRender
    {
        public ulong Handle { get; internal set; }
        public string Name { get; internal set; } = "";
        public PixelBuffer Buffer { get; internal set; } = null!;
        public string Fingerprint { get; internal set; } = "";
        public int EntityCount { get; internal set; }
        public bool CacheHit { get; internal set; }
    }

    public IReadOnlyDictionary<ulong, LayerRender> Layers => _layers;

    public IReadOnlyList<ulong> LayerOrder => _order;

    public (int Rendered, int Cached) LastRenderStats { get; private set; }

    public PixelBuffer Render(DwgObjectReader.WalkResult walk,
        int width = 2048, int height = 2048, int lineThickness = 2)
    {
        var all = FrameRenderer.CollectAllEntities(walk);

        var frame = FrameRenderer.DetectFrame(walk, all);
        double minX, minY, maxX, maxY;
        if (frame.HasValue)
        {
            (minX, minY, maxX, maxY) = frame.Value;
        }
        else
        {
            FrameRenderer.ComputeBounds(all, out minX, out minY, out maxX, out maxY);
        }
        if (FrameRenderer.IsBlankBounds(minX, minY, maxX, maxY))
            return new PixelBuffer(width, height);

        var viewKey = (width, height, lineThickness, minX, minY, maxX, maxY, frame.HasValue);
        bool viewChanged = _viewKey != viewKey;

        var byLayer = new Dictionary<ulong, List<DwgEntity>>();
        _order.Clear();
        foreach (var e in all)
        {
            if (!byLayer.TryGetValue(e.LayerHandle, out var list))
            {
                byLayer[e.LayerHandle] = list = new();
                _order.Add(e.LayerHandle);
            }
            list.Add(e);
        }

        var view = new DrawViewport(minX, minY, maxX, maxY, width, height, FrameRenderer.ViewportMarginPx(width, height), false);
        int thick = Math.Max(1, lineThickness);
        _frame.Prepare(walk);

        int rendered = 0, cached = 0;
        foreach (var handle in _order)
        {
            var entities = byLayer[handle];
            string fp = Fingerprint(entities);

            bool hit = false;
            LayerRender? entry = null;
            if (!viewChanged && _layers.TryGetValue(handle, out var existing) &&
                existing.Fingerprint == fp &&
                existing.Buffer.Width == width && existing.Buffer.Height == height)
            {
                hit = true;
                entry = existing;
            }
            if (hit)
            {
                entry!.CacheHit = true;
                entry.EntityCount = entities.Count;
                cached++;
                continue;
            }

            var buf = new PixelBuffer(width, height);
            buf.EnableCoverage();
            _frame.RenderEntities(entities, buf, thick, view);

            if (!_layers.TryGetValue(handle, out entry!))
                _layers[handle] = entry = new LayerRender { Handle = handle };
            entry.Name = walk.Layers.Find(handle)?.Name ?? $"图层:{handle:X}";
            entry.Buffer = buf;
            entry.Fingerprint = fp;
            entry.EntityCount = entities.Count;
            entry.CacheHit = false;
            rendered++;
        }

        {
            var live = new HashSet<ulong>(_order);
            var gone = new List<ulong>();
            foreach (var h in _layers.Keys)
                if (!live.Contains(h)) gone.Add(h);
            foreach (var h in gone) _layers.Remove(h);
        }

        _viewKey = viewKey;
        LastRenderStats = (rendered, cached);
        return Composite(_order);
    }

    public void Invalidate(ulong layerHandle)
    {
        if (_layers.TryGetValue(layerHandle, out var entry))
            entry.Fingerprint = "";
    }

    public void InvalidateAll() => _viewKey = null;

    public PixelBuffer? GetLayerBuffer(ulong layerHandle)
        => _layers.TryGetValue(layerHandle, out var e) ? e.Buffer : null;

    public PixelBuffer Composite(IReadOnlyList<ulong> visibleLayers)
    {
        PixelBuffer? any = null;
        foreach (var lh in visibleLayers)
            if (_layers.TryGetValue(lh, out var le)) { any = le.Buffer; break; }
        int w = any?.Width ?? _viewKey?.W ?? 1;
        int h = any?.Height ?? _viewKey?.H ?? 1;

        var dst = new PixelBuffer(w, h);
        foreach (var handle in visibleLayers)
        {
            if (!_layers.TryGetValue(handle, out var layer)) continue;
            var src = layer.Buffer;
            if (src.Width != w || src.Height != h) continue;
            var cov = src.Coverage;
            if (cov == null) continue;
            var sp = src.Pixels;
            var dp = dst.Pixels;
            for (int i = 0; i < cov.Length; i++)
            {
                if (cov[i] == 0) continue;
                int o = i * 4;
                dp[o] = sp[o]; dp[o + 1] = sp[o + 1]; dp[o + 2] = sp[o + 2]; dp[o + 3] = sp[o + 3];
            }
        }
        return dst;
    }


    public static string Fingerprint(IReadOnlyList<DwgEntity> entities)
    {
        ulong h = 1469598103934665603UL;
        void Mix(double v)
        {
            ulong bits = BitConverter.DoubleToUInt64Bits(v);
            for (int i = 0; i < 8; i++) { h ^= (bits >> (i * 8)) & 0xFF; h *= 1099511628211UL; }
        }
        void MixInt(uint v)
        {
            for (int i = 0; i < 4; i++) { h ^= (v >> (i * 8)) & 0xFF; h *= 1099511628211UL; }
        }
        void MixStr(string? s)
        {
            MixInt((uint)(s?.Length ?? 0));
            if (s == null) return;
            foreach (char c in s) MixInt(c);
        }

        foreach (var e in entities)
        {
            MixInt((uint)e.Kind);
            Mix((double)e.Handle);
            MixInt((uint)(ushort)e.ColorIndex);
            Mix((double)e.LayerHandle);
            Mix(e.Start.X); Mix(e.Start.Y);
            Mix(e.End.X); Mix(e.End.Y);
            Mix(e.Center.X); Mix(e.Center.Y);
            Mix(e.Radius); Mix(e.StartAngle); Mix(e.EndAngle);
            Mix(e.Height); Mix(e.Rotation); Mix(e.WidthFactor);
            Mix(e.ScaleX); Mix(e.ScaleY);
            Mix(e.ExtrusionZ); Mix(e.Thickness);
            MixStr(e.TextValue); MixStr(e.Name); MixStr(e.LtypeName);
            MixInt((uint)e.Points.Count);
            foreach (var p in e.Points) { Mix(p.X); Mix(p.Y); }
            MixInt((uint)e.Bulges.Count);
            foreach (var b in e.Bulges) Mix(b);
            MixInt((uint)e.Corners.Count);
            foreach (var c in e.Corners) { Mix(c.X); Mix(c.Y); }
            Mix(e.Measurement);
            Mix(e.DimTextRotation); Mix(e.DimLineRotation);
            MixStr(e.DimSubtype);
            Mix(e.TextAttachment);
        }
        return h.ToString("X16");
    }
}
