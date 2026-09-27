using DwgParser.Core.IO;

namespace DwgParser.Core.Objects;

internal sealed class SplineAcR2013Plus : SplineEntity
{
    public override void ReadGeometry(BitReader br)
    {
        Scenario = br.ReadBL();
        Flags1 = br.ReadBL();
        KnotParam = br.ReadBL();
        Degree = br.ReadBL();
        Points.Clear();

        long scenario = Scenario;
        if ((Flags1 & 1) != 0) scenario = 2;
        if (KnotParam == 15) scenario = 1;
        IsNurbsBranch = (scenario & 1) != 0;

        if (IsNurbsBranch)
            ReadNurbsBranch(br);
        else
            ReadBezierBranch(br);
        Finish();
    }
}
