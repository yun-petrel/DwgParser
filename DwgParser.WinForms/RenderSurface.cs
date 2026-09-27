using System.Drawing.Imaging;
using DwgParser.Core.Render;
using DwgParser.Rendering;

namespace DwgParser.WinForms;

internal sealed class RenderSurface
{
    private readonly FrameRenderer _frame = new();

    public bool DarkBackground { get; set; }

    public int LineThickness { get; set; } = 1;

    public Bitmap Render(DwgScene scene, ViewTransform view)
        => ToBitmap(RenderToBuffer(scene, view, LineThickness, DarkBackground));

    public static PixelBuffer RenderToBuffer(DwgScene scene, ViewTransform view, int thickness, bool dark)
    {
        int w = Math.Max(1, view.WidthPx);
        int h = Math.Max(1, view.HeightPx);
        var buf = new PixelBuffer(w, h);
        if (dark) buf.Clear(0, 0, 0);

        var viewport = new DrawViewport(view.MinX, view.MinY, view.MaxX, view.MaxY, w, h, 0, false);
        var frame = new FrameRenderer { DarkBackground = dark };
        frame.Prepare(scene.Walk);
        frame.RenderEntities(scene.VisibleEntities, buf, Math.Max(1, thickness), viewport);
        return buf;
    }

    public static PixelBuffer RenderFit(DwgScene scene, int widthPx, int heightPx, int thickness, bool dark)
    {
        var view = new ViewTransform();
        var b = scene.Bounds;
        view.Fit(b.MinX, b.MinY, b.MaxX, b.MaxY, widthPx, heightPx);
        return RenderToBuffer(scene, view, thickness, dark);
    }

    public static Bitmap ToBitmap(PixelBuffer buf)
    {
        var bmp = new Bitmap(buf.Width, buf.Height, PixelFormat.Format32bppArgb);
        var rect = new Rectangle(0, 0, buf.Width, buf.Height);
        var data = bmp.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        try
        {
            int rowBytes = buf.Width * 4;
            for (int y = 0; y < buf.Height; y++)
            {
                IntPtr dst = data.Scan0 + y * data.Stride;
                System.Runtime.InteropServices.Marshal.Copy(buf.Pixels, y * rowBytes, dst, rowBytes);
            }
        }
        finally
        {
            bmp.UnlockBits(data);
        }
        return bmp;
    }
}
