using DwgParser.Core;
using DwgParser.Core.Format;
using DwgParser.Core.Objects;
using DwgParser.Core.Render;
using DwgParser.Core.Strings;
using DwgParser.Rendering;

namespace DwgParser.Cli;

public static partial class Program
{
    public static int Main(string[] args)
    {
        DwgParser.Rendering.RenderingRuntime.EnsureSystemFonts();
        if (args.Length < 1)
        {
            Console.WriteLine("用法: dwginfo <文件.dwg> [选项]");
            Console.WriteLine();
            Console.WriteLine("查询选项:");
            Console.WriteLine("  --strings            提取文本/图层/字体等字符串");
            Console.WriteLine("  --classes            统计 AcDb 对象类别");
            Console.WriteLine("  --types              按类型码统计对象分布（诊断解码覆盖率）");
            Console.WriteLine("  --preview <file.bmp> 导出内嵌预览位图");
            Console.WriteLine("  --entities           解析对象流并列出实体");
            Console.WriteLine("  --eedaudit           EED 链普查：弃解具名 + 列信息段计数（只读诊断）");
            Console.WriteLine("  --json <file.json>   导出实体/图层/块的结构化 JSON（隐含 --entities，供真值比对）");
            Console.WriteLine("  --svg <file.svg>     渲染为 SVG（隐含 --entities）");
            Console.WriteLine("  --acis <file.sab>    导出 R2013+ ACIS 三族外置于 AcDs 数据树的几何负载（隐含 --entities）");
            Console.WriteLine();
            Console.WriteLine("转换选项:");
            Console.WriteLine("  --convert <版本>     转换为指定版本 (AC1015/AC1018/AC1021/AC1024/AC1027/AC1032)");
            Console.WriteLine("  --output <文件.dwg>  转换输出路径（与 --convert 配合使用）");
            Console.WriteLine("  --edit-attr <句柄[=新文本值][~新标记]>");
            Console.WriteLine("                       值级改写一枚 ATTRIB（配 --convert；写侧未领用即非零退出）");
            Console.WriteLine();
            Console.WriteLine("图纸修改（机读面，供技能调用；契约与判据见 docs/design-cli-edit-interface.md）:");
            Console.WriteLine("  --inspect <f.json>   列候选 + 每枚当下可跑的算子（配 --kind/--contains/--limit）");
            Console.WriteLine("  --ops <操作单.json>  批量改写并另存；必配 --output 与 --report");
            Console.WriteLine("                       算子：setText / setAttrib / delete / addMText（句柄写十六进制串）");
            Console.WriteLine("                       中文值只走文件、不走 argv；可叠 --convert 一步改+转");
            Console.WriteLine("  退出码：0 全绿已落盘 / 1 有枚未落地或自证不合（产物不留）/ 2 用法不合");
            Console.WriteLine();
            Console.WriteLine("示例:");
            Console.WriteLine("  dwginfo drawing.dwg --convert AC1015 --output old.dwg");
            Console.WriteLine("  dwginfo drawing.dwg --svg output.svg");
            return 2;
        }

        string path = args[0];

        string? opsPath = GetOption(args, "--ops");
        if (opsPath != null)
            return OpsMode(path, opsPath, GetOption(args, "--output"), GetOption(args, "--report"),
                args);
        string? inspectOut = GetOption(args, "--inspect");
        if (inspectOut != null) return InspectMode(path, inspectOut, args);

        string? convertTarget = GetOption(args, "--convert");
        if (convertTarget != null)
        {
            return ConvertMode(path, convertTarget, GetOption(args, "--output"), args);
        }

        if (args.Contains("--objects"))
        {
            var d0 = DwgDocument.Load(path);
            var objs0 = d0.GetObjects();
            foreach (var r in objs0.Records.OrderBy(x => x.Handle))
                Console.WriteLine($"h={r.Handle:X5} t={r.TypeCode,4} bytes={r.BodyEnd - r.BodyStart,6} v={r.SourceVersion}");
            return 0;
        }

        if (args.Contains("--fieldize"))
        {
            try
            {
                FieldizeReport(path, DwgDocument.Load(path));
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("字段化普查失败: " + ex.Message);
                return 1;
            }
            return 0;
        }

        if (args.Contains("--census"))
        {
            try
            {
                CensusReport(path, DwgDocument.Load(path));
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("字段化普查失败: " + ex.Message);
                return 1;
            }
            return 0;
        }

        if (args.Contains("--eedaudit"))
        {
            try
            {
                EedAuditReport(path, DwgDocument.Load(path));
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("EED 普查失败: " + ex.Message);
                return 1;
            }
            return 0;
        }

        bool wantStrings = args.Contains("--strings");
        bool wantClasses = args.Contains("--classes");
        bool wantEntities = args.Contains("--entities");
        string? jsonOut = GetOption(args, "--json");
        if (jsonOut != null) wantEntities = true;
        bool wantTypes = args.Contains("--types");
        string? previewOut = GetOption(args, "--preview");
        string? svgOut = GetOption(args, "--svg");
        if (svgOut != null) wantEntities = true;
        string? pngOut = GetOption(args, "--png");
        string? pngLayeredOut = GetOption(args, "--pngL");
        if (pngOut != null || pngLayeredOut != null) wantEntities = true;
        string? acisOut = GetOption(args, "--acis");
        if (acisOut != null) wantEntities = true;

        Console.WriteLine($"文件: {path}");
        Console.WriteLine(new string('=', 72));

        DwgDocument doc;
        try
        {
            doc = DwgDocument.Load(path);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("加载失败: " + ex.Message);
            return 1;
        }

        Console.WriteLine($"版本签名 : {doc.Header.VersionString}  ({doc.VersionName})");
        if (!doc.Version.IsSupportedVersion())
            Console.WriteLine($"           低于最低支持版本 {DwgVersionExtensions.MinimumSupportedVersion}，"
                              + "容器与对象流不解析，以下仅为结构扫描结果");
        Console.WriteLine($"维护版本 : {doc.Header.MaintenanceRelease}");
        Console.WriteLine($"文件大小 : {doc.FileSize:N0} 字节");
        if (doc.Header.LocatorMarker != 0)
            Console.WriteLine($"定位标记 : 0x{doc.Header.LocatorMarker:X}");
        if (doc.Header.SystemAreaAddress != 0)
            Console.WriteLine($"系统区指针: 0x{doc.Header.SystemAreaAddress:X}");
        if (doc.Header.PreviewAddress is uint pa)
            Console.WriteLine($"预览地址(候选): 0x{pa:X}" + (doc.HasPreviewBitmap() ? "" : " (无有效位图)"));
        if (doc.Header.CodepageGuess != 0)
            Console.WriteLine($"代码页   : {doc.Header.CodepageGuess}");

        foreach (var d in doc.Diagnostics)
            Console.WriteLine("[诊断] " + d);

        Console.WriteLine();
        Console.WriteLine($"发现节数量: {doc.Sections.Count}");
        foreach (var s in doc.Sections.Take(40))
            Console.WriteLine("  " + s + (s.Data != null ? $" data={s.Data.Length:N0}" : " (未解码)"));

        if (doc.R2000 != null)
        {
            Console.WriteLine();
            Console.WriteLine($"R2000 对象映射: {doc.R2000.Handles.Count} 个对象, 类定义: {doc.R2000.Classes.Count} 个");
        }

        if (acisOut != null)
        {
            var objs = doc.GetObjects();
            var withAcis = objs.Entities.Where(e => e.AcisData is { Length: > 0 }).ToList();
            Console.WriteLine();
            Console.WriteLine("R2013+ ACIS 负载导出 (来自 AcDb:AcDsPrototype_1b 数据树):");
            if (withAcis.Count == 0)
            {
                Console.WriteLine("  无（非 AC1027+/无 AcDs 节/未含 ACIS 三族实体）");
            }
            else
            {
                foreach (var e in withAcis)
                {
                    int nv = e.AcisVertices?.Length ?? 0;
                    Console.WriteLine($"  hdl={e.Handle:X} {e.Kind,-8} acis={e.AcisData!.Length:N0} 字节  顶点={nv}");
                    if (e.AcisVertices is { } vs)
                        foreach (var (v, k) in vs.Select((v, k) => (v, k)))
                            Console.WriteLine($"      v[{k}] = ({v.X:0.######}, {v.Y:0.######}, {v.Z:0.######})");
                }
                var first = withAcis[0];
                try
                {
                    WriteAtomically(acisOut, tmp => File.WriteAllBytes(tmp, first.AcisData!));
                    Console.WriteLine($"  → 已写 {first.Kind} hdl={first.Handle:X} 的 ACIS 到 {acisOut}");
                }
                catch (Exception ex) { Console.Error.WriteLine("  写盘失败: " + ex.Message); }
            }
        }

        if (wantEntities)
        {
            Console.WriteLine();
            Console.WriteLine("实体解析:");
            Console.WriteLine(new string('-', 72));
            var objs = doc.GetObjects();
            int liveCount = objs.Entities.Count(e => !e.IsErased);
            int ghostCount = objs.Entities.Count(e => e.IsErased);
            Console.WriteLine($"对象总数={objs.ObjectsSeen} 已解码={objs.ObjectsDecoded} 错误={objs.Errors} 实体={objs.Entities.Count} (存活={liveCount} 幽灵={ghostCount}) 块={objs.BlockRegions.Count}");
            foreach (var e in objs.Entities.Where(e => !e.IsErased).Take(80))
                Console.WriteLine($"  hdl={e.Handle:X6} {e.Kind,-11} color={e.ColorIndex,-4} owner={e.OwnerHandle:X6} " + ProgramRuntime.Geo(e));
            foreach (var b in objs.BlockRegions.Take(20))
                Console.WriteLine($"  BLOCK hdl={b.HeaderHandle:X6} '{b.NameGuess}' ents={b.EntityCount}");
        }

        if (jsonOut != null)
        {
            try
            {
                WriteJsonExport(jsonOut, path, doc, doc.GetObjects());
                Console.WriteLine($"JSON 已输出: {jsonOut}");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("JSON 导出失败: " + ex.Message);
                return 1;
            }
        }

        if (svgOut != null)
        {
            try
            {
                var objs = doc.GetObjects();
                string svg = SvgRenderer.Render(objs);
                WriteAtomically(svgOut, tmp => File.WriteAllText(tmp, svg));
                Console.WriteLine($"SVG 已输出: {svgOut} ({svg.Length:N0} 字节)");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("SVG 渲染失败: " + ex.Message);
                return 1;
            }
        }

        if (pngOut != null)
        {
            try
            {
                var objs = doc.GetObjects();
                var buf = new FrameRenderer(TextBackends.Current).Render(objs, 900, 900, 1);
                byte[] png = PngEncoder.Encode(buf);
                WriteAtomically(pngOut, tmp => File.WriteAllBytes(tmp, png));
                Console.WriteLine($"PNG 已输出: {pngOut} ({png.Length:N0} 字节)");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("PNG 渲染失败: " + ex.Message);
                return 1;
            }
        }

        if (pngLayeredOut != null)
        {
            try
            {
                var objs = doc.GetObjects();
                var buf = new LayeredFrameRenderer().Render(objs, 900, 900, 1);
                byte[] png = PngEncoder.Encode(buf);
                WriteAtomically(pngLayeredOut, tmp => File.WriteAllBytes(tmp, png));
                Console.WriteLine($"PNG(分层) 已输出: {pngLayeredOut} ({png.Length:N0} 字节)");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("PNG 分层渲染失败: " + ex.Message);
                return 1;
            }
        }

        if (wantTypes)
        {
            Console.WriteLine();
            Console.WriteLine("对象类型分布（★=已支持解码）:");
            Console.WriteLine(new string('-', 72));
            var objs = doc.GetObjects();
            foreach (var (type, count) in objs.TypeCounts.OrderByDescending(kv => kv.Value))
            {
                string name = ProgramRuntime.ResolveName(doc, type);
                bool supported = !name.StartsWith("Unknown");
                Console.WriteLine($"  0x{type:X2} {name,-16} x{count}{(supported ? "" : "  （未解码）")}");
            }
        }

        if (wantClasses)
        {
            Console.WriteLine();
            Console.WriteLine("对象类别统计:");
            Console.WriteLine(new string('-', 72));
            var classes = doc.ScanObjectClasses();
            if (classes.Count == 0)
                Console.WriteLine("  (未发现)");
            foreach (var c in classes.Take(50))
                Console.WriteLine($"  {c.ClassName,-40} x{c.Count}");
        }

        if (wantStrings)
        {
            Console.WriteLine();
            Console.WriteLine("提取字符串:");
            Console.WriteLine(new string('-', 72));
            var all = doc.ExtractStrings();

            var ordered = all
                .OrderBy(s => ProgramRuntime.Rank(s.Kind))
                .ThenByDescending(s => s.Value.Length)
                .ToList();

            foreach (var s in ordered.Take(200))
                Console.WriteLine($"  @0x{s.Offset:X8} [{s.Kind}] {s.Value}");
            Console.WriteLine($"  ... 共 {all.Count} 条");
        }

        if (previewOut != null)
        {
            var bmp = doc.TryExtractPreview();
            if (bmp == null)
            {
                Console.WriteLine("未找到可导出的预览位图。");
            }
            else
            {
                WriteAtomically(previewOut, tmp => File.WriteAllBytes(tmp, bmp));
                Console.WriteLine($"预览已导出: {previewOut} ({bmp.Length:N0} 字节)");
            }
        }

        return 0;
    }


