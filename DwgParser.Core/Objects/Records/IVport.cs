namespace DwgParser.Core.Objects.Records;

public interface IVport : IRecord
{
    bool Captured { get; }

    bool ValuesCommitted { get; }

    long LightsAnchorBit { get; }

    long XrefStartBit { get; }

    int XrefResolved { get; }

    ulong VisualStyleHandle { get; }

    string Name { get; }

    VportValues? Values { get; }
}
