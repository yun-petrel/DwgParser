using DwgParser.Core.IO;
using DwgParser.Core.Render;

namespace DwgParser.Core.Objects;

public sealed class LeaderEntity : DwgEntity, ILeader
{

    public int VertexCount => Points.Count;

    public short LeaderAnnotType { get; private set; }

    public short LeaderPathType { get; private set; }

    public double LeaderBoxHeight { get; private set; }

    public double LeaderBoxWidth { get; private set; }

    public Vec3 LeaderOrigin { get; private set; }

    public Vec3 LeaderExtrusion { get; private set; }

    public Vec3 LeaderXDirection { get; private set; }

    public Vec3 LeaderInsertionOffset { get; private set; }

    public Vec3 LeaderEndPointProjection { get; private set; }

    public bool LeaderHooklineDir { get; private set; }

    public bool LeaderArrowheadOn { get; private set; }

    public short LeaderArrowheadType { get; private set; }

    public bool LeaderUnknownBit1 { get; private set; }

    public bool LeaderTailUnknownBit4 { get; private set; }

    public bool LeaderTailUnknownBit5 { get; private set; }

    private void SyncArrowSlots()
    {
        ArrowheadOn = LeaderArrowheadOn;
        ArrowheadType = LeaderArrowheadType;
    }

    public void ReadBody(BitReader br) => ReadBody(br, record: null);

    public void ReadBody(BitReader br, DwgObjectRecord? record)
    {
        LeaderUnknownBit1 = br.ReadB();
        LeaderAnnotType = br.ReadBS();
        LeaderPathType = br.ReadBS();
        long numPoints = br.ReadBL();
        if (numPoints is < 0 or > 10000) throw new EndOfStreamException();
        Points.Clear();
        for (int i = 0; i < numPoints; i++)
        {
            var p = br.Read3BD();
            Points.Add(new Vec2(p.X, p.Y));
        }
        var o10 = br.Read3BD(); var e210 = br.Read3BD(); var x211 = br.Read3BD(); var i212 = br.Read3BD();
        LeaderOrigin = new Vec3(o10.X, o10.Y, o10.Z);
        LeaderExtrusion = new Vec3(e210.X, e210.Y, e210.Z);
        LeaderXDirection = new Vec3(x211.X, x211.Y, x211.Z);
        LeaderInsertionOffset = new Vec3(i212.X, i212.Y, i212.Z);
        Kind = DwgEntityType.Leader;
        if (Points.Count > 0)
            Start = Points[0];
        if (record == null)
            return;

        var ver = record.SourceVersion;
        if (ver < DwgVersionExtensions.MinimumSupportedVersion || ver > DwgVersion.AC1021)
        {
            if (ver < DwgVersion.AC1024 || record.HandleStreamStartBit <= 0)
                return;
            var p213 = br.Read3BD();
            LeaderEndPointProjection = new Vec3(p213.X, p213.Y, p213.Z);
            record.LeaderTailInsertBit = br.BitPosition;
            LeaderHooklineDir = br.ReadB();
            LeaderArrowheadOn = br.ReadB();
            LeaderArrowheadType = br.ReadBS();
            LeaderTailUnknownBit4 = br.ReadB();
            LeaderTailUnknownBit5 = br.ReadB();
            SyncArrowSlots();
            bool closed = br.BitPosition + 1 == record.HandleStreamStartBit;
            if (!closed) record.LeaderTailInsertBit = 0;
            if (System.Environment.GetEnvironmentVariable("DWGDBG") == "1")
                System.Console.Error.WriteLine(
                    $"LCAP h={record.Handle:X} v={ver} insert={record.LeaderTailInsertBit:X} " +
                    $"cur={br.BitPosition} hs={record.HandleStreamStartBit} closed={closed}");
            return;
        }
        var q213 = br.Read3BD();
        LeaderEndPointProjection = new Vec3(q213.X, q213.Y, q213.Z);
        long boxStart = br.BitPosition;
        LeaderBoxHeight = br.ReadBD();
        LeaderBoxWidth = br.ReadBD();
        long boxEnd = br.BitPosition;
        LeaderHooklineDir = br.ReadB();
        LeaderArrowheadOn = br.ReadB();
        LeaderArrowheadType = br.ReadBS();
        LeaderTailUnknownBit4 = br.ReadB();
        LeaderTailUnknownBit5 = br.ReadB();
        SyncArrowSlots();
        bool upClosed = boxEnd > boxStart && record.HandleStreamStartBit > 0
                        && br.BitPosition == record.HandleStreamStartBit;
        if (upClosed)
        {
            record.LeaderBoxDeleteStartBit = boxStart;
            record.LeaderBoxDeleteBitLen = (int)(boxEnd - boxStart);
        }
        if (System.Environment.GetEnvironmentVariable("DWGDBG") == "1")
            System.Console.Error.WriteLine(
                $"LCAP-UP h={record.Handle:X} v={ver} box=[{boxStart:X},{boxEnd:X}) " +
                $"len={boxEnd - boxStart} cur={br.BitPosition} hs={record.HandleStreamStartBit} " +
                $"closed={upClosed}");
    }
}
