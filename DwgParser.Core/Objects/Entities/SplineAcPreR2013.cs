using DwgParser.Core.IO;

namespace DwgParser.Core.Objects;

internal sealed class SplineAcPreR2013 : SplineEntity
{
    public override void ReadGeometry(BitReader br)
    {
        Scenario = br.ReadBL();
        Degree = br.ReadBL();
        Points.Clear();
        IsNurbsBranch = (Scenario & 1) != 0;

        if (IsNurbsBranch)
            ReadNurbsBranch(br);
        else
            ReadBezierBranch(br);
        Finish();
    }
}
