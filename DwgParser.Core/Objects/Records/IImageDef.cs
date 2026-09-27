namespace DwgParser.Core.Objects.Records;

public interface IImageDef : IRecord
{
    string FilePath { get; }

    int ClassVersion { get; }

    bool Captured { get; }
}
