namespace DwgParser.Core.Objects.Records;

public interface IVisualStyle : IRecord
{
    bool Captured { get; }

    int StyleType { get; }

    string Description { get; }

    int EdgeHaloGap { get; }

    double DisplayBrightness { get; }

    bool InternalOnly { get; }

    long[] FieldSpans { get; }

    int[] Scalars { get; }
}
