namespace DwgParser.Core.Objects.Records;

public interface IUnderlayDefinition : IRecord
{
    string Filename { get; }

    string Name { get; }

    string UnderlayKind { get; }

    bool Captured { get; }
}
