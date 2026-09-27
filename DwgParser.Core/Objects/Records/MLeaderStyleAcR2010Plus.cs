using DwgParser.Core.IO;

namespace DwgParser.Core.Objects.Records;

internal sealed class MLeaderStyleAcR2010Plus : MLeaderStyleRecord
{
    public override string Family => "R2010+";

    public MLeaderStyleAcR2010Plus(ulong handle, DwgVersion sourceVersion,
        DwgObjectRecord record, int objStart) : base(handle, sourceVersion, record, objStart) { }

    public override bool TryCapture(BitReader br)
    {
        if (SrcVersion < DwgVersion.AC1024) return false;
        if (!Rec.HasHandleStream) { Fail(br, "无句柄流锚(公共路径未设 hs)"); return false; }
        long hs = Rec.HandleStreamStartBit;
        if (hs <= br.BitPosition || hs > (long)Rec.BodyEnd * 8)
        { Fail(br, $"帧界(hs vs BodyEnd) hs={hs} pos={br.BitPosition} bodyEnd={(long)Rec.BodyEnd * 8}"); return false; }
        long start = br.BitPosition;
        long headEnd = 0, fieldsStart = 0, attachStart = 0, ssStart = 0;
        long descIns = 0, tdIns = 0;
        string desc = "", textDef = "";
        var v = new MLeaderStyleCapture.MLeaderStyleValues();
        var cmcSpans = new List<(long Start, long End)>();
        try
        {
            br.ReadHandle();
            if (!DwgObjectReader.TrySkipEed(br)) { Fail(br, "EED"); return false; }
            int numReactors = br.ReadBL();
            bool xdicMissing = br.ReadB();
            if (SrcVersion >= DwgVersion.AC1027) br.ReadB();
            if (numReactors is < 0 or > 64) { Fail(br, $"num_reactors={numReactors}"); return false; }
            headEnd = br.BitPosition;
            v.ClassVersion = br.ReadBS();
            fieldsStart = br.BitPosition;

            v.ContentType = br.ReadBS();
            v.MleaderOrder = br.ReadBS();
            v.LeaderOrder = br.ReadBS();
            v.MaxPoints = br.ReadBL();
            v.FirstSegAngle = br.ReadBD(); v.SecondSegAngle = br.ReadBD();
            v.LeaderType = br.ReadBS();
            long cmc1 = br.BitPosition;
            v.LeaderLineColor = DwgObjectReader.ReadCmcTriple(br);
            cmcSpans.Add((cmc1, br.BitPosition));
            v.LeaderLineWeight = br.ReadBL();
            v.EnableLanding = br.ReadB();
            v.LandingGap = br.ReadBD();
            v.EnableDogleg = br.ReadB();
            v.DoglegLength = br.ReadBD();
            descIns = br.BitPosition;
            v.ArrowHeadSize = br.ReadBD();
            tdIns = br.BitPosition;
            v.AttachLeft = br.ReadBS(); v.AttachRight = br.ReadBS();
            v.TextAngleType = br.ReadBS();
            v.TextAlignType = br.ReadBS();
            long cmc2 = br.BitPosition;
            v.TextColor = DwgObjectReader.ReadCmcTriple(br);
            cmcSpans.Add((cmc2, br.BitPosition));
            v.TextHeight = br.ReadBD();
            v.EnableTextFrame = br.ReadB();
            v.TextAlwaysLeft = br.ReadB();
            v.AlignSpace = br.ReadBD();
            long cmc3 = br.BitPosition;
            v.BlockColor = DwgObjectReader.ReadCmcTriple(br);
            cmcSpans.Add((cmc3, br.BitPosition));
            v.BlockScaleX = br.ReadBD(); v.BlockScaleY = br.ReadBD(); v.BlockScaleZ = br.ReadBD();
            v.UseBlockScale = br.ReadB();
            v.BlockRotation = br.ReadBD();
            v.UseBlockRotation = br.ReadB();
            v.BlockConnection = br.ReadBS();
            v.Scale = br.ReadBD();
            v.IsChanged = br.ReadB();
            v.IsAnnotative = br.ReadB();
            v.BreakSize = br.ReadBD();
            attachStart = br.BitPosition;
            v.AttachDir = br.ReadBS(); v.AttachTop = br.ReadBS(); v.AttachBottom = br.ReadBS();
            if (SrcVersion >= DwgVersion.AC1027) v.TextExtended = br.ReadB();
            ssStart = br.BitPosition;

            if (Rec.StringStreamStartBit != ssStart)
            { Fail(br, $"串区起点不符(StringStreamStartBit={Rec.StringStreamStartBit}≠attach末={ssStart})"); return false; }
            var saved = br.StringStream;
            br.StringStream = null;
            desc = br.ReadTV();
            textDef = br.ReadTV();
            br.StringStream = saved;
            int ssSize = br.ReadRS();
            bool hasStrings = br.ReadB();
            if (!hasStrings || br.BitPosition != hs)
            { Fail(br, $"不闭合(has_strings={hasStrings} pos={br.BitPosition} hs={hs})"); return false; }
            if (ssSize <= 0 || (long)ssStart + ssSize + 17 != hs)
            { Fail(br, $"页脚尺寸不合(ssStart+ss+17={ssStart + (long)ssSize + 17}≠hs={hs}, ss={ssSize})"); return false; }

            Cap.DownCaptured = true;
            Cap.DownHeadEndBit = headEnd;
            Cap.DownFieldsStartBit = fieldsStart;
            Cap.DownAttachStartBit = attachStart;
            Cap.DownStreamStartBit = ssStart;
            Cap.DownDesc = desc;
            Cap.DownTextDefault = textDef;
            Cap.DownDescInsertBit = descIns;
            Cap.DownTextDefInsertBit = tdIns;
            if (cmcSpans.Count != 3)
            { Fail(br, $"§374 内嵌色段不齐 count={cmcSpans.Count}"); return false; }
            Cap.DownCmcSpans = cmcSpans;
            v.Description = desc;
            v.TextDefault = textDef;
            Cap.Values = v;
            Cap.HandleRefs = MLeaderStyleHandleStream.Read(br, hs, (long)Rec.FrameEnd * 8,
                numReactors, xdicMissing);
            if (System.Environment.GetEnvironmentVariable("DWGDBG") == "1")
                System.Console.Error.WriteLine(
                    $"MLDN h={Rec.Handle:X} t={Rec.TypeCode} {SrcVersion} own={start} headEnd={headEnd} " +
                    $"cv={fieldsStart - headEnd}fields={attachStart - fieldsStart}attach={ssStart - attachStart} " +
                    $"ss={ssSize} hs={hs} desc='{desc}' td='{textDef}' ins=+{descIns - fieldsStart}/+{tdIns - fieldsStart}");
        }
        catch (Exception ex)
        {
            Fail(br, $"走查抛:{ex.GetType().Name}(自 own 起 {br.BitPosition - start} 位)");
            return false;
        }
        return true;
    }
}
