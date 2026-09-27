using System.Text;
using DwgParser.Core.IO;
using DwgParser.Core.Render;

namespace DwgParser.Core.Objects;

public static partial class DwgObjectReader
{

    private static bool DecodeBlockControl(BitReader br, WalkResult result, bool r2010Plus,
        int objStart, DwgVersion version, DwgObjectRecord? record)
    {
        bool r2004Plus = version.IsR2004Plus();
        bool r2013Plus = version >= DwgVersion.AC1027;
        uint bitsizeRl = 0;
        if (!r2010Plus)
            bitsizeRl = br.ReadRL();
        br.ReadHandle();
        if (!TrySkipEed(br))
            return false;
        long numReactors = br.ReadBL();
        bool xdicMissing = false;
        if (r2004Plus)
        {
            xdicMissing = br.ReadB();
            if (r2013Plus)
                br.ReadB();
        }
        long numEntries = br.ReadBL();
        if (numReactors is < 0 or > 10000 || numEntries is < 0 or > 1_000_000)
            return false;
        long ac1021Hs = (long)objStart * 8 + bitsizeRl;
        long jumpHs = r2010Plus
            ? (record?.HandleStreamStartBit ?? 0)
            : (version == DwgVersion.AC1021 ? ac1021Hs : 0);
        bool jump = jumpHs > 0 && jumpHs >= br.BitPosition
                    && (r2010Plus || version == DwgVersion.AC1021);
        if (jump)
        {
            br.SetPosition((int)(jumpHs >> 3), (int)(jumpHs & 7));
        }
        br.ReadHandle();
        for (int i = 0; i < numReactors; i++)
            br.ReadHandle();
        if (!r2004Plus || !xdicMissing)
            br.ReadHandle();
        for (int i = 0; i < numEntries; i++)
        {
            var bh = br.ReadHandle();
            result.BlockControlEntries.Add(bh.Resolve(0));
        }
        var ms = br.ReadHandle();
        var ps = br.ReadHandle();
        result.ModelSpaceHandle = ms.Resolve(0);
        result.PaperSpaceHandle = ps.Resolve(0);
        return true;
    }
}