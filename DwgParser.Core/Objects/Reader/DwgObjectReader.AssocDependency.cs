using DwgParser.Core.IO;
using DwgParser.Core.Objects.Records;

namespace DwgParser.Core.Objects;

public static partial class DwgObjectReader
{
    private const short TypeAssocDependency = 524;

    private const short TypeAssocGeomDependency = 529;

    private const int MaxAssocDepClassVersion = 3;

    private static void TryDecodeAssocDependency(BitReader shared, DwgVersion version,
        DwgObjectRecord? record, System.Text.Encoding? legacyEncoding)
    {
        if (record is null) return;
        bool r2010 = version.IsR2010Plus(), r2004 = version.IsR2004Plus();
        bool r2013 = version >= DwgVersion.AC1027, r2007 = version >= DwgVersion.AC1021;

        record.AssocDependency ??= new AssocDependencyCapture();
        var cap = record.AssocDependency;
        cap.IsGeomShape = record.LogicalType == TypeAssocGeomDependency;

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
        if (hs <= br.BitPosition || hs > (long)record.BodyEnd * 8) { FailAssocDep(cap, br, dataStart, "帧界"); return; }

        br.ReadHandle();
        if (!TrySkipEed(br)) { FailAssocDep(cap, br, dataStart, "EED"); return; }
        long numReactors = br.ReadBL();
        if (numReactors is < 0 or > 100_000) { FailAssocDep(cap, br, dataStart, "num_reactors"); return; }
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

        int classVersion = br.ReadBS();
        if (classVersion is < 0 or > MaxAssocDepClassVersion)
        {
            FailAssocDep(cap, br, dataStart, $"class_version={classVersion}");
            return;
        }
        long status = br.ReadBL();
        bool isReadDep = br.ReadB();
        bool isWriteDep = br.ReadB();
        bool attached = br.ReadB();
        bool delegating = br.ReadB();
        long order = br.ReadBL();
        bool hasName = br.ReadB();
        string name = hasName ? br.ReadTV() : "";
        long depBodyId = br.ReadBL();
        int geomVersion = -1;
        bool enabled = false;
        string persubent = "";
        bool depOnCompound = false;
        if (cap.IsGeomShape)
        {
            geomVersion = br.ReadBS();
            enabled = br.ReadB();
            persubent = br.ReadTV();
            depOnCompound = br.ReadB();
        }

        if (br.BitPosition != stopBit)
        {
            FailAssocDep(cap, br, dataStart, $"停点 {br.BitPosition - stopBit}");
            return;
        }
        cap.NumReactors = numReactors;
        cap.XdicMissing = xdicMissing;
        cap.ClassVersion = classVersion;
        cap.Status = status;
        cap.IsReadDep = isReadDep;
        cap.IsWriteDep = isWriteDep;
        cap.IsAttachedToObject = attached;
        cap.IsDelegatingToOwningAction = delegating;
        cap.DepOrder = order;
        cap.HasName = hasName;
        cap.Name = name;
        cap.DepBodyId = depBodyId;
        cap.GeomVersion = geomVersion;
        cap.Enabled = enabled;
        cap.PersubentClassname = persubent;
        cap.DependentOnCompoundObject = depOnCompound;
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
            if (!r2004 || !xdicMissing)
                cap.XdicHandle = hb.ReadHandle().Resolve(record.Handle);
            cap.DepOn = hb.ReadHandle().Resolve(record.Handle);
            cap.ReadDep = hb.ReadHandle().Resolve(record.Handle);
            cap.Node = hb.ReadHandle().Resolve(record.Handle);
            cap.DepBody = hb.ReadHandle().Resolve(record.Handle);
            long pad = (long)record.BodyEnd * 8 - hb.BitPosition;
            if (pad is < 0 or > 7) { FailAssocDepEntries(cap, $"句柄流余 {pad}"); return; }
            cap.HandleStreamPadBits = pad;
            cap.EntriesCaptured = true;
            cap.EntriesFailReason = "";
        }
        catch (Exception e) { FailAssocDepEntries(cap, e.GetType().Name); }
    }

    private static void FailAssocDep(AssocDependencyCapture cap, BitReader br, long dataStart, string why)
    {
        cap.Captured = false;
        cap.FailReason = $"ASSOC@{why}:";
        cap.FailBit = br.BitPosition - dataStart;
    }

    private static void FailAssocDepEntries(AssocDependencyCapture cap, string why)
    {
        cap.EntriesCaptured = false;
        cap.EntriesFailReason = why;
    }
}
