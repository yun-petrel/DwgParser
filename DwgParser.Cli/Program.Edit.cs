using System.Text.Json;
using DwgParser.Core;
using DwgParser.Core.Format;

namespace DwgParser.Cli;

public static partial class Program
{
    internal static int OpsMode(string path, string opsPath, string? outDwg, string? reportPath,
        string[] args)
    {
        if (string.IsNullOrEmpty(outDwg) || string.IsNullOrEmpty(reportPath))
        {
            Console.Error.WriteLine("用法: --ops <操作单.json> 必配 --output <新图.dwg> 与 "
                + "--report <报告.json>（不给原地覆盖源图任何通路）");
            return 2;
        }
        if (!File.Exists(opsPath))
        {
            Console.Error.WriteLine("操作单不存在: " + opsPath);
            return 2;
        }
        if (!TryParseOps(opsPath, out var ops, out string? parseErr))
        {
            Console.Error.WriteLine("操作单不合: " + parseErr);
            return 2;
        }
        DwgVersion? target = null;
        if (GetOption(args, "--convert") is string tv)
        {
            target = ParseVersionString(tv);
            if (target == DwgVersion.Unknown)
            {
                Console.Error.WriteLine($"错误: 无法识别的版本 '{tv}'");
                return 2;
            }
        }

        IDwgFile file;
        try
        {
            file = DwgConverter.Load(path);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("加载失败: " + ex.Message);
            return 1;
        }

        EditReport report;
        try
        {
            report = DwgEditor.Apply(file, ops, outDwg, target);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("改写失败: " + ex.Message);
            return 1;
        }

        try
        {
            WriteAtomically(reportPath, tmp => WriteReportBody(tmp, report));
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("报告写出失败: " + ex.Message);
            return 1;
        }

        foreach (var o in report.Outcomes)
            Console.WriteLine($"[{o.Status}] #{o.Index} {o.OpName} h={o.Handle:x}"
                              + (o.Reason is null ? "" : "  " + o.Reason));
        Console.WriteLine(report.Written
            ? $"✓ 已落盘 {report.ProductPath}（{report.ProductBytes:N0} 字节，"
              + $"实体 {report.Counts.EntitiesBefore}→{report.Counts.EntitiesAfter}）"
            : "✗ 未落盘（产物不留，败因逐条见报告）");
        return report.Written ? 0 : 1;
    }

    internal static int InspectMode(string path, string outJson, string[] args)
    {
        IDwgFile file;
        try
        {
            file = DwgConverter.Load(path);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("加载失败: " + ex.Message);
            return 1;
        }
        var res = DwgEditor.Inspect(file, GetOption(args, "--kind"), GetOption(args, "--contains"),
            ParseIntOr(GetOption(args, "--limit"), 0));
        try
        {
            WriteAtomically(outJson, tmp => WriteInspectBody(tmp, path, res));
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("候选清单写出失败: " + ex.Message);
            return 1;
        }
        Console.WriteLine($"候选 {res.Candidates.Count} 枚（已解码 {res.DecodedRecords} / "
                          + $"未解码 {res.UndecodedRecords}）→ {outJson}");
        return 0;
    }

    private static int ParseIntOr(string? s, int def) =>
        int.TryParse(s, System.Globalization.NumberStyles.Integer,
            System.Globalization.CultureInfo.InvariantCulture, out int v) ? v : def;

