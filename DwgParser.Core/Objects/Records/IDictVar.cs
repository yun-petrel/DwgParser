namespace DwgParser.Core.Objects.Records;

public interface IDictVar : IRecord
{
    string StrValue { get; }

    bool Captured { get; }

    int Schema { get; }
}
