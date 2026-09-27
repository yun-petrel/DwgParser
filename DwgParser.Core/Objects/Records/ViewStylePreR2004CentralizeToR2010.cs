using DwgParser.Core.IO;
using static DwgParser.Core.Objects.DwgObjectStreamWriter;

namespace DwgParser.Core.Objects.Records;

internal sealed class ViewStylePreR2004CentralizeToR2010 : ViewStyleCarryPlan
{
    public override string Name => "VSTY:AC1015源→R2010+目标(短CMC升宽+串集中化)";

    public override bool IsCaptured(DwgObjectRecord rec)
        => rec.SourceVersion == DwgVersion.AC1015 && rec.HasHandleStream
           && rec.LogicalType is (510 or 511);

    internal override bool TryCarry(BitWriter body, DwgObjectRecord rec, byte[] src,
        DwgObjectStreamWriter.Target t, out BitWriter? handles)
        => TryCarryViewStylePreR2004Centralized(body, src, rec, t, out handles);
}
