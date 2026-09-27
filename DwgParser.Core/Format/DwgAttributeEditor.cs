using DwgParser.Core.Objects;

namespace DwgParser.Core.Format;

public static class DwgAttributeEditor
{
    private const int TextFieldIndex = 0;

    private const int TagFieldIndex = 1;

    private const int AttribStringCount = 2;

    public static string? TryEdit(IDwgFile file, ulong handle, string? textValue = null,
        string? tag = null)
    {
        if (file is null) return "file 为空。";
        if (textValue is null && tag is null) return "未给出任何新值。";
        if (!file.Version.IsSupportedVersion())
            return $"源版本 {file.Version} 低于受支持下限，对象流不解析 ⇒ 无法定位属性帧。";

        var walk = file.GetObjects();
        var entity = walk.Entities.Find(e => e.Handle == handle && e.Kind == DwgEntityType.Attrib);
        if (entity is null) return $"h={handle:X} 不是本图里的 ATTRIB 实体。";
        var record = walk.Records.Find(r => r.Handle == handle && r.LogicalType == 0x02);
        if (record is null)
            return $"h={handle:X} 有实体无记录（对象流未走到该帧的写侧搬运面）。";
        if (walk.SourceData is null)
            return "对象流缓冲区未随遍历结果挂上，写侧无位窗可定位。";
        if (record.StringFields.Count < AttribStringCount)
            return $"h={handle:X} 的串捕获只有 {record.StringFields.Count} 枚（需正文值 + tag 两枚）。";
        if (record.StringFields[TextFieldIndex].Value != entity.TextValue
            || record.StringFields[TagFieldIndex].Value != entity.Tag)
            return $"h={handle:X} 的串捕获与实体槽不一致（[0]≠TextValue 或 [1]≠Tag）⇒ 序位不可信，未改。";

        var edits = new List<WriteSideEdits.StringFieldEdit>(2);
        if (textValue is not null)
        {
            record.StringFields[TextFieldIndex] = record.StringFields[TextFieldIndex]
                with { Value = textValue };
            edits.Add(new WriteSideEdits.StringFieldEdit(TextFieldIndex, textValue));
            entity.TextValue = textValue;
        }
        if (tag is not null)
        {
            record.StringFields[TagFieldIndex] = record.StringFields[TagFieldIndex]
                with { Value = tag };
            edits.Add(new WriteSideEdits.StringFieldEdit(TagFieldIndex, tag));
            entity.Tag = tag;
        }
        WriteSideEdits.MarkAttributeEdited(handle, edits);
        return null;
    }

    public static List<(ulong Handle, string Tag, string TextValue)> ListAttributes(IDwgFile file)
    {
        ArgumentNullException.ThrowIfNull(file);
        var list = new List<(ulong, string, string)>();
        foreach (var e in file.GetObjects().Entities)
            if (e.Kind == DwgEntityType.Attrib)
                list.Add((e.Handle, e.Tag, e.TextValue));
        return list;
    }

    public static IReadOnlyCollection<ulong> UnappliedEdits => WriteSideEdits.UnappliedAttributeEdits;

    public static void ClearEdits() => WriteSideEdits.ClearAttributeEdits();
}
