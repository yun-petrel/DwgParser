namespace DwgParser.Core.Objects.Records;

public interface IRasterVariables : IRecord
{
    long ClassVersion { get; }

    long ImageFrame { get; }

    long ImageQuality { get; }

    long Units { get; }

    bool StringStreamPresent { get; }

    bool Captured { get; }
}
