namespace DwgParser.Core.Objects.Records;

public sealed class RenderSettingsCapture : RecordIdentity, IRenderSettings
{
    public string SettingsKind = "";

    public long ClassVersion = -1;

    public string Name = "";

    public bool FogEnabled;

    public bool FogBackgroundEnabled;

    public bool BackfacesEnabled;

    public bool EnvironImageEnabled;

    public string EnvironImageFilename = "";

    public string Description = "";

    public long DisplayIndex = -1;

    public List<RenderSettingField> Fields = new();

    public bool Captured;

    public long WalkEndBit;

    public string FailReason = "";

    string IRenderSettings.SettingsKind => SettingsKind;
    long IRenderSettings.ClassVersion => ClassVersion;
    string IRenderSettings.Name => Name;
    bool IRenderSettings.FogEnabled => FogEnabled;
    bool IRenderSettings.FogBackgroundEnabled => FogBackgroundEnabled;
    bool IRenderSettings.BackfacesEnabled => BackfacesEnabled;
    bool IRenderSettings.EnvironImageEnabled => EnvironImageEnabled;
    string IRenderSettings.EnvironImageFilename => EnvironImageFilename;
    string IRenderSettings.Description => Description;
    long IRenderSettings.DisplayIndex => DisplayIndex;
    IReadOnlyList<RenderSettingField> IRenderSettings.Fields => Fields;
    bool IRenderSettings.Captured => Captured;
}
