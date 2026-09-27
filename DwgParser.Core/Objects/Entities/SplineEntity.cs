using DwgParser.Core.IO;
using DwgParser.Core.Render;

namespace DwgParser.Core.Objects;

public abstract class SplineEntity : DwgEntity, ISpline
{

    public long Scenario { get; protected set; }

    public long Degree { get; protected set; }

    public long Flags1 { get; protected set; }

    public long KnotParam { get; protected set; }

    public bool Rational { get; protected set; }

    public new bool Closed { get; protected set; }

    public bool Periodic { get; protected set; }

    public double KnotTol { get; protected set; }

    public double CtrlTol { get; protected set; }

    public List<double> Knots { get; } = new();

    public List<double> Weights { get; } = new();

    public int KnotCount => Knots.Count;

    public double FitTol { get; protected set; }

    public (double X, double Y, double Z) BeginTangent { get; protected set; }

    public (double X, double Y, double Z) EndTangent { get; protected set; }

    public List<(double X, double Y, double Z)> FitPoints { get; } = new();

    public bool IsNurbsBranch { get; protected set; }

    public abstract void ReadGeometry(BitReader br);

    protected void ReadNurbsBranch(BitReader br)
    {
        Rational = br.ReadB();
        Closed = br.ReadB();
        base.Closed = Closed;
        Periodic = br.ReadB();
        KnotTol = br.ReadBD();
        CtrlTol = br.ReadBD();
        long numKnots = br.ReadBL();
        long numCtrl = br.ReadBL();
        bool weighted = br.ReadB();
        if (numKnots is < 0 or > 100000 || numCtrl is < 0 or > 100000)
            throw new System.IO.EndOfStreamException();
        Knots.EnsureCapacity((int)numKnots);
        Weights.EnsureCapacity(weighted ? (int)numCtrl : 0);
        for (int i = 0; i < numKnots; i++) Knots.Add(br.ReadBD());
        for (int i = 0; i < numCtrl; i++)
        {
            var p = br.Read3BD();
            Points.Add(new Vec2(p.X, p.Y));
            if (weighted) Weights.Add(br.ReadBD());
        }
    }

    protected void ReadBezierBranch(BitReader br)
    {
        FitTol = br.ReadBD();
        BeginTangent = br.Read3BD();
        EndTangent = br.Read3BD();
        long numFit = br.ReadBL();
        if (numFit is < 0 or > 100000) throw new System.IO.EndOfStreamException();
        FitPoints.EnsureCapacity((int)numFit);
        for (int i = 0; i < numFit; i++)
        {
            var p = br.Read3BD();
            FitPoints.Add(p);
            Points.Add(new Vec2(p.X, p.Y));
        }
    }

    protected void Finish()
    {
        if (Points.Count > 0)
            Start = Points[0];
        Kind = DwgEntityType.Spline;
    }
}
