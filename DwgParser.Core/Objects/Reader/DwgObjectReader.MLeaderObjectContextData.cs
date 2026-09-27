using System.Text;
using DwgParser.Core.IO;
using DwgParser.Core.Objects.Records;

namespace DwgParser.Core.Objects;

public static partial class DwgObjectReader
{
    private const long MlCtxMaxRoots = 4_096;
    private const long MlCtxMaxLines = 4_096;
    private const long MlCtxMaxPoints = 100_000;
    private const long MlCtxMaxBreaks = 100_000;

    private static void TryDecodeMLeaderObjectContextData(BitReader shared, DwgVersion version,
        DwgObjectRecord? record, Encoding? legacyEncoding)
    {
        if (record is null) return;
        bool r2010 = version.IsR2010Plus(), r2004 = version.IsR2004Plus();
        bool r2013 = version >= DwgVersion.AC1027, r2007 = version >= DwgVersion.AC1021;

        record.MLeaderObjectContextData ??= new MLeaderObjectContextDataCapture();
        var cap = record.MLeaderObjectContextData;

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
        if (hs <= br.BitPosition || hs > (long)record.BodyEnd * 8)
        {
            FailMlCtx(cap, br, dataStart, "帧界");
            return;
        }

        br.ReadHandle();
        if (!TrySkipEed(br)) { FailMlCtx(cap, br, dataStart, "EED"); return; }
        long numReactors = br.ReadBL();
        if (numReactors is < 0 or > 100_000) { FailMlCtx(cap, br, dataStart, "num_reactors"); return; }
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

        var lineSlots = new List<MlCtxLineCapture>();

        cap.ClassVersion = br.ReadBS();
        cap.IsDefault = br.ReadB();

        long numRoots = br.ReadBL();
        if (numRoots is < 0 or > MlCtxMaxRoots) { FailMlCtx(cap, br, dataStart, "num_leader_roots"); return; }
        var roots = new List<MlCtxRootCapture>((int)numRoots);
        for (long i = 0; i < numRoots; i++)
        {
            var root = new MlCtxRootCapture();
            bool hasLlp = br.ReadB();
            bool hasDogleg = br.ReadB();
            root.HasLastLeaderLinePoint = hasLlp;
            root.HasDogleg = hasDogleg;
            if (hasLlp) root.LastLeaderLinePoint = ReadMlCtxVec(br);
            if (hasDogleg) root.DoglegVector = ReadMlCtxVec(br);
            long rootBreaks = br.ReadBL();
            if (rootBreaks is < 0 or > MlCtxMaxBreaks) { FailMlCtx(cap, br, dataStart, "root.num_breaks"); return; }
            var rb = new List<MlCtxBreakCapture>((int)rootBreaks);
            for (long k = 0; k < rootBreaks; k++)
                rb.Add(new MlCtxBreakCapture(ReadMlCtxVec(br), ReadMlCtxVec(br)));
            root.Breaks = rb;
            root.BranchIndex = br.ReadBL();
            root.DoglegLength = br.ReadBD();

            long numLines = br.ReadBL();
            if (numLines is < 0 or > MlCtxMaxLines) { FailMlCtx(cap, br, dataStart, "root.num_lines"); return; }
            var lines = new List<MlCtxLineCapture>((int)numLines);
            for (long l = 0; l < numLines; l++)
            {
                var line = new MlCtxLineCapture();
                long numPoints = br.ReadBL();
                if (numPoints is < 0 or > MlCtxMaxPoints) { FailMlCtx(cap, br, dataStart, "line.num_points"); return; }
                var pts = new List<MlCtxVec>((int)numPoints);
                for (long p = 0; p < numPoints; p++) pts.Add(ReadMlCtxVec(br));
                line.Points = pts;
                long lineBreaks = br.ReadBL();
                if (lineBreaks is < 0 or > MlCtxMaxBreaks) { FailMlCtx(cap, br, dataStart, "line.num_breaks"); return; }
                var lb = new List<MlCtxBreakCapture>((int)lineBreaks);
                for (long k = 0; k < lineBreaks; k++)
                    lb.Add(new MlCtxBreakCapture(ReadMlCtxVec(br), ReadMlCtxVec(br)));
                line.Breaks = lb;
                line.LineIndex = br.ReadBL();
                if (r2010)
                {
                    line.Type = br.ReadBS();
                    line.Color = ReadMlCtxColor(br);
                    line.LineWeight = br.ReadBL();
                    line.ArrowSize = br.ReadBD();
                    line.Flags = br.ReadBL();
                    lineSlots.Add(line);
                }
                lines.Add(line);
            }
            root.Lines = lines;
            if (r2010) root.AttachDir = br.ReadBS();
            roots.Add(root);
        }

        cap.ContextScale = br.ReadBD();
        cap.ContentBase = ReadMlCtxVec(br);
        cap.TextHeight = br.ReadBD();
        cap.ArrowSize = br.ReadBD();
        cap.LandingGap = br.ReadBD();
        cap.TextLeft = br.ReadBS();
        cap.TextRight = br.ReadBS();
        cap.TextAngleType = br.ReadBS();
        cap.TextAlignment = br.ReadBS();

        cap.HasTextContent = br.ReadB();
        if (cap.HasTextContent)
        {
            cap.TextLabel = br.ReadTV();
            cap.TextNormal = ReadMlCtxVec(br);
            cap.TextLocation = ReadMlCtxVec(br);
            cap.TextDirection = ReadMlCtxVec(br);
            cap.TextRotation = br.ReadBD();
            cap.TextBoundaryWidth = br.ReadBD();
            cap.TextBoundaryHeight = br.ReadBD();
            cap.LineSpacingFactor = br.ReadBD();
            cap.LineSpacingStyle = br.ReadBS();
            cap.TextColor = ReadMlCtxColor(br);
            cap.TextAlignmentPoint = br.ReadBS();
            cap.FlowDirection = br.ReadBS();
            cap.BackgroundColor = ReadMlCtxColor(br);
            cap.BackgroundScale = br.ReadBD();
            cap.BackgroundTransparency = br.ReadBL();
            cap.BackgroundFillEnabled = br.ReadB();
            cap.BackgroundMaskFillOn = br.ReadB();
            cap.ColumnType = br.ReadBS();
            cap.TextHeightAutomatic = br.ReadB();
            cap.ColumnWidth = br.ReadBD();
            cap.ColumnGutter = br.ReadBD();
            cap.ColumnFlowReversed = br.ReadB();
            long numColSizes = br.ReadBL();
            if (numColSizes is < 0 or > MlCtxMaxPoints) { FailMlCtx(cap, br, dataStart, "num_column_sizes"); return; }
            var cols = new List<double>((int)numColSizes);
            for (long i = 0; i < numColSizes; i++) cols.Add(br.ReadBD());
            cap.ColumnSizes = cols;
            cap.WordBreak = br.ReadB();
            cap.UnknownTailBit = br.ReadB();
        }
        else
        {
            cap.HasBlockContent = br.ReadB();
            if (cap.HasBlockContent)
            {
                cap.BlockNormal = ReadMlCtxVec(br);
                cap.BlockLocation = ReadMlCtxVec(br);
                cap.BlockScale = ReadMlCtxVec(br);
                cap.BlockRotation = br.ReadBD();
                cap.BlockColor = ReadMlCtxColor(br);
                var matrix = new List<double>(16);
                for (int i = 0; i < 16; i++) matrix.Add(br.ReadBD());
                cap.BlockTransform = matrix;
            }
        }

        cap.BasePoint = ReadMlCtxVec(br);
        cap.BaseDirection = ReadMlCtxVec(br);
        cap.BaseVertical = ReadMlCtxVec(br);
        cap.NormalReversed = br.ReadB();
        if (r2010)
        {
            cap.TextTopAttachment = br.ReadBS();
            cap.TextBottomAttachment = br.ReadBS();
        }

        if (br.BitPosition != stopBit)
        {
            FailMlCtx(cap, br, dataStart, $"停点 {br.BitPosition - stopBit}");
            return;
        }
        cap.Roots = roots;
        cap.NumReactors = numReactors;
        cap.XdicMissing = xdicMissing;
        cap.WalkEndBit = br.BitPosition;
        cap.FailReason = "";
        cap.Captured = true;

        ReadMlCtxHandleStream(shared, record, cap, lineSlots, hs, numReactors, xdicMissing,
            r2004, r2007, roots.Count, cap.HasTextContent, cap.HasBlockContent);
    }

