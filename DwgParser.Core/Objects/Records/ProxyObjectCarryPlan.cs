namespace DwgParser.Core.Objects.Records;

public abstract class ProxyObjectCarryPlan
{
    public abstract string Name { get; }

    public abstract bool IsCaptured(DwgObjectRecord rec);

    internal abstract bool TryCarry(DwgParser.Core.IO.BitWriter body, byte[] src, long from, long to,
        DwgObjectRecord rec, DwgObjectStreamWriter.Target t);
}
