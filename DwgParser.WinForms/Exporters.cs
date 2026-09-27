using System.Globalization;
using System.Text;
using System.Text.Json;
using DwgParser.Core;
using DwgParser.Core.Format;
using DwgParser.Core.Objects;
using DwgParser.Core.Render;
using DwgParser.Rendering;

namespace DwgParser.WinForms;

internal static class Exporters
{
    public static void SavePng(DwgScene scene, string path, int widthPx, int heightPx, int thickness, bool dark)
    {
        var buf = RenderSurface.RenderFit(scene, widthPx, heightPx, thickness, dark);
        WriteAtomic(path, PngEncoder.Encode(buf));
    }

    public static void SaveSvg(DwgScene scene, string path, bool dark)
    {
        string svg = SvgRenderer.Render(scene.Walk, flipY: true, margin: 10.0, darkBackground: dark);
        WriteAtomic(path, new UTF8Encoding(false).GetBytes(svg));
    }

    public static void SaveJson(DwgScene scene, string path)
    {
        using var ms = new MemoryStream();
        using (var w = new Utf8JsonWriter(ms, new JsonWriterOptions { Indented = true }))
        {
            w.WriteStartObject();
            w.WriteString("file", scene.FilePath);
            w.WriteString("version", scene.Document.VersionName);
            w.WriteNumber("entitiesTotal", scene.AllEntities.Count);
            w.WriteNumber("entitiesVisible", scene.VisibleEntities.Count);

            w.WriteStartArray("entities");
            foreach (var e in scene.VisibleEntities)
            {
                w.WriteStartObject();
                w.WriteString("handle", $"0x{e.Handle:X}");
                w.WriteString("kind", e.Kind.ToString());
                w.WriteString("layer", scene.LayerNameOf(e.LayerHandle));
                w.WriteNumber("colorIndex", e.ColorIndex);
                w.WriteString("owner", $"0x{e.OwnerHandle:X}");
                if (!string.IsNullOrEmpty(e.LtypeName)) w.WriteString("ltype", e.LtypeName);
                if (!string.IsNullOrEmpty(e.TextValue)) w.WriteString("text", e.TextValue);

                WritePoint(w, "start", e.Start);
                if (e.Kind is DwgEntityType.Line or DwgEntityType.Ray or DwgEntityType.XLine)
                    WritePoint(w, "end", e.End);
                if (e.Kind is DwgEntityType.Circle or DwgEntityType.Arc or DwgEntityType.Ellipse)
                {
                    WritePoint(w, "center", e.Center);
                    w.WriteNumber("radius", Round(e.Radius));
                }
                if (e.Kind is DwgEntityType.Text or DwgEntityType.MText
                    or DwgEntityType.AttDef or DwgEntityType.Attrib)
                {
                    w.WriteNumber("height", Round(e.Height));
                    w.WriteNumber("rotationDeg", Round(e.Rotation * 180.0 / Math.PI));
                }
                if (e.Points.Count > 0)
                {
                    w.WriteStartArray("points");
                    foreach (var p in e.Points)
                    {
                        w.WriteStartObject();
                        w.WriteNumber("x", Round(p.X));
                        w.WriteNumber("y", Round(p.Y));
                        w.WriteEndObject();
                    }
                    w.WriteEndArray();
                }
                if (e.Corners.Count > 0)
                {
                    w.WriteStartArray("corners");
                    foreach (var c in e.Corners)
                    {
                        w.WriteStartObject();
                        w.WriteNumber("x", Round(c.X));
                        w.WriteNumber("y", Round(c.Y));
                        w.WriteEndObject();
                    }
                    w.WriteEndArray();
                }
                w.WriteEndObject();
            }
            w.WriteEndArray();
            w.WriteEndObject();
        }
        WriteAtomic(path, ms.ToArray());
    }

    public static void SaveDwg(DwgScene scene, string path, DwgVersion target)
        => DwgConverter.Save(scene.Document.File, target, path);

    private static void WritePoint(Utf8JsonWriter w, string name, Vec2 p)
    {
        w.WriteStartObject(name);
        w.WriteNumber("x", Round(p.X));
        w.WriteNumber("y", Round(p.Y));
        w.WriteEndObject();
    }

    private static double Round(double v)
        => double.IsFinite(v) ? Math.Round(v, 6, MidpointRounding.AwayFromZero) : 0;

    private static void WriteAtomic(string path, byte[] bytes)
    {
        string dir = Path.GetDirectoryName(Path.GetFullPath(path)) ?? ".";
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        string tmp = path + ".tmp";
        File.WriteAllBytes(tmp, bytes);
        if (File.Exists(path)) File.Delete(path);
        File.Move(tmp, path);
    }

    public static readonly DwgVersion[] TargetVersions =
    {
        DwgVersion.AC1015, DwgVersion.AC1018, DwgVersion.AC1021,
        DwgVersion.AC1024, DwgVersion.AC1027, DwgVersion.AC1032
    };

    public static string VersionLabel(DwgVersion v)
        => v switch
        {
            DwgVersion.AC1015 => "AC1015 (AutoCAD 2000)",
            DwgVersion.AC1018 => "AC1018 (AutoCAD 2004)",
            DwgVersion.AC1021 => "AC1021 (AutoCAD 2007)",
            DwgVersion.AC1024 => "AC1024 (AutoCAD 2010)",
            DwgVersion.AC1027 => "AC1027 (AutoCAD 2013)",
            DwgVersion.AC1032 => "AC1032 (AutoCAD 2018)",
            _ => v.ToString()
        };

    public static string Describe(DwgScene scene)
        => string.Format(CultureInfo.InvariantCulture,
            "{0}  版本 {1} ({2})  文件 {3:N0} 字节",
            scene.DisplayName, scene.Document.Header.VersionString,
            scene.Document.VersionName, scene.Document.FileSize);
}
