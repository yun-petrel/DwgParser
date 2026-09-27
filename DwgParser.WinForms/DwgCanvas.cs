using System.Drawing.Drawing2D;
using DwgParser.Core.Objects;

namespace DwgParser.WinForms;

internal sealed class DwgCanvas : Control
{
    private const int ClickSlopPx = 4;
    private const int PickTolerancePx = 6;

    private readonly ViewTransform _view = new();
    private readonly RenderSurface _surface = new();

    private DwgScene? _scene;
    private Bitmap? _bitmap;
    private string _bitmapKey = "";
    private bool _bitmapDirty = true;
    private string? _renderError;

    private bool _dragging;
    private bool _dragMoved;
    private Point _dragOrigin;
    private Point _dragLast;

    public DwgCanvas()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint
                 | ControlStyles.OptimizedDoubleBuffer
                 | ControlStyles.UserPaint
                 | ControlStyles.ResizeRedraw, true);
        BackColor = Color.FromArgb(32, 32, 32);
        TabStop = true;
    }

    public DwgEntity? Selected { get; private set; }

    public event EventHandler? SelectionChanged;

    public event EventHandler<(double X, double Y)>? CursorMoved;

    public event EventHandler? ViewChanged;

    public ViewTransform View => _view;
    public RenderSurface Surface => _surface;

    public bool DarkBackground
    {
        get => _surface.DarkBackground;
        set
        {
            if (_surface.DarkBackground == value) return;
            _surface.DarkBackground = value;
            InvalidateRender();
        }
    }

    public void SetScene(DwgScene? scene)
    {
        _scene = scene;
        Selected = null;
        _bitmapDirty = true;
        if (scene != null)
        {
            var b = scene.Bounds;
            _view.Fit(b.MinX, b.MinY, b.MaxX, b.MaxY, ClientSize.Width, ClientSize.Height);
        }
        SelectionChanged?.Invoke(this, EventArgs.Empty);
        ViewChanged?.Invoke(this, EventArgs.Empty);
        Invalidate();
    }

    public void InvalidateRender()
    {
        _bitmapDirty = true;
        Invalidate();
    }

    public void FitToDrawing()
    {
        if (_scene == null) return;
        var b = _scene.Bounds;
        _view.Fit(b.MinX, b.MinY, b.MaxX, b.MaxY, ClientSize.Width, ClientSize.Height);
        OnViewChanged();
    }

    public void ZoomToSelection()
    {
        if (_scene == null) return;
        if (Selected == null) { FitToDrawing(); return; }
        var b = _scene.BoundsOf(Selected);
        if (!b.IsFinite) { FitToDrawing(); return; }
        _view.CenterOn(b.MinX, b.MinY, b.MaxX, b.MaxY);
        OnViewChanged();
    }

    public void ZoomBy(double factor)
    {
        if (_scene == null) return;
        _view.ZoomAt(ClientSize.Width / 2.0, ClientSize.Height / 2.0, factor);
        OnViewChanged();
    }

    public void SelectEntity(DwgEntity? entity)
    {
        if (ReferenceEquals(Selected, entity)) return;
        Selected = entity;
        SelectionChanged?.Invoke(this, EventArgs.Empty);
        Invalidate();
    }


    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(BackColor);

        if (_scene == null)
        {
            DrawHint(g, "用「文件 → 打开图纸…」载入 DWG（也可把 .dwg 拖到窗口上）", Color.Gainsboro);
            return;
        }

        EnsureBitmap();
        if (_bitmap != null) g.DrawImageUnscaled(_bitmap, 0, 0);
        if (_renderError != null) DrawHint(g, "渲染失败：" + _renderError, Color.Salmon);

        DrawSelection(g);
    }

    private void DrawHint(Graphics g, string text, Color color)
    {
        using var font = new Font(Font.FontFamily, 10f);
        using var brush = new SolidBrush(color);
        g.DrawString(text, font, brush, 16, 16);
    }

    private void EnsureBitmap()
    {
        if (_scene == null) return;
        int w = Math.Max(1, ClientSize.Width), h = Math.Max(1, ClientSize.Height);
        string key = string.Join('|',
            _view.MinX.ToString("R"), _view.MinY.ToString("R"), _view.Scale.ToString("R"),
            w.ToString(), h.ToString(), _surface.LineThickness.ToString(),
            _surface.DarkBackground.ToString());

        if (!_bitmapDirty && key == _bitmapKey && _bitmap is not null) return;

        var old = _bitmap;
        _bitmap = null;
        _renderError = null;
        try
        {
            _bitmap = _surface.Render(_scene, _view);
            _bitmapKey = key;
            _bitmapDirty = false;
        }
        catch (Exception ex)
        {
            _renderError = ex.Message;
            _bitmapKey = "";
        }
        finally
        {
            old?.Dispose();
        }
    }

    private void DrawSelection(Graphics g)
    {
        if (_scene == null || Selected == null) return;
        var b = _scene.BoundsOf(Selected);
        if (!b.IsFinite) return;

        float x0 = (float)_view.ScreenX(b.MinX), x1 = (float)_view.ScreenX(b.MaxX);
        float y0 = (float)_view.ScreenY(b.MaxY), y1 = (float)_view.ScreenY(b.MinY);
        if (x1 < x0) (x0, x1) = (x1, x0);
        if (y1 < y0) (y0, y1) = (y1, y0);

        var rect = RectangleF.FromLTRB(x0 - 3, y0 - 3, x1 + 3, y1 + 3);
        using var pen = new Pen(Color.FromArgb(255, 140, 0), 1.5f)
        {
            DashStyle = DashStyle.Custom,
            DashPattern = new[] { 5f, 3f }
        };
        g.DrawRectangle(pen, rect.X, rect.Y, rect.Width, rect.Height);

        using var font = new Font(Font.FontFamily, 8f);
        using var textBrush = new SolidBrush(Color.FromArgb(255, 200, 120));
        using var backBrush = new SolidBrush(Color.FromArgb(170, 0, 0, 0));
        string label = $"{Selected.Kind} 0x{Selected.Handle:X}";
        var size = g.MeasureString(label, font);
        float ly = rect.Y - size.Height - 2;
        if (ly < 0) ly = rect.Bottom + 2;
        g.FillRectangle(backBrush, rect.X, ly, size.Width + 4, size.Height);
        g.DrawString(label, font, textBrush, rect.X + 2, ly);
    }


    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        if (_scene == null) return;
        _view.ZoomAt(e.X, e.Y, e.Delta > 0 ? 1.2 : 1 / 1.2);
        OnViewChanged();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();
        if (_scene == null) return;
        if (e.Button is MouseButtons.Left or MouseButtons.Middle)
        {
            _dragging = true;
            _dragMoved = false;
            _dragOrigin = e.Location;
            _dragLast = e.Location;
            Cursor = Cursors.SizeAll;
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        if (_scene != null)
        {
            var (wx, wy) = _view.ToWorld(e.X, e.Y);
            CursorMoved?.Invoke(this, (wx, wy));
        }

        if (!_dragging) return;

        int dx = e.X - _dragLast.X, dy = e.Y - _dragLast.Y;
        _dragLast = e.Location;

        if (!_dragMoved
            && Math.Abs(e.X - _dragOrigin.X) + Math.Abs(e.Y - _dragOrigin.Y) >= ClickSlopPx)
        {
            _dragMoved = true;
            dx = e.X - _dragOrigin.X;
            dy = e.Y - _dragOrigin.Y;
        }

        if (_dragMoved && (dx != 0 || dy != 0))
        {
            _view.PanPixels(dx, dy);
            OnViewChanged();
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (!_dragging) return;
        _dragging = false;
        Cursor = Cursors.Default;

        if (!_dragMoved && e.Button == MouseButtons.Left)
            PickAt(e.X, e.Y);
    }

    public void PickAt(int screenX, int screenY)
    {
        if (_scene == null) return;
        var (wx, wy) = _view.ToWorld(screenX, screenY);
        double tolerance = PickTolerancePx / Math.Max(_view.Scale, 1e-9);
        SelectEntity(EntityHitTest.Pick(_scene, wx, wy, tolerance));
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        _view.Resize(ClientSize.Width, ClientSize.Height);
        _bitmapDirty = true;
        ViewChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnViewChanged()
    {
        _bitmapDirty = true;
        ViewChanged?.Invoke(this, EventArgs.Empty);
        Invalidate();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _bitmap?.Dispose();
            _bitmap = null;
        }
        base.Dispose(disposing);
    }
}