    private static string? GetOption(string[] args, string name)
    {
        int i = Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }

    private static int ConvertMode(string inputPath, string targetVersionStr, string? outputPath,
        string[] args)
    {
        var targetVersion = ParseVersionString(targetVersionStr);
        if (targetVersion == DwgVersion.Unknown)
        {
            Console.Error.WriteLine($"错误: 无法识别的版本 '{targetVersionStr}'");
            Console.Error.WriteLine("支持的版本: AC1015, AC1018, AC1021, AC1024, AC1027, AC1032");
            return 1;
        }

        if (string.IsNullOrEmpty(outputPath))
        {
            var ext = Path.GetExtension(inputPath);
            var dir = Path.GetDirectoryName(inputPath) ?? ".";
            var name = Path.GetFileNameWithoutExtension(inputPath);
            outputPath = Path.Combine(dir, $"{name}_converted_{targetVersionStr}{ext}");
        }

        Console.WriteLine($"源文件: {inputPath}");
        Console.WriteLine($"目标版本: {targetVersionStr} ({targetVersion.GetVersionName()})");
        Console.WriteLine($"输出文件: {outputPath}");
        Console.WriteLine();

        try
        {
            if (GetOption(args, "--edit-attr") is string spec)
            {
                string? err = TryParseAttrSpec(spec, out var attrOp);
                if (attrOp is null)
                {
                    Console.Error.WriteLine("✗ 属性修改被拒: " + err);
                    return 1;
                }
                var report = DwgEditor.Apply(DwgConverter.Load(inputPath),
                    new EditOp[] { attrOp }, outputPath, targetVersion);
                var o = report.Outcomes[0];
                Console.WriteLine($"属性改写: h={attrOp.Handle:X} 文本值 = {attrOp.Text ?? "(不变)"} "
                                  + $"/ 标记 = {attrOp.Tag ?? "(不变)"}");
                if (!report.Written)
                {
                    Console.Error.WriteLine($"✗ 属性修改未生效（{o.Status}）: {o.Reason}");
                    return 1;
                }
                Console.WriteLine("✓ 转换成功!");
                Console.WriteLine($"  文件大小: {new FileInfo(outputPath).Length:N0} 字节");
                Console.WriteLine($"  输出路径: {outputPath}");
                return 0;
            }

            var file = DwgConverter.Load(inputPath);
            DwgConverter.Save(file, targetVersion, outputPath);
            var size = new FileInfo(outputPath).Length;

            Console.WriteLine("✓ 转换成功!");
            Console.WriteLine($"  文件大小: {size:N0} 字节");
            Console.WriteLine($"  输出路径: {outputPath}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"✗ 转换失败: {ex.Message}");
            return 1;
        }
    }

    private static string? TryParseAttrSpec(string spec, out SetAttribOp? op)
    {
        op = null;
        int eq = spec.IndexOf('=');
        int tilde = spec.IndexOf('~');
        string head = (eq >= 0 ? spec[..eq] : (tilde >= 0 ? spec[..tilde] : spec)).Trim();
        string? text = null, tag = null;
        if (eq >= 0)
        {
            string rest = spec[(eq + 1)..];
            int t2 = rest.IndexOf('~');
            text = t2 >= 0 ? rest[..t2] : rest;
            if (t2 >= 0) tag = rest[(t2 + 1)..];
        }
        else if (tilde >= 0) tag = spec[(tilde + 1)..];
        if (text is null && tag is null)
            return $"语法应为 <句柄>[=<新文本>][~<新标记>]，实到「{spec}」。";
        if (!ulong.TryParse(head, System.Globalization.NumberStyles.HexNumber,
                System.Globalization.CultureInfo.InvariantCulture, out ulong handle))
            return $"句柄不是十六进制:「{head}」。";
        op = new SetAttribOp(handle, text, tag);
        return null;
    }

    private static DwgVersion ParseVersionString(string s) => s.ToUpperInvariant() switch
    {
        "AC1015" or "R2000" => DwgVersion.AC1015,
        "AC1018" or "R2004" => DwgVersion.AC1018,
        "AC1021" or "R2007" => DwgVersion.AC1021,
        "AC1024" or "R2010" => DwgVersion.AC1024,
        "AC1027" or "R2013" => DwgVersion.AC1027,
        "AC1032" or "R2018" => DwgVersion.AC1032,
        _ => DwgVersion.Unknown
    };
}