    private static void ReadMlCtxHandleStream(BitReader shared, DwgObjectRecord record,
        MLeaderObjectContextDataCapture cap, List<MlCtxLineCapture> lineSlots, long hs,
        long numReactors, bool xdicMissing, bool r2004, bool r2007,
        int rootCount, bool hasText, bool hasBlock)
    {
        try
        {
            var hb = new BitReader(shared.UnderlyingData(), 0) { UnicodeStrings = r2007 };
            hb.SetPosition((int)(hs >> 3), (int)(hs & 7));
            cap.OwnerHandle = hb.ReadHandle().Resolve(record.Handle);
            var reactors = new List<ulong>((int)numReactors);
            for (long i = 0; i < numReactors; i++) reactors.Add(hb.ReadHandle().Resolve(record.Handle));
            cap.ReactorHandles = reactors;
            if (!r2004 || !xdicMissing) cap.XdicHandle = hb.ReadHandle().Resolve(record.Handle);
            cap.ScaleHandle = hb.ReadHandle().Resolve(record.Handle);
            foreach (var line in lineSlots)
            {
                line.LTypeHandle = hb.ReadHandle().Resolve(record.Handle);
                line.ArrowHandle = hb.ReadHandle().Resolve(record.Handle);
            }
            if (hasText) cap.TextStyleHandle = hb.ReadHandle().Resolve(record.Handle);
            else if (hasBlock) cap.BlockRecordHandle = hb.ReadHandle().Resolve(record.Handle);

            long pad = (long)record.BodyEnd * 8 - hb.BitPosition;
            if (pad is < 0 or > 7)
            {
                cap.EntriesCaptured = false;
                cap.EntriesFailReason = $"句柄流余 {pad}（根 {rootCount} / 线 {lineSlots.Count}）";
                return;
            }
            cap.HandleStreamPadBits = pad;
            cap.EntriesCaptured = true;
            cap.EntriesFailReason = "";
        }
        catch (Exception e) { cap.EntriesCaptured = false; cap.EntriesFailReason = e.GetType().Name; }
    }

    private static MlCtxVec ReadMlCtxVec(BitReader br) => new(br.ReadBD(), br.ReadBD(), br.ReadBD());

    private static MlCtxColor ReadMlCtxColor(BitReader br)
        => new(ReadCmc(br, out uint rgb, out string name, out string book), rgb, name, book);

    private static void FailMlCtx(MLeaderObjectContextDataCapture cap, BitReader br, long dataStart, string why)
    {
        cap.Captured = false;
        cap.FailReason = $"MLCTX@{why}:";
        cap.FailBit = br.BitPosition - dataStart;
    }
}
