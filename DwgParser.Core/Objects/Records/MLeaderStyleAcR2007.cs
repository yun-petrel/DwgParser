using DwgParser.Core.IO;

namespace DwgParser.Core.Objects.Records;

internal sealed class MLeaderStyleAcR2007 : MLeaderStyleRecord
{
    public override string Family => "R2007";

    public MLeaderStyleAcR2007(ulong handle, DwgVersion sourceVersion,
        DwgObjectRecord record, int objStart) : base(handle, sourceVersion, record, objStart) { }

    public override bool TryCapture(BitReader br)
    {
        if (SrcVersion != DwgVersion.AC1021) return false;
        uint bitsize = br.ReadRL();
        long hs = (long)ObjStart * 8 + bitsize;
        if (hs <= br.BitPosition || hs > (long)Rec.BodyEnd * 8)
        { Fail(br, $"帧界(RL vs BodyEnd) hs={hs} pos={br.BitPosition} bodyEnd={(long)Rec.BodyEnd * 8}"); return false; }
        Rec.HandleStreamStartBit = hs;
        Rec.HasHandleStream = true;
        br.ReadHandle();
        long eedStart = br.BitPosition;
        if (!DwgObjectReader.TrySkipEed(br)) { Fail(br, "EED"); return false; }
        Rec.EedStartBit = eedStart;
        Rec.EedBitLen = (int)(br.BitPosition - eedStart);
        int numReactors = br.ReadBL();
        bool xdicMissing = br.ReadB();
        if (numReactors is < 0 or > 8) { Fail(br, $"契约:R2007+ nr>8 捕获联动写侧升级支(反应器引用建模另案),维持Opaque(§609刀试=ODA收但oracle不合)"); return false; }
        long bodyStart = br.BitPosition;
        var v = new MLeaderStyleCapture.MLeaderStyleValues();
        long descIns = 0, tdIns = 0;
        try
        {
            v.ContentType = br.ReadBS();
            v.MleaderOrder = br.ReadBS();
            v.LeaderOrder = br.ReadBS();
            v.MaxPoints = br.ReadBL();
            v.FirstSegAngle = br.ReadBD();
            v.SecondSegAngle = br.ReadBD();
            v.LeaderType = br.ReadBS();
            long c0 = br.BitPosition;
            v.LeaderLineColor = DwgObjectReader.ReadCmcTriple(br);
            Cap.DownCmcSpans.Add((c0, br.BitPosition));
            v.LeaderLineWeight = br.ReadBL();
            v.EnableLanding = br.ReadB();
            v.LandingGap = br.ReadBD();
            v.EnableDogleg = br.ReadB();
            v.DoglegLength = br.ReadBD();
            descIns = br.BitPosition;
            v.ArrowHeadSize = br.ReadBD();
            tdIns = br.BitPosition;
            v.AttachLeft = br.ReadBS();
            v.AttachRight = br.ReadBS();
            v.TextAngleType = br.ReadBS();
            v.TextAlignType = br.ReadBS();
            long c1 = br.BitPosition;
            v.TextColor = DwgObjectReader.ReadCmcTriple(br);
            Cap.DownCmcSpans.Add((c1, br.BitPosition));
            v.TextHeight = br.ReadBD();
            v.EnableTextFrame = br.ReadB();
            v.TextAlwaysLeft = br.ReadB();
            v.AlignSpace = br.ReadBD();
            long c2 = br.BitPosition;
            v.BlockColor = DwgObjectReader.ReadCmcTriple(br);
            Cap.DownCmcSpans.Add((c2, br.BitPosition));
            v.BlockScaleX = br.ReadBD(); v.BlockScaleY = br.ReadBD(); v.BlockScaleZ = br.ReadBD();
            v.UseBlockScale = br.ReadB();
            v.BlockRotation = br.ReadBD();
            v.UseBlockRotation = br.ReadB();
            v.BlockConnection = br.ReadBS();
            v.Scale = br.ReadBD();
            v.IsChanged = br.ReadB();
            v.IsAnnotative = br.ReadB();
            v.BreakSize = br.ReadBD();
        }
        catch (Exception ex) { Fail(br, $"字段区异常:{ex.GetType().Name}"); return false; }
        long streamStart = br.BitPosition;
        if (System.Environment.GetEnvironmentVariable("DWGDBG") == "1")
            System.Console.Error.WriteLine(
                $"MLSRC h={Rec.Handle:X} walk {bodyStart}->{streamStart} hs={hs}");
        try
        {
            br.UnicodeStrings = true;
            string desc = br.ReadTV();
            string textDef = br.ReadTV();
            br.UnicodeStrings = false;
            int ssSize = br.ReadRS();
            bool hasStrings = br.ReadB();
            if (!hasStrings || br.BitPosition != streamStart + ssSize + 17
                || br.BitPosition != hs)
            {
                Fail(br, $"不闭合(has_strings={hasStrings} pos={br.BitPosition} " +
                         $"串区起点+尺寸+17={streamStart + ssSize + 17} hs={hs})");
                return false;
            }
            Cap.Captured = true;
            Cap.BodyStartBit = bodyStart;
            Cap.StreamStartBit = streamStart;
            Cap.Desc = desc;
            Cap.TextDefault = textDef;
            v.Description = desc;
            v.TextDefault = textDef;
            Cap.Values = v;
            Cap.DownCaptured = true;
            Cap.DownHeadEndBit = bodyStart;
            Cap.DownFieldsStartBit = bodyStart;
            Cap.DownAttachStartBit = streamStart;
            Cap.DownStreamStartBit = streamStart;
            Cap.DownDescInsertBit = descIns;
            Cap.DownTextDefInsertBit = tdIns;
            Cap.DownDesc = desc;
            Cap.DownTextDefault = textDef;
            Cap.HandleRefs = MLeaderStyleHandleStream.Read(br, hs, (long)Rec.FrameEnd * 8,
                numReactors, xdicMissing);
        }
        catch (Exception ex) { br.UnicodeStrings = false; Fail(br, $"串流区异常:{ex.GetType().Name}"); return false; }
        return true;
    }
}
