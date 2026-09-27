using DwgParser.Core.IO;

namespace DwgParser.Core.Objects.Records;

public abstract class ViewStyleCarryPlan
{
    public abstract string Name { get; }

    public abstract bool IsCaptured(DwgObjectRecord rec);

    internal abstract bool TryCarry(BitWriter body, DwgObjectRecord rec, byte[] src,
        DwgObjectStreamWriter.Target t, out BitWriter? handles);
}
