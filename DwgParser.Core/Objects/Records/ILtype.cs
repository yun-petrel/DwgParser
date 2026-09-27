namespace DwgParser.Core.Objects.Records;

public interface ILtype : IRecord
{
    string Name { get; }

    string Description { get; }

    int XrefResolved { get; }

    int NumDashes { get; }

    bool Captured { get; }

    double PatternLen { get; }

    int Alignment { get; }

    System.Collections.Generic.List<LtypeRecord.LtypeDash> Dashes { get; }
}
