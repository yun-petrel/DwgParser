using DwgParser.Core;
using DwgParser.Core.Format;
using DwgParser.Core.Objects;
using DwgParser.Core.Render;
using DwgParser.Core.Strings;

namespace DwgParser.Cli;

public static partial class Program
{
    private static string Hex(ulong v) => ProgramRuntime.Hex(v);
    private static string Hex(short v) => ProgramRuntime.Hex(v);

    private static string ResolveName(DwgDocument doc, short type) => ProgramRuntime.ResolveName(doc, type);

    private static void Pt(System.Text.Json.Utf8JsonWriter w, string name, Vec2 p)
        => ProgramRuntime.Pt(w, name, p);

    private static void Pt3(System.Text.Json.Utf8JsonWriter w, string name, Vec3 p)
        => ProgramRuntime.Pt3(w, name, p);

    private static void Num(System.Text.Json.Utf8JsonWriter w, string name, double v)
        => ProgramRuntime.Num(w, name, v);

    private static void Num(System.Text.Json.Utf8JsonWriter w, string name, int v)
        => ProgramRuntime.Num(w, name, v);

    private static void NumValue(System.Text.Json.Utf8JsonWriter w, double v)
        => ProgramRuntime.NumValue(w, v);
}
