namespace DwgParser.Core.Objects.Records;

public abstract class RecordIdentity
{
    public ulong Handle { get; internal set; }

    public short TypeCode { get; internal set; }

    public DwgVersion SourceVersion { get; internal set; }

    internal void AttachIdentity(DwgObjectRecord rec)
    {
        Handle = rec.Handle;
        TypeCode = rec.LogicalType;
        SourceVersion = rec.SourceVersion;
    }
}
