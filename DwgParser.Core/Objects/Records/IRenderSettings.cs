namespace DwgParser.Core.Objects.Records;

public enum RenderSettingKind { Int32, Int16, Real, Flag, Text }

public readonly record struct RenderSettingField(
    string Label, RenderSettingKind Kind, long IntValue, double RealValue, string TextValue, int BitLen)
{
    public override string ToString() => Kind switch
    {
        RenderSettingKind.Text => $"{Label}='{TextValue}'",
        RenderSettingKind.Real => $"{Label}={RealValue:R}",
        _ => $"{Label}={IntValue}",
    };
}

public interface IRenderSettings : IRecord
{
    string SettingsKind { get; }

    long ClassVersion { get; }

    string Name { get; }

    bool FogEnabled { get; }

    bool FogBackgroundEnabled { get; }

    bool BackfacesEnabled { get; }

    bool EnvironImageEnabled { get; }

    string EnvironImageFilename { get; }

    string Description { get; }

    long DisplayIndex { get; }

    IReadOnlyList<RenderSettingField> Fields { get; }

    bool Captured { get; }
}
