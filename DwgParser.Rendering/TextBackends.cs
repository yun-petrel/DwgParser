using System.Runtime.InteropServices;

using DwgParser.Core.Render;
namespace DwgParser.Rendering;

public static class TextBackends
{
    private static readonly bool _windows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    private static ITextRendererBackend? _current;

    public static ITextRendererBackend Current => _current ??= _windows ? GdiTextBackend.Instance : ManagedTextBackend.Instance;

    public static void Override(ITextRendererBackend? backend) => _current = backend;

    public static bool IsGdi => _windows;

    public static ITextShapeBackend? ShapeBackend
        => Current as ITextShapeBackend;

    static TextBackends() => RenderingRuntime.EnsureSystemFonts();
}
