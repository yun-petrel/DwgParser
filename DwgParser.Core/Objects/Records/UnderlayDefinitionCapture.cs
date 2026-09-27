namespace DwgParser.Core.Objects.Records;

public sealed class UnderlayDefinitionCapture : RecordIdentity, IUnderlayDefinition
{
    public string Filename = "";

    public string Name = "";

    public string UnderlayKind = "";

    public bool Captured;

    public long FilenameStartBit;

    public int FilenameBitLen;

    public long WalkEndBit;

    public string FailReason = "";

    string IUnderlayDefinition.Filename => Filename;
    string IUnderlayDefinition.Name => Name;
    string IUnderlayDefinition.UnderlayKind => UnderlayKind;
    bool IUnderlayDefinition.Captured => Captured;
}
