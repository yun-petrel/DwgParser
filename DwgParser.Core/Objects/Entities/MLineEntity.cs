using DwgParser.Core;
using DwgParser.Core.IO;

namespace DwgParser.Core.Objects;

public sealed class MLineEntity : DwgEntity, IMLine
{
    public double Scale { get; private set; } = 1.0;
    public int Justification { get; private set; } = 1;
    public int Flags { get; private set; }
    public int LineCount { get; private set; }
    public int VertexCount => Vertices.Count;
    public List<MLineVertex> Vertices { get; } = new();
    System.Collections.Generic.IReadOnlyList<MLineVertex> IMLine.Vertices => Vertices;

    public ulong MLineStyleHandle { get; internal set; }

    public void ReadBody(BitReader br, DwgObjectRecord? record, DwgVersion version, ulong handle, int objStart)
    {
        double scale = br.ReadBD();
        int just = br.ReadRC();
        var basePt = br.Read3BD();
        var ext = br.Read3BD();
        int flags = br.ReadBS();
        int nlines = br.ReadRC();
        int nverts = br.ReadBS();
        if (!Finite(scale, basePt.X, basePt.Y, basePt.Z, ext.X, ext.Y, ext.Z))
            throw new EndOfStreamException();
        if (nlines is < 1 or > 64 || nverts is < 0 or > 20000)
            throw new EndOfStreamException();

        var verts = new List<MLineVertex>(nverts);
        for (int vi = 0; vi < nverts; vi++)
        {
            var p = br.Read3BD();
            var d = br.Read3BD();
            var m = br.Read3BD();
            if (!Finite(p.X, p.Y, p.Z, d.X, d.Y, d.Z, m.X, m.Y, m.Z))
                throw new EndOfStreamException();
            var lines = new MLineLine[nlines];
            for (int li = 0; li < nlines; li++)
            {
                int ns = br.ReadBS();
                if (ns is < 0 or > 20000) throw new EndOfStreamException();
                var seg = new double[ns];
                for (int i = 0; i < ns; i++)
                {
                    seg[i] = br.ReadBD();
                    if (!double.IsFinite(seg[i])) throw new EndOfStreamException();
                }
                int na = br.ReadBS();
                if (na is < 0 or > 20000) throw new EndOfStreamException();
                var area = new double[na];
                for (int i = 0; i < na; i++)
                {
                    area[i] = br.ReadBD();
                    if (!double.IsFinite(area[i])) throw new EndOfStreamException();
                }
                lines[li] = new MLineLine { SegmentParams = seg, AreaFillParams = area };
            }
            verts.Add(new MLineVertex
            {
                X = p.X, Y = p.Y, Z = p.Z,
                DirX = d.X, DirY = d.Y, DirZ = d.Z,
                MiterX = m.X, MiterY = m.Y, MiterZ = m.Z,
                Lines = lines,
            });
        }

        Scale = scale; Justification = just; Flags = flags; LineCount = nlines;
        Vertices.Clear(); Vertices.AddRange(verts);
        Start = new Vec2(basePt.X, basePt.Y);
        Elevation = basePt.Z;
        ExtrusionZ = ext.Z;
        Points.Clear();
        foreach (var v in verts) Points.Add(new Vec2(v.X, v.Y));
        Kind = DwgEntityType.MLine;
    }

    private static bool Finite(params double[] xs)
    {
        foreach (var x in xs) if (!double.IsFinite(x)) return false;
        return true;
    }
}
