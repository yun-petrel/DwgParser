using DwgParser.Core.IO;
using DwgParser.Core.Objects.Records;

namespace DwgParser.Core.Objects;

public static partial class DwgObjectReader
{
    private const short TypeAssocVertexParam = 523;

    private static void TryDecodeAssocVertexParam(BitReader shared, DwgVersion version,
        DwgObjectRecord? record, System.Text.Encoding? legacyEncoding)
    {
        if (record is null) return;
        bool r2010 = version.IsR2010Plus(), r2004 = version.IsR2004Plus();
        bool r2013 = version >= DwgVersion.AC1027, r2007 = version >= DwgVersion.AC1021;

        record.AssocVertexParam ??= new AssocVertexParamCapture();
        var cap = record.AssocVertexParam;

        var br = new BitReader(shared.UnderlyingData(), 0)
        {
            UnicodeStrings = r2007,
            LegacyEncoding = legacyEncoding,
        };
        DwgObjectRecord.SeekTo(br, record.CarryStartBit);
        long dataStart = br.BitPosition;

        long hs;
        if (!r2010)
        {
            uint rl = br.ReadRL();
            hs = (long)record.BodyStart * 8 + rl;
            cap.SourceDataBits = rl;
        }
        else
        {
            hs = record.HandleStreamStartBit;
            cap.SourceDataBits = hs - dataStart;
        }
        if (hs <= br.BitPosition || hs > (long)record.BodyEnd * 8) { FailAssocVp(cap, br, dataStart, "帧界"); return; }

        br.ReadHandle();
        if (!TrySkipEed(br)) { FailAssocVp(cap, br, dataStart, "EED"); return; }
        long numReactors = br.ReadBL();
        if (numReactors is < 0 or > 100_000) { FailAssocVp(cap, br, dataStart, "num_reactors"); return; }
        bool xdicMissing = r2004 && br.ReadB();
        if (r2013) br.ReadB();

        long stopBit = hs;
        if (r2007)
        {
            var scratch = new DwgObjectRecord();
            uint bitsize = (uint)(hs - (long)record.BodyStart * 8);
            AttachStringStream(br, record.BodyStart, bitsize, version, scratch);
            stopBit = scratch.StringStreamStartBit > 0 ? scratch.StringStreamStartBit : hs - 1;
        }
        cap.StopBit = stopBit;

        int isR2013 = br.ReadBS();
        long? aapVersion = null;
        if (r2013) aapVersion = br.ReadBL();
        string name = br.ReadTV();
        long asdapClassVersion = br.ReadBL();
        long classVersion = br.ReadBL();
        double px = br.ReadBD(), py = br.ReadBD(), pz = br.ReadBD();

        if (br.BitPosition != stopBit)
        {
            FailAssocVp(cap, br, dataStart, $"停点 {br.BitPosition - stopBit}");
            return;
        }
        cap.IsR2013 = isR2013;
        cap.AapVersion = aapVersion;
        cap.Name = name;
        cap.AsdapClassVersion = asdapClassVersion;
        cap.ClassVersion = classVersion;
        cap.PtX = px;
        cap.PtY = py;
        cap.PtZ = pz;
        cap.NumReactors = numReactors;
        cap.XdicMissing = xdicMissing;
        cap.WalkEndBit = br.BitPosition;
        cap.FailReason = "";
        cap.Captured = true;

        try
        {
            var hb = new BitReader(shared.UnderlyingData(), 0) { UnicodeStrings = r2007 };
            hb.SetPosition((int)(hs >> 3), (int)(hs & 7));
            cap.OwnerHandle = hb.ReadHandle().Resolve(record.Handle);
            var reactors = new List<ulong>((int)numReactors);
            for (long i = 0; i < numReactors; i++) reactors.Add(hb.ReadHandle().Resolve(record.Handle));
            cap.ReactorHandles = reactors;
            if (!r2004 || !xdicMissing) cap.XdicHandle = hb.ReadHandle().Resolve(record.Handle);
            cap.Dep = hb.ReadHandle().Resolve(record.Handle);
            long pad = (long)record.BodyEnd * 8 - hb.BitPosition;
            if (pad is < 0 or > 7) { FailAssocVpEntries(cap, $"句柄流余 {pad}"); return; }
            cap.HandleStreamPadBits = pad;
            cap.EntriesCaptured = true;
            cap.EntriesFailReason = "";
        }
        catch (Exception e) { FailAssocVpEntries(cap, e.GetType().Name); }
    }

    private static void FailAssocVp(AssocVertexParamCapture cap, BitReader br, long dataStart, string why)
    {
        cap.Captured = false;
        cap.FailReason = $"ASSOCVP@{why}:";
        cap.FailBit = br.BitPosition - dataStart;
    }

    private static void FailAssocVpEntries(AssocVertexParamCapture cap, string why)
    {
        cap.EntriesCaptured = false;
        cap.EntriesFailReason = why;
    }
}
