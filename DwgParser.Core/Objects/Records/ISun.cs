namespace DwgParser.Core.Objects.Records;

public interface ISun : IRecord
{
    long ClassVersion { get; }

    bool IsOn { get; }

    int ColorIndex { get; }

    uint ColorRgb { get; }

    string ColorName { get; }

    string ColorBookName { get; }

    double Intensity { get; }

    bool HasShadow { get; }

    long JulianDay { get; }

    long Msecs { get; }

    bool IsDayLightSavings { get; }

    long ShadowType { get; }

    long ShadowMapSize { get; }

    int ShadowSoftness { get; }

    bool StringStreamPresent { get; }

    bool Captured { get; }
}
