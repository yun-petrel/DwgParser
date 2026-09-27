namespace DwgParser.Core.Objects.Records;

public interface IAppid : IRecord
{
    string Name { get; }

    int XrefResolved { get; }

    int Unknown71 { get; }

    bool HasUnknown71 { get; }

    bool Captured { get; }
}
