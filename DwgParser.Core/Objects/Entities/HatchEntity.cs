using DwgParser.Core.IO;
using DwgParser.Core.Render;

namespace DwgParser.Core.Objects;

public sealed class HatchEntity : DwgEntity, IHatch
{
    protected override string WriteSvg(RenderContext ctx, Matrix m)
    {
        string c = ctx.EntityColor(this);
        var rings = HatchGeometry.BoundaryRings(this);
        if (rings.Count == 0) return RenderHatch(ctx, m, c, c);

        var sb = new System.Text.StringBuilder();
        foreach (var ring in rings)
        {
            var d = new System.Text.StringBuilder();
            for (int i = 0; i < ring.Length; i++)
            {
                var (x, y) = m.Apply(ring[i].X, ring[i].Y);
                ctx.Expand(x, y);
                d.Append(i == 0 ? $"M {RenderContext.F(x)} {RenderContext.F(y)}"
                                : $" L {RenderContext.F(x)} {RenderContext.F(y)}");
            }
            d.Append(" Z");
            string fill = SolidFill
                ? $" fill=\"{c}\" fill-opacity=\"{RenderContext.F(HatchGeometry.SolidFillOpacity)}\""
                : " fill=\"none\"";
            sb.Append($"    <path d=\"{d}\" stroke=\"{c}\" stroke-width=\"1\"{fill}/>\n");
        }

        var segs = HatchGeometry.PatternSegments(this);
        if (segs.Count > 0)
        {
            var pd = new System.Text.StringBuilder();
            foreach (var (a, b) in segs)
            {
                var (x1, y1) = m.Apply(a.X, a.Y);
                var (x2, y2) = m.Apply(b.X, b.Y);
                ctx.Expand(x1, y1); ctx.Expand(x2, y2);
                pd.Append($" M {RenderContext.F(x1)} {RenderContext.F(y1)} L {RenderContext.F(x2)} {RenderContext.F(y2)}");
            }
            sb.Append($"    <path d=\"{pd}\" stroke=\"{c}\" stroke-width=\"1\" vector-effect=\"non-scaling-stroke\" fill=\"none\"/>\n");
        }
        return sb.ToString();
    }

    public bool IsSolidFill => SolidFill;

