using DwgParser.Rendering;

namespace DwgParser.WinForms;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        RenderingRuntime.EnsureSystemFonts();

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);

        string? initial = args.Length > 0 && File.Exists(args[0]) ? Path.GetFullPath(args[0]) : null;
        Application.Run(new MainForm(initial));
    }
}
