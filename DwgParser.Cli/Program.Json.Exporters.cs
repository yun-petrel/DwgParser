using DwgParser.Core;
using DwgParser.Core.Format;
using DwgParser.Core.Objects;
using DwgParser.Core.Render;
using DwgParser.Core.Strings;

namespace DwgParser.Cli;

public static partial class ProgramJsonExport
{
    internal static void WriteXrecord(System.Text.Json.Utf8JsonWriter w, DwgParser.Core.Objects.Records.XrecordCapture? xr)
    {
        if (xr is null || !xr.Captured) return;
        w.WriteStartObject("xrecord");
        w.WriteNumber("xdataSize", xr.XdataSize);
        w.WriteBoolean("xdataBeyondDeclared", xr.XdataBeyondDeclared);
        w.WriteBoolean("dataBeyondFrame", xr.DataBeyondFrame);
        w.WriteNumber("cloning", xr.Cloning);
        w.WriteBoolean("cloningBeyondFrame", xr.CloningBeyondFrame);
        w.WriteBoolean("xdicMissing", xr.XdicMissing);
        w.WriteNumber("itemCount", xr.Items.Count);

        w.WriteStartObject("summary");
        var histogram = xr.CodeHistogram;
        w.WriteNumber("distinctCodes", histogram.Count);
        w.WriteStartArray("codeDistribution");
        foreach (var (code, count) in histogram.OrderByDescending(kv => kv.Value))
        {
            w.WriteStartObject();
            w.WriteNumber("code", code);
            w.WriteNumber("count", count);
            var first = xr.FindByCode(code);
            if (first != null) w.WriteString("valueType", first.ValueType);
            w.WriteEndObject();
        }
        w.WriteEndArray();
        w.WriteEndObject();

        w.WriteStartArray("items");
        foreach (var it in xr.Items)
        {
            w.WriteStartObject();
            w.WriteNumber("code", it.Code);
            w.WriteNumber("bits", it.BitLen);
            w.WriteString("type", it.ValueType);
            if (it.Text is not null) w.WriteString("text", it.Text);
            if (it.Number is long l) w.WriteNumber("int", l);
            else if (it.Number is double d) w.WriteNumber("real", d);
            if (it.IsHandle) w.WriteNumber("handle", it.HandleValue);
            if (it.Point is { } p)
            {
                w.WriteStartArray("point");
                foreach (var c in p) w.WriteNumberValue(c);
                w.WriteEndArray();
            }
            if (it.Binary is { } bin)
            {
                w.WriteNumber("binaryLen", bin.Length);
                int previewLen = Math.Min(bin.Length, 16);
                var hex = new System.Text.StringBuilder(previewLen * 2);
                for (int i = 0; i < previewLen; i++) hex.Append(bin[i].ToString("x2"));
                if (bin.Length > 16) hex.Append("...");
                w.WriteString("binaryPreview", hex.ToString());
            }
            w.WriteEndObject();
        }
        w.WriteEndArray();
        w.WriteEndObject();
    }
}