    public HatchEntity TransformedClone(Matrix m)
    {
        double avg = (Math.Sqrt(m.M11 * m.M11 + m.M12 * m.M12)
                    + Math.Sqrt(m.M21 * m.M21 + m.M22 * m.M22)) / 2;
        double rot = Math.Atan2(m.M12, m.M11);
        bool mirrored = m.M11 * m.M22 - m.M12 * m.M21 < 0;

        Vec2 P(Vec2 v) { var (x, y) = m.Apply(v.X, v.Y); return new Vec2(x, y); }
        Vec2 D(Vec2 v) => new(m.M11 * v.X + m.M21 * v.Y, m.M12 * v.X + m.M22 * v.Y);

        var t = new HatchEntity
        {
            Kind = DwgEntityType.Hatch,
            Handle = Handle,
            ColorIndex = ColorIndex,
            LayerHandle = LayerHandle,
            OwnerHandle = OwnerHandle,
            EntMode = EntMode,
            LtypeName = LtypeName,
            TrueColor = TrueColor,
            HasTrueColor = HasTrueColor,
            ColorAlpha = ColorAlpha,
            HasColorAlpha = HasColorAlpha,
            ColorName = ColorName,
            ColorBookName = ColorBookName,
            ExtrusionZ = ExtrusionZ,
            Extrusion = Extrusion,
            Thickness = Thickness,
            Elevation = Elevation,
            SolidFill = SolidFill,
            Closed = Closed,
            ConstantWidth = ConstantWidth,
            Style = Style,
            PatternType = PatternType,
            PatternName = PatternName,
            HasGradientBlock = HasGradientBlock,
            HasSplineFitpoints = HasSplineFitpoints,
            PatternAngle = PatternAngle is { } pa ? pa + rot : null,
            PatternScale = PatternScale is { } ps ? ps * avg : null,
        };

        foreach (var pl in PatternLines) t.PatternLines.Add(pl);
        foreach (var gc in GradientColors) t.GradientColors.Add(gc);
        foreach (var sp in SeedPoints) t.SeedPoints.Add(P(sp));

        foreach (var seg in Segments)
        {
            var poly = new Vec2[seg.PolylinePoints.Length];
            for (int i = 0; i < poly.Length; i++) poly[i] = P(seg.PolylinePoints[i]);

            double[] bulges = seg.Bulges;
            if (mirrored && bulges.Length > 0)
            {
                bulges = new double[seg.Bulges.Length];
                for (int i = 0; i < bulges.Length; i++) bulges[i] = -seg.Bulges[i];
            }

            var cps = new (Vec2 Point, double Weight)[seg.ControlPoints.Length];
            for (int i = 0; i < cps.Length; i++)
                cps[i] = (P(seg.ControlPoints[i].Point), seg.ControlPoints[i].Weight);

            var fits = new Vec2[seg.FitPoints.Length];
            for (int i = 0; i < fits.Length; i++) fits[i] = P(seg.FitPoints[i]);

            t.Segments.Add(new HatchBoundarySegment
            {
                CurveType = seg.CurveType,
                IsPolylinePath = seg.IsPolylinePath,
                P1 = P(seg.P1),
                P2 = P(seg.P2),
                Center = P(seg.Center),
                Radius = seg.Radius * avg,
                MajorAxis = D(seg.MajorAxis),
                Ratio = seg.Ratio,
                StartAngle = seg.StartAngle + rot,
                EndAngle = seg.EndAngle + rot,
                IsCcw = seg.IsCcw,
                Degree = seg.Degree,
                Rational = seg.Rational,
                Periodic = seg.Periodic,
                Knots = seg.Knots,
                ControlPoints = cps,
                FitPoints = fits,
                StartTangent = D(seg.StartTangent),
                EndTangent = D(seg.EndTangent),
                HasFitPoints = seg.HasFitPoints,
                PolylinePoints = poly,
                BulgesPresent = seg.BulgesPresent,
                Bulges = bulges,
                IsClosed = seg.IsClosed,
                BoundaryHandleCount = seg.BoundaryHandleCount,
            });
        }

        foreach (var (start, count) in PathSegmentRanges) t.PathSegmentRanges.Add((start, count));
        foreach (var f in PathFlags) t.PathFlags.Add(f);
        foreach (var c in PathBoundaryHandleCounts) t.PathBoundaryHandleCounts.Add(c);

        foreach (var p in Points) t.Points.Add(P(p));
        foreach (var b in Bulges) t.Bulges.Add(mirrored ? -b : b);
        return t;
    }

    internal bool HasGradientBlock { get; set; }

    internal bool HasSplineFitpoints { get; set; }


    public bool IsGradientFill { get; private set; }

    public double GradientAngle { get; private set; }

    public double GradientShift { get; private set; }

    public bool GradientSingleColor { get; private set; }

    public double GradientTint { get; private set; }

    public long GradientColorCount { get; private set; }

    public System.Collections.Generic.List<(double Shift, short Index, uint Rgb, string Name, string BookName)> GradientColors { get; } = new();

    public bool IsAssociative { get; private set; }

    public System.Collections.Generic.List<long> PathFlags { get; } = new();

    public System.Collections.Generic.List<bool> ArcCcwFlags { get; } = new();

    public System.Collections.Generic.List<bool> EllipseCcwFlags { get; } = new();

    public System.Collections.Generic.List<(int Degree, bool Rational, bool Periodic)> Splines { get; } = new();


    public Vec3 Extrusion { get; private set; }

    public string PatternName { get; private set; } = "";

    public System.Collections.Generic.List<HatchBoundarySegment> Segments { get; } = new();

    public System.Collections.Generic.List<long> PathBoundaryHandleCounts { get; } = new();

    public long BoundaryHandleTotal { get; private set; }

    public long PathCount { get; private set; }

    public System.Collections.Generic.List<bool> PolylineBulgesPresent { get; } = new();

    internal System.Collections.Generic.List<(int Start, int Count)> PathSegmentRanges { get; } = new();


