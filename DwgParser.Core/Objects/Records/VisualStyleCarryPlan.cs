using DwgParser.Core.IO;

namespace DwgParser.Core.Objects.Records;

public abstract class VisualStyleCarryPlan
{
    public abstract string Name { get; }

    public abstract bool IsCaptured(DwgObjectRecord rec);

    internal abstract bool TryCarry(BitWriter body, byte[] src, long from, long to,
        DwgObjectRecord rec, DwgObjectStreamWriter.Target t);
}
