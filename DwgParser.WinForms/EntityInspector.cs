using System.Globalization;
using DwgParser.Core.Format;
using DwgParser.Core.Objects;

namespace DwgParser.WinForms;

internal enum PropKind
{
    ReadOnly,
    Number,
    Integer,
    Text,
    Boolean,
    Choice
}

internal sealed class PropRow
{
    public required string Name { get; init; }
    public required string Value { get; init; }
    public PropKind Kind { get; init; } = PropKind.ReadOnly;
    public string[] Choices { get; init; } = Array.Empty<string>();

    public Func<string, string?>? Apply { get; init; }

    public bool IsReadOnly => Kind == PropKind.ReadOnly || Apply is null;
}

internal static class EntityInspector
{
    private const double DegToRad = Math.PI / 180.0;
    private const double RadToDeg = 180.0 / Math.PI;

    public static List<PropRow> Build(DwgEntity e, DwgScene scene)
    {
        var rows = new List<PropRow>();
        void Touch() => scene.InvalidateBounds(e);

        rows.Add(Ro("句柄", $"0x{e.Handle:X}"));
        rows.Add(Ro("类型", e.Kind.ToString()));
        rows.Add(Ro("所有者句柄", $"0x{e.OwnerHandle:X}"));

        var layerNames = scene.Layers
            .Select(l => l.Name is { Length: > 0 } n ? n : $"图层:{l.Handle:X}")
            .ToArray();
        rows.Add(new PropRow
        {
            Name = "图层",
            Value = scene.LayerNameOf(e.LayerHandle),
            Kind = PropKind.Choice,
            Choices = layerNames,
            Apply = text =>
            {
                var layer = scene.Layers.FirstOrDefault(l =>
                    string.Equals(l.Name is { Length: > 0 } n ? n : $"图层:{l.Handle:X}", text,
                                  StringComparison.Ordinal));
                if (layer is null) return $"找不到图层「{text}」";
                e.LayerHandle = layer.Handle;
                return null;
            }
        });

        rows.Add(Int("颜色索引", () => e.ColorIndex, v =>
        {
            if (v is < 0 or > 256) throw new ArgumentOutOfRangeException(nameof(v), "颜色索引应在 0–256（256 = ByLayer）");
            e.ColorIndex = (short)v;
        }));
        rows.Add(Ro("真彩色", e.HasTrueColor ? $"#{e.TrueColor & 0xFFFFFF:X6}" : "(无)"));
        rows.Add(TextRow("线型名", () => e.LtypeName, v => e.LtypeName = v));
        rows.Add(Ro("空间模式", e.EntMode switch
        {
            0 => "0 模型空间",
            1 => "1 图纸空间",
            _ => $"{e.EntMode}"
        }));

        rows.Add(Num("厚度", () => e.Thickness, v => e.Thickness = v));
        rows.Add(Num("标高", () => e.Elevation, v => e.Elevation = v));
        rows.Add(Num("挤出方向 Z", () => e.ExtrusionZ, v => e.ExtrusionZ = v));

        switch (e.Kind)
        {
            case DwgEntityType.Line:
            case DwgEntityType.Ray:
            case DwgEntityType.XLine:
                rows.Add(Num("起点 X", () => e.Start.X, v => e.Start = new Vec2(v, e.Start.Y)));
                rows.Add(Num("起点 Y", () => e.Start.Y, v => e.Start = new Vec2(e.Start.X, v)));
                rows.Add(Num("终点 X", () => e.End.X, v => e.End = new Vec2(v, e.End.Y)));
                rows.Add(Num("终点 Y", () => e.End.Y, v => e.End = new Vec2(e.End.X, v)));
                break;

            case DwgEntityType.Point:
                rows.Add(Num("位置 X", () => e.Start.X, v => e.Start = new Vec2(v, e.Start.Y)));
                rows.Add(Num("位置 Y", () => e.Start.Y, v => e.Start = new Vec2(e.Start.X, v)));
                break;

            case DwgEntityType.Circle:
                AddCircle(rows, e);
                break;

            case DwgEntityType.Arc:
                AddCircle(rows, e);
                rows.Add(Angle("起始角", () => e.StartAngle, v => e.StartAngle = v));
                rows.Add(Angle("终止角", () => e.EndAngle, v => e.EndAngle = v));
                break;

            case DwgEntityType.Ellipse:
                rows.Add(Num("圆心 X", () => e.Center.X, v => e.Center = new Vec2(v, e.Center.Y)));
                rows.Add(Num("圆心 Y", () => e.Center.Y, v => e.Center = new Vec2(e.Center.X, v)));
                rows.Add(Num("长半轴", () => e.Radius, v => e.Radius = v));
                rows.Add(Num("长轴向量 X", () => e.Start.X, v => e.Start = new Vec2(v, e.Start.Y)));
                rows.Add(Num("长轴向量 Y", () => e.Start.Y, v => e.Start = new Vec2(e.Start.X, v)));
                rows.Add(Num("短长轴比", () => e.WidthFactor, v => e.WidthFactor = v));
                rows.Add(Angle("起始角", () => e.StartAngle, v => e.StartAngle = v));
                rows.Add(Angle("终止角", () => e.EndAngle, v => e.EndAngle = v));
                break;

            case DwgEntityType.Text:
            case DwgEntityType.MText:
            case DwgEntityType.AttDef:
            case DwgEntityType.Attrib:
                AddText(rows, e, scene);
                break;

            case DwgEntityType.LwPolyline:
            case DwgEntityType.Polyline2D:
                rows.Add(Ro("顶点数", e.Points.Count.ToString(CultureInfo.InvariantCulture)));
                rows.Add(Bool("闭合", () => e.Closed, v => e.Closed = v));
                rows.Add(Num("常宽", () => e.ConstantWidth, v => e.ConstantWidth = v));
                rows.Add(Num("起点宽度", () => e.VertexStartWidths.Count > 0 ? e.VertexStartWidths[0] : 0,
                             v => SetVertexWidth(e.VertexStartWidths, v)));
                rows.Add(Num("终点宽度", () => e.VertexEndWidths.Count > 0 ? e.VertexEndWidths[0] : 0,
                             v => SetVertexWidth(e.VertexEndWidths, v)));
                rows.Add(Bool("多段线生成", () => e.PlineGen, v => e.PlineGen = v));
                break;

            case DwgEntityType.Solid:
            case DwgEntityType.Trace:
                rows.Add(Bool("实心填充", () => e.SolidFill, v => e.SolidFill = v));
                rows.Add(Ro("角点数", e.Corners.Count.ToString(CultureInfo.InvariantCulture)));
                for (int i = 0; i < e.Corners.Count; i++)
                {
                    int idx = i;
                    rows.Add(Num($"角点[{idx}] X", () => e.Corners[idx].X,
                                 v => e.Corners[idx] = new Vec2(v, e.Corners[idx].Y)));
                    rows.Add(Num($"角点[{idx}] Y", () => e.Corners[idx].Y,
                                 v => e.Corners[idx] = new Vec2(e.Corners[idx].X, v)));
                }
                break;

            case DwgEntityType.Insert:
                rows.Add(Num("插入点 X", () => e.Start.X, v => e.Start = new Vec2(v, e.Start.Y)));
                rows.Add(Num("插入点 Y", () => e.Start.Y, v => e.Start = new Vec2(e.Start.X, v)));
                rows.Add(Angle("旋转", () => e.Rotation, v => e.Rotation = v));
                rows.Add(Num("比例 X", () => e.ScaleX, v => e.ScaleX = v));
                rows.Add(Num("比例 Y", () => e.ScaleY, v => e.ScaleY = v));
                rows.Add(Num("比例 Z", () => e.ScaleZ, v => e.ScaleZ = v));
                rows.Add(Ro("块头句柄", $"0x{e.BlockHeaderHandle:X}"));
                break;

            case DwgEntityType.Dimension:
                rows.Add(Ro("测量值", e.Measurement.ToString("0.######", CultureInfo.InvariantCulture)));
                rows.Add(Ro("标注子类", string.IsNullOrEmpty(e.DimSubtype) ? "(未解)" : e.DimSubtype));
                rows.Add(Num("文字中点 X", () => e.DimTextMid.X, v => e.DimTextMid = new Vec2(v, e.DimTextMid.Y)));
                rows.Add(Num("文字中点 Y", () => e.DimTextMid.Y, v => e.DimTextMid = new Vec2(e.DimTextMid.X, v)));
                rows.Add(Angle("文字旋转", () => e.DimTextRotation, v => e.DimTextRotation = v));
                rows.Add(Angle("标注线旋转", () => e.DimLineRotation, v => e.DimLineRotation = v));
                rows.Add(Ro("顶点数", e.Points.Count.ToString(CultureInfo.InvariantCulture)));
                break;

            case DwgEntityType.Viewport:
                rows.Add(Num("视口宽", () => e.VportWidth, v => e.VportWidth = v));
                rows.Add(Num("视图中心 X", () => e.VportViewCenter.X, v => e.VportViewCenter = new Vec2(v, e.VportViewCenter.Y)));
                rows.Add(Num("视图中心 Y", () => e.VportViewCenter.Y, v => e.VportViewCenter = new Vec2(e.VportViewCenter.X, v)));
                rows.Add(Num("视图尺寸", () => e.VportViewSize, v => e.VportViewSize = v));
                rows.Add(Angle("视图扭转", () => e.VportTwist, v => e.VportTwist = v));
                break;

            case DwgEntityType.MLine:
                rows.Add(Ro("顶点数", e.Points.Count.ToString(CultureInfo.InvariantCulture)));
                break;

            case DwgEntityType.Tolerance:
                rows.Add(Num("插入点 X", () => e.Start.X, v => e.Start = new Vec2(v, e.Start.Y)));
                rows.Add(Num("插入点 Y", () => e.Start.Y, v => e.Start = new Vec2(e.Start.X, v)));
                break;

            case DwgEntityType.Table:
                rows.Add(Num("插入点 X", () => e.Start.X, v => e.Start = new Vec2(v, e.Start.Y)));
                rows.Add(Num("插入点 Y", () => e.Start.Y, v => e.Start = new Vec2(e.Start.X, v)));
                rows.Add(Angle("旋转", () => e.Rotation, v => e.Rotation = v));
                rows.Add(Num("比例 X", () => e.ScaleX, v => e.ScaleX = v));
                rows.Add(Num("比例 Y", () => e.ScaleY, v => e.ScaleY = v));
                break;
        }

        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            if (row.Apply is null || row.Kind == PropKind.ReadOnly) continue;
            var inner = row.Apply;
            rows[i] = new PropRow
            {
                Name = row.Name,
                Value = row.Value,
                Kind = row.Kind,
                Choices = row.Choices,
                Apply = text =>
                {
                    try
                    {
                        var err = inner(text);
                        if (err is null) Touch();
                        return err;
                    }
                    catch (Exception ex)
                    {
                        return ex.Message;
                    }
                }
            };
        }
        return rows;
    }

    private static void AddCircle(List<PropRow> rows, DwgEntity e)
    {
        rows.Add(Num("圆心 X", () => e.Center.X, v => e.Center = new Vec2(v, e.Center.Y)));
        rows.Add(Num("圆心 Y", () => e.Center.Y, v => e.Center = new Vec2(e.Center.X, v)));
        rows.Add(Num("半径", () => e.Radius, v => e.Radius = v));
    }

    private static void AddText(List<PropRow> rows, DwgEntity e, DwgScene scene)
    {
        bool persist = e.Kind == DwgEntityType.Attrib;
        rows.Add(TextRow("文本", () => e.TextValue,
            v => SetTextFamily(e, persist ? scene.Document.File : null, v, isTag: false)));
        if (e.Kind == DwgEntityType.Attrib || e.Kind == DwgEntityType.AttDef)
            rows.Add(TextRow("标记", () => e.Tag,
                v => SetTextFamily(e, persist ? scene.Document.File : null, v, isTag: true)));
        rows.Add(Num("位置 X", () => e.Start.X, v => e.Start = new Vec2(v, e.Start.Y)));
        rows.Add(Num("位置 Y", () => e.Start.Y, v => e.Start = new Vec2(e.Start.X, v)));
        rows.Add(Num("对齐点 X", () => e.AlignmentPoint.X, v => e.AlignmentPoint = new Vec2(v, e.AlignmentPoint.Y)));
        rows.Add(Num("对齐点 Y", () => e.AlignmentPoint.Y, v => e.AlignmentPoint = new Vec2(e.AlignmentPoint.X, v)));
        rows.Add(Num("字高", () => e.Height, v => e.Height = v));
        rows.Add(Angle("旋转", () => e.Rotation, v => e.Rotation = v));
        rows.Add(Num("宽度因子", () => e.WidthFactor, v => e.WidthFactor = v));
        rows.Add(Angle("倾斜角", () => e.ObliqueAngle, v => e.ObliqueAngle = v));
        rows.Add(Int("水平对齐", () => e.HorizMode, v => e.HorizMode = (short)v));
        rows.Add(Int("垂直对齐", () => e.VertMode, v => e.VertMode = (short)v));
    }

    private static string? SetTextFamily(DwgEntity e, IDwgFile? file, string value,
        bool isTag)
    {
        if (file is null)
        {
            if (isTag) e.Tag = value; else e.TextValue = value;
            return null;
        }
        var err = DwgAttributeEditor.TryEdit(file, e.Handle,
            textValue: isTag ? null : value, tag: isTag ? value : null);
        if (err is not null) return err;
        if (isTag) e.Tag = value; else e.TextValue = value;
        return null;
    }

    private static void SetVertexWidth(List<double> list, double value)
    {
        if (list.Count == 0) list.Add(value);
        else list[0] = value;
    }


    private static PropRow Ro(string name, string value) => new() { Name = name, Value = value };

    private static PropRow Num(string name, Func<double> get, Action<double> set) => new()
    {
        Name = name,
        Value = get().ToString("0.######", CultureInfo.InvariantCulture),
        Kind = PropKind.Number,
        Apply = text => ParseDouble(text, out double v) ?? Run(() => set(v))
    };

    private static PropRow Angle(string name, Func<double> get, Action<double> set) => new()
    {
        Name = name + "（度）",
        Value = (get() * RadToDeg).ToString("0.######", CultureInfo.InvariantCulture),
        Kind = PropKind.Number,
        Apply = text => ParseDouble(text, out double deg) ?? Run(() => set(deg * DegToRad))
    };

    private static PropRow Int(string name, Func<int> get, Action<int> set) => new()
    {
        Name = name,
        Value = get().ToString(CultureInfo.InvariantCulture),
        Kind = PropKind.Integer,
        Apply = text =>
        {
            if (!int.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int v))
                return "不是合法整数";
            return Run(() => set(v));
        }
    };

    private static PropRow TextRow(string name, Func<string> get, Action<string> set) => new()
    {
        Name = name,
        Value = get() ?? "",
        Kind = PropKind.Text,
        Apply = text => Run(() => set(text))
    };

    private static PropRow Bool(string name, Func<bool> get, Action<bool> set) => new()
    {
        Name = name,
        Value = get() ? "是" : "否",
        Kind = PropKind.Boolean,
        Choices = new[] { "是", "否" },
        Apply = text => text.Trim() switch
        {
            "是" or "True" or "true" or "1" => Run(() => set(true)),
            "否" or "False" or "false" or "0" => Run(() => set(false)),
            _ => "请填「是」或「否」"
        }
    };

    private static string? ParseDouble(string text, out double value)
    {
        if (double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value)) return null;
        if (double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.CurrentCulture, out value)) return null;
        return "不是合法数字";
    }

    private static string? Run(Action action)
    {
        action();
        return null;
    }
}
