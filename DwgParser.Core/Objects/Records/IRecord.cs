namespace DwgParser.Core.Objects.Records;

public interface IRecord
{
    ulong Handle { get; }

    short TypeCode { get; }

    DwgVersion SourceVersion { get; }
}
