namespace DwgParser.Core.Objects.Records;

public interface ILayout : IRecord
{
    string LayoutName { get; }

    bool Captured { get; }
}
