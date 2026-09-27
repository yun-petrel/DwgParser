using DwgParser.Core.IO;

namespace DwgParser.Core.Objects.Records;

public abstract class MaterialCarryPlan
{
    public abstract string Name { get; }

    public abstract bool IsCaptured(DwgObjectRecord rec);

    internal abstract void WriteUpgradeTail(BitWriter body, DwgObjectRecord rec,
        DwgObjectStreamWriter.Target t);
}
