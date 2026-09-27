using DwgParser.Core;
using DwgParser.Core.Format;
using DwgParser.Core.Objects;
using DwgParser.Core.Render;
using DwgParser.Core.Strings;

namespace DwgParser.Cli;

internal static class ProgramRuntime
{
    internal static string Hex(ulong v) => v.ToString("x");
    internal static string Hex(short v) => v.ToString("x2");

    internal static string ResolveName(DwgDocument doc, short type)
        => R2000TypeNames.ResolveDisplayName(type)
           ?? (doc.File switch
           {
               R2000File r2 => r2.ResolveTypeName(type),
               R2004File r4 => r4.ResolveTypeName(type),
               _ => null,
           })
           ?? DwgObjectReader.WalkResult.TypeName(type);

    internal static void Pt(System.Text.Json.Utf8JsonWriter w, string name, Vec2 p)
    {
        w.WriteStartArray(name);
        NumValue(w, p.X);
        NumValue(w, p.Y);
        w.WriteEndArray();
    }

    internal static void Pt3(System.Text.Json.Utf8JsonWriter w, string name, Vec3 p)
    {
        w.WriteStartArray(name);
        NumValue(w, p.X);
        NumValue(w, p.Y);
        NumValue(w, p.Z);
        w.WriteEndArray();
    }

    internal static void Num(System.Text.Json.Utf8JsonWriter w, string name, double v)
    {
        if (double.IsNaN(v) || double.IsInfinity(v)) w.WriteString(name, v.ToString("R"));
        else w.WriteNumber(name, v);
    }

    internal static void Num(System.Text.Json.Utf8JsonWriter w, string name, int v) => w.WriteNumber(name, v);

    internal static void NumValue(System.Text.Json.Utf8JsonWriter w, double v)
    {
        if (double.IsNaN(v) || double.IsInfinity(v)) w.WriteStringValue(v.ToString("R"));
        else w.WriteNumberValue(v);
    }

    internal static int Rank(StringKind k) => k switch
    {
        StringKind.ClassName => 0,
        StringKind.FontRef => 1,
        StringKind.Path => 2,
        StringKind.SymbolTable => 3,
        _ => 4,
    };

    internal static string Geo(DwgEntity e) => e.Kind switch
    {
        DwgEntityType.Line => $"{e.Start.X:F2},{e.Start.Y:F2} -> {e.End.X:F2},{e.End.Y:F2}",
        DwgEntityType.Circle => $"c=({e.Center.X:F2},{e.Center.Y:F2}) r={e.Radius:F2}",
        DwgEntityType.Arc => $"c=({e.Center.X:F2},{e.Center.Y:F2}) r={e.Radius:F2} a={e.StartAngle:F3}..{e.EndAngle:F3}",
        DwgEntityType.Text => $"\"{e.TextValue}\" @({e.Start.X:F2},{e.Start.Y:F2}) h={e.Height:F2}",
        DwgEntityType.Insert => $"@({e.Start.X:F2},{e.Start.Y:F2}) rot={e.Rotation:F3} blk={e.BlockHeaderHandle:X6}",
        DwgEntityType.Point => $"({e.Start.X:F2},{e.Start.Y:F2})",
        DwgEntityType.Solid => $"corners={e.Corners.Count}",
        DwgEntityType.LwPolyline or DwgEntityType.Polyline2D => $"pts={e.Points.Count} bulges={e.Bulges.Count}",
        _ => "",
    };
}