    public long Style { get; private set; }

    public long PatternType { get; private set; }

    public double? PatternAngle { get; private set; }

    public double? PatternScale { get; private set; }

    public bool? PatternDouble { get; private set; }

    public System.Collections.Generic.List<(double Angle, Vec2 Base, Vec2 Offset,
        System.Collections.Generic.List<double> Dashes)> PatternLines { get; } = new();

    public double? PixelSize { get; private set; }

    public System.Collections.Generic.List<Vec2> SeedPoints { get; } = new();

    public void ReadBody(BitReader br, DwgObjectRecord? record)
    {
        if (HasGradientBlock)
        {
            IsGradientFill = br.ReadBL() != 0;
            br.ReadBL();
            GradientAngle = br.ReadBD();
            GradientShift = br.ReadBD();
            GradientSingleColor = br.ReadBL() != 0;
            GradientTint = br.ReadBD();
            long numColors = br.ReadBL();
            if (numColors is < 0 or > 1000)
                throw new EndOfStreamException();
            GradientColorCount = numColors;
            for (int i = 0; i < numColors; i++)
            {
                double shift = br.ReadBD();
                short cIdx = DwgObjectReader.ReadCmc(br, out uint cRgb, out string cName, out string cBook);
                GradientColors.Add((shift, cIdx, cRgb, cName, cBook));
            }
            long gradStart = br.BitPosition;
            string gradientName = br.ReadTV();
            record?.CaptureString(gradStart, br.BitPosition, gradientName);
            if (record != null) record.HatchGradientEndBit = br.BitPosition;
            if (record != null) record.HatchGradientName = gradientName;
        }
        Elevation = br.ReadBD();
        var ext = br.Read3BD();
        Extrusion = new Vec3(ext.X, ext.Y, ext.Z);
        ExtrusionZ = ext.Z;
        long patStart = br.BitPosition;
        string pattern = br.ReadTV();
        PatternName = pattern;
        record?.CaptureString(patStart, br.BitPosition, pattern);
        if (record != null) { record.HatchPatternSlotBit = patStart; record.HatchPatternName = pattern; }
        if (System.Environment.GetEnvironmentVariable("DWGDBG") == "1" && record != null)
            System.Console.Error.WriteLine(
                $"DWGDBG HATCHPAT h={record.Handle:X} slot={patStart} end={br.BitPosition} " +
                $"len={pattern.Length} \"{pattern}\" gradEnd={record.HatchGradientEndBit} " +
                $"gradName=\"{record.HatchGradientName}\" " +
                $"ssStart={record.StringStreamStartBit} geoStart={record.GeoStartBit} " +
                $"geoLen={record.GeoBitLen} hsStart={record.HandleStreamStartBit}");
        bool solidFill = br.ReadB();
        IsAssociative = br.ReadB();
        long numPaths = br.ReadBL();
        if (numPaths is < 0 or > 1000) throw new EndOfStreamException();
        PathCount = numPaths;

        Points.Clear();
        Segments.Clear();
        PatternLines.Clear();
        SeedPoints.Clear();
        PathBoundaryHandleCounts.Clear();
        PathSegmentRanges.Clear();
        PolylineBulgesPresent.Clear();
        SolidFill = solidFill;
        Kind = DwgEntityType.Hatch;

        long boundaryTotal = 0;
        bool hasDerived = false;
        for (int p = 0; p < numPaths; p++)
        {
            long flag = br.ReadBL();
            PathFlags.Add(flag);
            hasDerived |= (flag & 0x4) != 0;
            int pathStart = Segments.Count;
            long pathBoundary;
            if ((flag & 2) == 0)
            {
                int pathSegStart = Segments.Count;
                long numSegs = br.ReadBL();
                if (numSegs is < 0 or > 100000) throw new EndOfStreamException();
                for (int s = 0; s < numSegs; s++)
                {
                    long curveType = br.ReadRC();
                    switch (curveType)
                    {
                        case 1:
                        {
                            var p1 = br.Read2RD();
                            var p2 = br.Read2RD();
                            Points.Add(new Vec2(p1.X, p1.Y));
                            Points.Add(new Vec2(p2.X, p2.Y));
                            Segments.Add(new HatchBoundarySegment
                            {
                                CurveType = 1, P1 = new Vec2(p1.X, p1.Y), P2 = new Vec2(p2.X, p2.Y),
                            });
                            break;
                        }
                        case 2:
                        {
                            var c = br.Read2RD();
                            double r = br.ReadBD();
                            double a1 = br.ReadBD();
                            double a2 = br.ReadBD();
                            bool ccw = br.ReadB();
                            ArcCcwFlags.Add(ccw);
                            Segments.Add(new HatchBoundarySegment
                            {
                                CurveType = 2, Center = new Vec2(c.X, c.Y), Radius = r,
                                StartAngle = a1, EndAngle = a2, IsCcw = ccw,
                            });
                            int n = Math.Max(4, (int)Math.Abs((a2 - a1) / 0.2));
                            for (int k = 0; k <= n; k++)
                            {
                                double a = a1 + (a2 - a1) * k / n;
                                Points.Add(new Vec2(c.X + r * Math.Cos(a), c.Y + r * Math.Sin(a)));
                            }
                            break;
                        }
                        case 3:
                        {
                            var c = br.Read2RD();
                            var end = br.Read2RD();
                            double ratio = br.ReadBD();
                            double a1 = br.ReadBD();
                            double a2 = br.ReadBD();
                            bool ccw = br.ReadB();
                            EllipseCcwFlags.Add(ccw);
                            Segments.Add(new HatchBoundarySegment
                            {
                                CurveType = 3, Center = new Vec2(c.X, c.Y),
                                MajorAxis = new Vec2(end.X, end.Y), Ratio = ratio,
                                StartAngle = a1, EndAngle = a2, IsCcw = ccw,
                            });
                            double theta = Math.Atan2(end.Y, end.X);
                            double a = Math.Sqrt(end.X * end.X + end.Y * end.Y);
                            double b = a * ratio;
                            double cosT = Math.Cos(theta), sinT = Math.Sin(theta);
                            int n = Math.Max(4, (int)Math.Abs((a2 - a1) / 0.2));
                            for (int k = 0; k <= n; k++)
                            {
                                double t = a1 + (a2 - a1) * k / n;
                                double ct = Math.Cos(t), st = Math.Sin(t);
                                Points.Add(new Vec2(c.X + a * ct * cosT - b * st * sinT,
                                                      c.Y + a * ct * sinT + b * st * cosT));
                            }
                            break;
                        }
                        case 4:
                        {
                            int degree = (int)br.ReadBL();
                            bool rational = br.ReadB();
                            bool periodic = br.ReadB();
                            Splines.Add((degree, rational, periodic));
                            long numKnots = br.ReadBL();
                            long numCtrl = br.ReadBL();
                            if (numKnots is < 0 or > 100000 || numCtrl is < 0 or > 100000)
                                throw new EndOfStreamException();
                            var knots = new double[numKnots];
                            for (int k = 0; k < numKnots; k++) knots[k] = br.ReadBD();
                            var ctrl = new (Vec2 Point, double Weight)[numCtrl];
                            for (int k = 0; k < numCtrl; k++)
                            {
                                var pt = br.Read2RD();
                                double weight = rational ? br.ReadBD() : 0.0;
                                Points.Add(new Vec2(pt.X, pt.Y));
                                ctrl[k] = (new Vec2(pt.X, pt.Y), weight);
                            }
                            var fitPts = System.Array.Empty<Vec2>();
                            bool hasFit = false;
                            var startTan = default(Vec2);
                            var endTan = default(Vec2);
                            if (HasSplineFitpoints)
                            {
                                long r2010From = br.BitPosition;
                                long numFit = br.ReadBL();
                                if (numFit is < 0 or > 100000)
                                    throw new EndOfStreamException();
                                if (numFit > 0)
                                {
                                    hasFit = true;
                                    fitPts = new Vec2[numFit];
                                    for (long k = 0; k < numFit; k++)
                                    {
                                        var fp = br.Read2RD();
                                        fitPts[k] = new Vec2(fp.X, fp.Y);
                                    }
                                    var st = br.Read2RD();
                                    var et = br.Read2RD();
                                    startTan = new Vec2(st.X, st.Y);
                                    endTan = new Vec2(et.X, et.Y);
                                }
                                if (record != null)
                                    (record.HatchR2010SplineSpans ??=
                                        new System.Collections.Generic.List<(long, int)>())
                                        .Add((r2010From, (int)(br.BitPosition - r2010From)));
                            }
                            Segments.Add(new HatchBoundarySegment
                            {
                                CurveType = 4, Degree = degree, Rational = rational, Periodic = periodic,
                                Knots = knots, ControlPoints = ctrl,
                                FitPoints = fitPts, HasFitPoints = hasFit,
                                StartTangent = startTan, EndTangent = endTan,
                            });
                            break;
                        }
                    }
                }
                pathBoundary = br.ReadBL();
                if (pathBoundary is < 0 or > 10000)
                    throw new EndOfStreamException();
                if (Segments.Count > pathSegStart)
                    Segments[pathSegStart].BoundaryHandleCount = pathBoundary;
            }
            else
            {
                bool bulgesPresent = br.ReadB();
                bool closed = br.ReadB();
                long numPts = br.ReadBL();
                if (numPts is < 0 or > 100000) throw new EndOfStreamException();
                PolylineBulgesPresent.Add(bulgesPresent);
                var poly = new Vec2[numPts];
                var bulges = new double[numPts];
                for (int k = 0; k < numPts; k++)
                {
                    var pt = br.Read2RD();
                    poly[k] = new Vec2(pt.X, pt.Y);
                    Points.Add(new Vec2(pt.X, pt.Y));
                    if (bulgesPresent) bulges[k] = br.ReadBD();
                }
                if (closed && Points.Count > 0)
                    Points.Add(Points[0]);
                pathBoundary = ReadPolylinePathBoundaryCount(br);
                Segments.Add(new HatchBoundarySegment
                {
                    IsPolylinePath = true, PolylinePoints = poly, BulgesPresent = bulgesPresent,
                    Bulges = bulges, IsClosed = closed, BoundaryHandleCount = pathBoundary,
                });
            }
            PathSegmentRanges.Add((pathStart, Segments.Count - pathStart));
            PathBoundaryHandleCounts.Add(pathBoundary);
            boundaryTotal += pathBoundary;
        }
        BoundaryHandleTotal = boundaryTotal;
        if (record != null) record.HatchBoundaryHandleTotal = boundaryTotal;

        Style = br.ReadBS();
        PatternType = br.ReadBS();
        if (!solidFill)
        {
            PatternAngle = br.ReadBD();
            PatternScale = br.ReadBD();
            PatternDouble = br.ReadB();
            long numDefLines = br.ReadBS();
            if (numDefLines is < 0 or > 1000) throw new EndOfStreamException();
            for (int i = 0; i < numDefLines; i++)
            {
                double lineAngle = br.ReadBD();
                double baseX = br.ReadBD();
                double baseY = br.ReadBD();
                double offX = br.ReadBD();
                double offY = br.ReadBD();
                long numDashes = br.ReadBS();
                if (numDashes is < 0 or > 1000) throw new EndOfStreamException();
                var dashes = new System.Collections.Generic.List<double>((int)numDashes);
                for (int d = 0; d < numDashes; d++)
                    dashes.Add(br.ReadBD());
                PatternLines.Add((lineAngle, new Vec2(baseX, baseY), new Vec2(offX, offY), dashes));
            }
        }
        if (hasDerived)
            PixelSize = br.ReadBD();
        long numSeeds = br.ReadBL();
        if (numSeeds is < 0 or > 10000) throw new EndOfStreamException();
        for (int i = 0; i < numSeeds; i++)
        {
            var seed = br.Read2RD();
            SeedPoints.Add(new Vec2(seed.X, seed.Y));
        }
        if (Points.Count > 0)
            Start = Points[0];
    }

    private static long ReadPolylinePathBoundaryCount(BitReader br)
    {
        long n = br.ReadBL();
        if (n is < 0 or > 10000) throw new EndOfStreamException();
        return n;
    }
}