    private static bool TryParseOps(string path, out List<EditOp> ops, out string? err)
    {
        ops = new List<EditOp>();
        err = null;
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(File.ReadAllText(path));
        }
        catch (Exception ex)
        {
            err = "不是合法 JSON: " + ex.Message;
            return false;
        }
        using (doc)
        {
            if (doc.RootElement.ValueKind != JsonValueKind.Object
                || !doc.RootElement.TryGetProperty("ops", out var arr)
                || arr.ValueKind != JsonValueKind.Array)
            {
                err = "操作单根须为对象且含 ops 数组。";
                return false;
            }
            int i = 0;
            try
            {
            foreach (var j in arr.EnumerateArray())
            {
                string name = j.TryGetProperty("op", out var n) ? n.GetString() ?? "" : "";
                string? hs = j.TryGetProperty("handle", out var h) ? h.GetString() : null;
                ulong handle = 0;
                if (hs is not null && !ulong.TryParse(hs, System.Globalization.NumberStyles
                        .HexNumber, System.Globalization.CultureInfo.InvariantCulture, out handle))
                {
                    err = $"#{i} 的句柄不是十六进制串：「{hs}」。";
                    return false;
                }
                switch (name)
                {
                    case "setText":
                        if (!TryStr(j, "value", out string? v))
                        {
                            err = $"#{i} setText 缺 value。";
                            return false;
                        }
                        ops.Add(new SetTextOp(handle, v!));
                        break;
                    case "setAttrib":
                        TryStr(j, "text", out string? t);
                        TryStr(j, "tag", out string? g);
                        ops.Add(new SetAttribOp(handle, t, g));
                        break;
                    case "delete":
                        ops.Add(new DeleteOp(handle));
                        break;
                    case "addMText":
                        if (!TryStr(j, "value", out string? mv))
                        {
                            err = $"#{i} addMText 缺 value。";
                            return false;
                        }
                        TryStr(j, "layer", out string? lay);
                        ops.Add(new AddMTextOp(Num(j, "x"), Num(j, "y"), mv!, Num(j, "height"), lay));
                        break;
                    default:
                        err = $"#{i} 未知算子「{name}」（合法：setText/setAttrib/delete/addMText）。";
                        return false;
                }
                i++;
            }
            }
            catch (Exception ex)
            {
                err = "操作单元素解析失败: " + ex.Message;
                return false;
            }
        }
        if (ops.Count == 0) { err = "ops 为空单。"; return false; }
        return true;
    }

    private static bool TryStr(JsonElement j, string prop, out string? value)
    {
        if (j.TryGetProperty(prop, out var e) && e.ValueKind == JsonValueKind.String)
        {
            value = e.GetString();
            return true;
        }
        value = null;
        return false;
    }

    private static double Num(JsonElement j, string prop) =>
        j.TryGetProperty(prop, out var e) && e.ValueKind == JsonValueKind.Number
            ? e.GetDouble() : 0d;

    private static void WriteReportBody(string path, EditReport r)
    {
        using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
        using var w = new System.Text.Json.Utf8JsonWriter(fs);
        w.WriteStartObject();
        w.WriteString("contract", "dwgreport/1");
        w.WriteString("sourceVersion", r.SourceVersion);
        w.WriteNumber("sourceBytes", r.SourceBytes);
        w.WriteString("product", Path.GetFullPath(r.ProductPath).Replace('\\', '/'));
        w.WriteNumber("productBytes", r.ProductBytes);
        w.WriteBoolean("written", r.Written);
        w.WriteStartObject("counts");
        w.WriteNumber("entitiesBefore", r.Counts.EntitiesBefore);
        w.WriteNumber("entitiesAfter", r.Counts.EntitiesAfter);
        w.WriteNumber("objectsBefore", r.Counts.ObjectsBefore);
        w.WriteNumber("objectsAfter", r.Counts.ObjectsAfter);
        w.WriteNumber("walkErrors", r.Counts.WalkErrors);
        w.WriteEndObject();
        w.WriteStartObject("untouchedFingerprint");
        w.WriteString("before", r.FingerprintBefore);
        w.WriteString("after", r.FingerprintAfter);
        w.WriteBoolean("equal", r.FingerprintBefore.Length > 0
            && r.FingerprintBefore == r.FingerprintAfter);
        w.WriteEndObject();
        w.WriteStartObject("referenceCoverage");
        w.WriteNumber("decodedRecords", r.Coverage.DecodedRecords);
        w.WriteNumber("undecodedRecords", r.Coverage.UndecodedRecords);
        w.WriteBoolean("refsComplete", r.Coverage.RefsComplete);
        w.WriteEndObject();
        w.WriteStartArray("outcomes");
        foreach (var o in r.Outcomes)
        {
            w.WriteStartObject();
            w.WriteNumber("index", o.Index);
            w.WriteString("op", o.OpName);
            w.WriteString("handle", o.Handle.ToString("x"));
            w.WriteString("status", o.Status.ToString());
            w.WriteBoolean("verified", o.Verified);
            if (o.Reason is null) w.WriteNull("reason"); else w.WriteString("reason", o.Reason);
            w.WriteEndObject();
        }
        w.WriteEndArray();
        w.WriteEndObject();
        w.Flush();
    }

    private static void WriteInspectBody(string path, string srcPath, InspectResult res)
    {
        using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
        using var w = new System.Text.Json.Utf8JsonWriter(fs);
        w.WriteStartObject();
        w.WriteString("contract", "dwginspect/1");
        w.WriteString("file", Path.GetFullPath(srcPath).Replace('\\', '/'));
        w.WriteString("version", res.Version);
        w.WriteNumber("decodedRecords", res.DecodedRecords);
        w.WriteNumber("undecodedRecords", res.UndecodedRecords);
        w.WriteBoolean("refsComplete", res.UndecodedRecords == 0);
        w.WriteStartArray("candidates");
        foreach (var c in res.Candidates)
        {
            w.WriteStartObject();
            w.WriteString("handle", c.Handle.ToString("x"));
            w.WriteString("kind", c.Kind);
            w.WriteString("text", c.Text);
            w.WriteStartArray("ops");
            foreach (var o in c.Ops) w.WriteStringValue(o);
            w.WriteEndArray();
            w.WriteEndObject();
        }
        w.WriteEndArray();
        w.WriteEndObject();
        w.Flush();
    }
}
