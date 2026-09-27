using DwgParser.Core;
using DwgParser.Core.IO;

namespace DwgParser.Core.Objects;

public interface IModelerGeometry : IEntity
{
    bool GeometryCaptured { get; }

    bool AcisEmpty { get; }

    int AcisVersion { get; }

    System.Collections.Generic.IReadOnlyList<long> SatBlockSizes { get; }

    bool WireframeDataPresent { get; }

    bool PointPresent { get; }

    double PointX { get; }

    double PointY { get; }

    double PointZ { get; }

    long Isolines { get; }

    bool IsolinePresent { get; }

    int TailBits { get; }

    bool AcisUnknownBit { get; }
}

public abstract class ModelerGeometryEntity : DwgEntity, IModelerGeometry
{
    public bool GeometryCaptured { get; private set; }
    public bool AcisEmpty { get; private set; }

    public bool AcisUnknownBit { get; private set; }
    public int AcisVersion { get; private set; }
    public System.Collections.Generic.IReadOnlyList<long> SatBlockSizes { get; private set; }
        = System.Array.Empty<long>();
    public bool WireframeDataPresent { get; private set; }
    public bool PointPresent { get; private set; }
    public double PointX { get; private set; }
    public double PointY { get; private set; }
    public double PointZ { get; private set; }
    public long Isolines { get; private set; }
    public bool IsolinePresent { get; private set; }
    public int TailBits { get; private set; } = -1;

    protected void ReadModelerGeometry(BitReader br, DwgObjectRecord? record)
    {
        long start = br.BitPosition;
        long hs = record is { HasHandleStream: true } ? record.HandleStreamStartBit : long.MaxValue;
        try
        {
            bool empty = br.ReadB();
            int ver = 0;
            var sizes = new System.Collections.Generic.List<long>();
            if (!empty)
            {
                AcisUnknownBit = br.ReadB();
                ver = br.ReadBS();
                if (ver == 1)
                {
                    while (true)
                    {
                        long sz = br.ReadBL();
                        if (sz <= 0) break;
                        if (sz > 8_000_000 || sizes.Count > 64) throw new InvalidDataException("SAT 块长越界");
                        for (long i = 0; i < sz; i++) br.ReadRC();
                        sizes.Add(sz);
                    }
                }
                else if (ver == 2)
                {
                    if (hs == long.MaxValue)
                        throw new InvalidDataException("SAB(v2) 无法定位句柄流起点");
                    long curBit = br.BitPosition;
                    long sabBits = hs - 1 - curBit;
                    if (sabBits < 0 || sabBits % 8 != 0)
                        throw new InvalidDataException($"SAB(v2) 位长异常: {sabBits}");
                    int sabBytes = (int)(sabBits / 8);
                    if (sabBytes > 16_000_000) throw new InvalidDataException("SAB(v2) 长度越界");
                    for (int i = 0; i < sabBytes; i++) br.ReadRC();
                }
                else
                {
                    throw new InvalidDataException($"ACIS version={ver} 未支持");
                }
            }
            bool wfp = br.ReadB();
            bool pp = false;
            double px = 0, py = 0, pz = 0;
            long iso = 0;
            bool isop = false;
            if (wfp)
            {
                pp = br.ReadB();
                if (pp)
                {
                    var p = br.Read3BD();
                    px = p.X; py = p.Y; pz = p.Z;
                }
                iso = br.ReadBL();
                isop = br.ReadB();
                if (isop) throw new InvalidDataException("isoline 数据未取证");
            }
            _ = br.ReadB();
            if (br.BitPosition > hs) throw new InvalidDataException("越过句柄流起点");
            int tail = -1;
            if (hs != long.MaxValue)
            {
                long probe = br.BitPosition;
                byte bb = br.ReadBB();
                if (br.BitPosition == hs) tail = bb;
                else br.SetPosition((int)(probe >> 3), (int)(probe & 7));
            }

            AcisEmpty = empty;
            AcisVersion = empty ? 0 : ver;
            SatBlockSizes = sizes;
            WireframeDataPresent = wfp;
            PointPresent = pp;
            PointX = px; PointY = py; PointZ = pz;
            Isolines = iso;
            IsolinePresent = isop;
            TailBits = tail;
            GeometryCaptured = true;
        }
        catch (Exception e) when (e is InvalidDataException or EndOfStreamException
                                     or System.IO.IOException or OverflowException)
        {
            int b = (int)(start >> 3), bit = (int)(start & 7);
            br.SetPosition(b, bit);
            GeometryCaptured = false;
            if (DwgObjectStreamWriter.DebugTrace)
                System.Console.Error.WriteLine(
                    $"DWGDBG ACIS h={Handle:X} 未解（{e.Message}）⇒ 几何区原样搬运");
        }
    }
}
