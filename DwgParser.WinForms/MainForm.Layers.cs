using DwgParser.Core.Objects.Records;

namespace DwgParser.WinForms;

internal sealed partial class MainForm
{
    private ulong? _activeLayer;

    private const int MaxLabelChars = 4;

    private static readonly Color LayerAllBase = Color.FromArgb(72, 72, 82);

    private static Color LayerColor(int index)
        => FromHsl((205 + index * 137.508) % 360, 0.62, 0.42);

    private static Color FromHsl(double h, double s, double l)
    {
        double c = (1 - Math.Abs(2 * l - 1)) * s;
        double hp = h / 60, x = c * (1 - Math.Abs(hp % 2 - 1));
        double r = 0, g = 0, b = 0;
        if (hp < 1) { r = c; g = x; }
        else if (hp < 2) { r = x; g = c; }
        else if (hp < 3) { g = c; b = x; }
        else if (hp < 4) { g = x; b = c; }
        else if (hp < 5) { r = x; b = c; }
        else { r = c; b = x; }
        double m = l - c / 2;
        return Color.FromArgb(255,
            (int)Math.Round((r + m) * 255),
            (int)Math.Round((g + m) * 255),
            (int)Math.Round((b + m) * 255));
    }

    private sealed record LayerButtonInfo(ulong Handle, Color Base);

    private void BuildLayerBar()
    {
        int charW = TextRenderer.MeasureText("国", Font).Width;
        int charH = TextRenderer.MeasureText("国", Font).Height;

        _btnAllLayers = new Button
        {
            Dock = DockStyle.Top,
            Height = ButtonHeight(charH),
            Text = VerticalLabel("所有图层"),
            TextAlign = ContentAlignment.MiddleCenter,
            FlatStyle = FlatStyle.Flat,
            TabStop = false
        };
        _btnAllLayers.FlatAppearance.BorderColor = SystemColors.ControlDark;
        _btnAllLayers.Click += (_, _) => SelectLayerFilter(null);

        _layerFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            Padding = new Padding(2)
        };
        _layerFlow.Resize += (_, _) => LayoutLayerButtons();

        _layerBar = new Panel
        {
            Dock = DockStyle.Left,
            Width = charW + 8,
            Padding = new Padding(1)
        };

        _layerBar.Controls.Add(_layerFlow);
        _layerBar.Controls.Add(_btnAllLayers);
    }

    private void RebuildLayerBar()
    {
        _activeLayer = null;
        int btnH = ButtonHeight(TextRenderer.MeasureText("国", Font).Height);

        _layerFlow.SuspendLayout();
        try
        {
            foreach (Control c in _layerFlow.Controls) c.Dispose();
            _layerFlow.Controls.Clear();

            if (_scene != null)
            {
                var entries = CollectLayerEntries();
                for (int i = 0; i < entries.Count; i++)
                {
                    var (handle, label) = entries[i];
                    ulong h = handle;
                    var info = new LayerButtonInfo(h, LayerColor(i));
                    var btn = new Button
                    {
                        Text = VerticalLabel(label),
                        Height = btnH,
                        TextAlign = ContentAlignment.MiddleCenter,
                        Padding = new Padding(0),
                        FlatStyle = FlatStyle.Flat,
                        Margin = new Padding(1, 1, 1, 1),
                        TabStop = false,
                        Tag = info
                    };
                    btn.FlatAppearance.BorderColor = SystemColors.ControlDark;
                    btn.Click += (_, _) => SelectLayerFilter(h);
                    _layerTip.SetToolTip(btn, label);
                    _layerFlow.Controls.Add(btn);
                }
            }
        }
        finally
        {
            _layerFlow.ResumeLayout(true);
        }

        UpdateLayerButtonStates();
        LayoutLayerButtons();
    }

    private static int ButtonHeight(int charHeight) => charHeight * MaxLabelChars + 8;

    private static string VerticalLabel(string name)
    {
        string s = name?.Trim() ?? "";
        if (s.Length == 0) return "—";
        var chars = s.Take(MaxLabelChars).ToList();
        if (s.Length > MaxLabelChars) chars[^1] = '…';
        return string.Join('\n', chars);
    }

    private List<(ulong Handle, string Label)> CollectLayerEntries()
    {
        var entries = new List<(ulong Handle, string Label)>();
        if (_scene == null) return entries;
        var seen = new HashSet<ulong>();

        foreach (var layer in _scene.Layers)
        {
            if (!seen.Add(layer.Handle)) continue;
            entries.Add((layer.Handle, LayerLabel(layer)));
        }

        foreach (var e in _scene.AllEntities)
            if (seen.Add(e.LayerHandle))
                entries.Add((e.LayerHandle, _scene.LayerNameOf(e.LayerHandle)));

        return entries;
    }

    private string LayerLabel(ILayer layer)
    {
        string name = _scene?.LayerNameOf(layer.Handle) ?? $"图层:{layer.Handle:X}";
        var marks = new List<string>();
        if (layer.Off) marks.Add("off");
        if (layer.Frozen) marks.Add("frozen");
        if (layer.Locked) marks.Add("locked");
        return marks.Count == 0 ? name : $"{name}  [{string.Join(",", marks)}]";
    }

    private void SelectLayerFilter(ulong? handle)
    {
        if (_scene == null) return;
        if (_activeLayer == handle) return;

        _activeLayer = handle;
        _scene.ShowOnlyLayer(handle);

        if (handle is { } only && _canvas.Selected is { } sel && sel.LayerHandle != only)
            _canvas.SelectEntity(null);

        UpdateLayerButtonStates();
        _canvas.InvalidateRender();
        RefreshStatus();
    }

    private void UpdateLayerButtonStates()
    {
        bool all = _activeLayer is null;
        ApplyLayerButtonState(_btnAllLayers, LayerAllBase, all);

        foreach (Control c in _layerFlow.Controls)
        {
            if (c is not Button b || b.Tag is not LayerButtonInfo info) continue;
            ApplyLayerButtonState(b, info.Base, !all && _activeLayer == info.Handle);
        }
    }

    private static void ApplyLayerButtonState(Button b, Color baseColor, bool active)
    {
        b.BackColor = active ? baseColor : Tint(baseColor, 0.72);
        b.ForeColor = active ? Color.White : baseColor;
    }

    private static Color Tint(Color c, double amount)
        => Color.FromArgb(255,
            (int)(c.R + (255 - c.R) * amount),
            (int)(c.G + (255 - c.G) * amount),
            (int)(c.B + (255 - c.B) * amount));

    private int LayerButtonWidth()
        => TextRenderer.MeasureText("国", Font).Width + 4;

    private void LayoutLayerButtons()
    {
        int btnW = LayerButtonWidth();
        foreach (Control c in _layerFlow.Controls) c.Width = btnW;

        int scroll = _layerFlow.VerticalScroll.Visible
            ? SystemInformation.VerticalScrollBarWidth + 2 : 0;
        int want = btnW + _layerFlow.Padding.Horizontal
                   + _layerBar.Padding.Horizontal + scroll + 2;
        if (_layerBar.Width != want) _layerBar.Width = want;
    }
}
