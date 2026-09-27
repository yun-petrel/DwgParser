namespace DwgParser.Core.Objects.Records;

public interface IViewStyle : IRecord
{
    System.Collections.Generic.IReadOnlyList<string> InlineStrings { get; }

    bool Captured { get; }
}
