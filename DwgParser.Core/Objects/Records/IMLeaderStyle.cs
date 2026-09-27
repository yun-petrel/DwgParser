namespace DwgParser.Core.Objects.Records;

public interface IMLeaderStyle : IRecord
{
    bool Captured { get; }

    bool DownCaptured { get; }

    string Description { get; }

    string TextDefault { get; }

    MLeaderStyleCapture.MLeaderStyleValues? Values { get; }
}
