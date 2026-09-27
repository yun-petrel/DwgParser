namespace DwgParser.Core.Objects.Records;

public sealed class ViewStyleCapture : RecordIdentity, IViewStyle, IViewStyleTemplate
{
    public readonly record struct Slot(long Start, long End, string Value);

    public readonly List<Slot> Slots = new();

    public readonly record struct NumField(string Tag, string Kind, long Raw, string Text);

    public readonly List<NumField> Nums = new();

    public long HandleStreamStartBit;

    public bool Captured;


    public bool ValuesCommitted;

    public ViewStyleValues? FieldValues;

    public readonly List<string> ExtraStrings = new();

    public string FailReason = "";
    public long FailBit = -1;


    public bool TemplateCaptured;

    public long TemplateHeadEndBit;

    public string TemplateDesc = "";
    public string TemplateExclude = "";
    public string TemplatePattern = "";
    public string TemplateHatchPattern = "";

    public IReadOnlyList<string> Values => _values ??= BuildValues();
    private string[]? _values;
    private string[] BuildValues()
    {
        var a = new string[Slots.Count];
        for (int i = 0; i < Slots.Count; i++) a[i] = Slots[i].Value;
        return a;
    }

    System.Collections.Generic.IReadOnlyList<string> IViewStyle.InlineStrings => Values;
    bool IViewStyle.Captured => Captured;
    bool IViewStyleTemplate.TemplateCaptured => TemplateCaptured;
}
