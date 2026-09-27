using DwgParser.Core.IO;

namespace DwgParser.Core.Objects.Records;

public sealed class MLeaderStyleHandleRefs
{
    public DwgHandleReference Owner { get; internal set; }

    public List<DwgHandleReference> Reactors { get; } = new();

    public DwgHandleReference? Xdic { get; internal set; }

    public DwgHandleReference LineType { get; internal set; }

    public DwgHandleReference ArrowHead { get; internal set; }

    public DwgHandleReference TextStyle { get; internal set; }

    public DwgHandleReference Block { get; internal set; }

    public IReadOnlyList<DwgHandleReference> TypeAxis => _typeAxis;
    private readonly List<DwgHandleReference> _typeAxis = new(4);

    public int TailBits { get; internal set; }

    internal void SetTypeAxis(DwgHandleReference a, DwgHandleReference b, DwgHandleReference c, DwgHandleReference d)
    {
        LineType = a; ArrowHead = b; TextStyle = c; Block = d;
        _typeAxis.Clear();
        _typeAxis.Add(a); _typeAxis.Add(b); _typeAxis.Add(c); _typeAxis.Add(d);
    }
}

internal static class MLeaderStyleHandleStream
{
    internal static MLeaderStyleHandleRefs? Read(BitReader br, long hsBit, long frameEndBit,
        int numReactors, bool xdicMissing)
    {
        if (numReactors < 0 || numReactors > 8) return null;
        if (hsBit <= 0 || hsBit >= frameEndBit) return null;
        var br2 = new BitReader(br.UnderlyingData(), 0);
        br2.SetPosition((int)(hsBit / 8), (int)(hsBit % 8));
        try
        {
            var m = new MLeaderStyleHandleRefs();
            m.Owner = br2.ReadHandle();
            for (int i = 0; i < numReactors; i++) m.Reactors.Add(br2.ReadHandle());
            if (!xdicMissing) m.Xdic = br2.ReadHandle();
            var a = br2.ReadHandle(); var b = br2.ReadHandle();
            var c = br2.ReadHandle(); var d = br2.ReadHandle();
            if (a.Code != 5 || b.Code != 5 || c.Code != 5 || d.Code != 5) return null;
            m.SetTypeAxis(a, b, c, d);
            m.TailBits = (int)Math.Max(0, frameEndBit - br2.BitPosition);
            return m;
        }
        catch { return null; }
    }
}
