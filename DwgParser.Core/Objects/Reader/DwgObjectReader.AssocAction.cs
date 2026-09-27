using DwgParser.Core.IO;
using DwgParser.Core.Objects.Records;

namespace DwgParser.Core.Objects;

public static partial class DwgObjectReader
{
    private const short TypeAssocNetwork = 520;

    private const short TypeAssocAction = 521;

    private const long MaxAssocActions = 100;

    private static void TryDecodeAssocAction(BitReader shared, DwgVersion version,
        DwgObjectRecord? record, System.Text.Encoding? legacyEncoding)
    {
        if (record is null) return;
        bool r2010 = version.IsR2010Plus(), r2004 = version.IsR2004Plus();
        bool r2013 = version >= DwgVersion.AC1027, r2007 = version >= DwgVersion.AC1021;

        record.AssocAction ??= new AssocActionCapture();
        var cap = record.AssocAction;
        cap.IsNetwork = record.LogicalType == TypeAssocNetwork;

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
        if (hs <= br.BitPosition || hs > (long)record.BodyEnd * 8) { FailAssocAct(cap, br, dataStart, "帧界"); return; }

        br.ReadHandle();
        if (!TrySkipEed(br)) { FailAssocAct(cap, br, dataStart, "EED"); return; }
        long numReactors = br.ReadBL();
        if (numReactors is < 0 or > 100_000) { FailAssocAct(cap, br, dataStart, "num_reactors"); return; }
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
        long geometryStatus = br.ReadBL();
        long actionIndex = br.ReadBL();
        long maxDepIndex = br.ReadBL();
        long numDeps = br.ReadBL();
        if (numDeps is < 0 or > 100_000) { FailAssocAct(cap, br, dataStart, "num_deps"); return; }
        var depFlags = new bool[(int)numDeps];
        for (int i = 0; i < numDeps; i++) depFlags[i] = br.ReadB();

        long numOwnedParams = -1, numValues = -1;
        int slot1 = -1, slot2 = -1;
        var valueParams = new List<AssocValueParamCapture>();
        if (classVersion > 1)
        {
            slot1 = br.ReadBS();
            numOwnedParams = br.ReadBL();
            if (numOwnedParams is < 0 or > MaxAssocActions)
            { FailAssocAct(cap, br, dataStart, $"num_owned_params={numOwnedParams}"); return; }
            slot2 = br.ReadBS();
            numValues = br.ReadBL();
            if (numValues is < 0 or > MaxAssocActions)
            { FailAssocAct(cap, br, dataStart, $"num_values={numValues}"); return; }
            for (long i = 0; i < numValues; i++)
            {
                var vp = new AssocValueParamCapture();
                vp.ClassVersion = br.ReadBL();
                vp.Name = br.ReadTV();
                vp.UnitType = br.ReadBL();
                long numVars = br.ReadBL();
                if (numVars is < 0 or > MaxAssocActions)
                { FailAssocAct(cap, br, dataStart, $"num_vars={numVars}(value{i})"); return; }
                var vars = new List<AssocEvalVarCapture>((int)numVars);
                for (int k = 0; k < numVars; k++)
                {
                    var v = new AssocEvalVarCapture { Code = br.ReadBS() };
                    v.Kind = AssocEvalVariantTable.KindOf(v.Code);
                    switch (v.Kind)
                    {
                        case AssocEvalVariantKind.Real: v.Real = br.ReadBD(); break;
                        case AssocEvalVariantKind.Int32: v.Int32 = br.ReadBL(); break;
                        case AssocEvalVariantKind.Int16: v.Int16 = br.ReadBS(); break;
                        case AssocEvalVariantKind.Int8: v.Int8 = br.ReadRC(); break;
                        case AssocEvalVariantKind.Text: v.Text = br.ReadTV(); break;
                        case AssocEvalVariantKind.Handle:
                        case AssocEvalVariantKind.Null:
                        case AssocEvalVariantKind.Unknown:
                            break;
                    }
                    if (v.Kind == AssocEvalVariantKind.Unknown)
                    {
                        FailAssocAct(cap, br, dataStart,
                            $"值参 code={v.Code} 的载荷型不成文(libredwg decoder 该支零消费)");
                        return;
                    }
                    vars.Add(v);
                }
                vp.Vars = vars;
                valueParams.Add(vp);
            }
        }

        int networkVersion = -1;
        long networkActionIndex = -1, numActions = -1, numOwnedActions = -1;
        bool[] actionFlags = Array.Empty<bool>();
        if (cap.IsNetwork)
        {
            networkVersion = br.ReadBS();
            networkActionIndex = br.ReadBL();
            numActions = br.ReadBL();
            if (numActions is < 0 or > MaxAssocActions) { FailAssocAct(cap, br, dataStart, $"num_actions={numActions}"); return; }
            actionFlags = new bool[(int)numActions];
            for (int i = 0; i < numActions; i++) actionFlags[i] = br.ReadB();
            numOwnedActions = br.ReadBL();
            if (numOwnedActions is < 0 or > MaxAssocActions)
            { FailAssocAct(cap, br, dataStart, $"num_owned_actions={numOwnedActions}"); return; }
        }

        if (br.BitPosition != stopBit)
        {
            FailAssocAct(cap, br, dataStart, $"停点 {br.BitPosition - stopBit}");
            return;
        }
        cap.ClassVersion = classVersion;
        cap.GeometryStatus = geometryStatus;
        cap.ActionIndex = actionIndex;
        cap.MaxAssocDepIndex = maxDepIndex;
        cap.NumDeps = numDeps;
        cap.DepOwnedFlags = depFlags;
        cap.NetworkVersion = networkVersion;
        cap.NetworkActionIndex = networkActionIndex;
        cap.NumActions = numActions;
        cap.NumOwnedActions = numOwnedActions;
        cap.OwnedParamSlot1 = slot1;
        cap.OwnedParamSlot2 = slot2;
        cap.NumOwnedParams = numOwnedParams;
        cap.NumValues = numValues;
        cap.Values = valueParams;
        cap.ActionOwnedFlags = actionFlags;
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
            cap.OwningNetwork = hb.ReadHandle().Resolve(record.Handle);
            cap.ActionBody = hb.ReadHandle().Resolve(record.Handle);
            var deps = new List<ulong>((int)numDeps);
            for (long i = 0; i < numDeps; i++) deps.Add(hb.ReadHandle().Resolve(record.Handle));
            cap.DepHandles = deps;
            if (classVersion > 1)
            {
                var owned = new List<ulong>((int)numOwnedParams);
                for (long i = 0; i < numOwnedParams; i++) owned.Add(hb.ReadHandle().Resolve(record.Handle));
                cap.OwnedParamHandles = owned;
                foreach (var vp in valueParams)
                {
                    foreach (var v in vp.Vars)
                    {
                        if (v.Kind == AssocEvalVariantKind.Handle) v.ValueHandle = hb.ReadHandle().Resolve(record.Handle);
                        v.SlotHandle = hb.ReadHandle().Resolve(record.Handle);
                    }
                    vp.ControlledObjdep = hb.ReadHandle().Resolve(record.Handle);
                }
            }
            if (cap.IsNetwork)
            {
                var acts = new List<ulong>((int)numActions);
                for (long i = 0; i < numActions; i++) acts.Add(hb.ReadHandle().Resolve(record.Handle));
                cap.ActionHandles = acts;
                var owned = new List<ulong>((int)numOwnedActions);
                for (long i = 0; i < numOwnedActions; i++) owned.Add(hb.ReadHandle().Resolve(record.Handle));
                cap.OwnedActionHandles = owned;
            }
            long pad = (long)record.BodyEnd * 8 - hb.BitPosition;
            if (pad is < 0 or > 7) { FailAssocActEntries(cap, $"句柄流余 {pad}"); return; }
            cap.HandleStreamPadBits = pad;
            cap.EntriesCaptured = true;
            cap.EntriesFailReason = "";
        }
        catch (Exception e) { FailAssocActEntries(cap, e.GetType().Name); }
    }

    private static void FailAssocAct(AssocActionCapture cap, BitReader br, long dataStart, string why)
    {
        cap.Captured = false;
        cap.FailReason = $"ASSOCA@{why}:";
        cap.FailBit = br.BitPosition - dataStart;
    }

    private static void FailAssocActEntries(AssocActionCapture cap, string why)
    {
        cap.EntriesCaptured = false;
        cap.EntriesFailReason = why;
    }
}
