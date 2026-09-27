namespace DwgParser.Core.Objects.Records;

public interface ITableStyle : IRecord
{
    bool Captured { get; }

    string Name { get; }

    double HorizMargin { get; }

    double VertMargin { get; }
}
