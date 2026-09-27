using DwgParser.Core.IO;

namespace DwgParser.Core.Objects.Records;

internal sealed class MLeaderStyleAcPreR2007 : MLeaderStyleRecord
{
    public override string Family => "PreR2007";

    public MLeaderStyleAcPreR2007(ulong handle, DwgVersion sourceVersion,
        DwgObjectRecord record, int objStart) : base(handle, sourceVersion, record, objStart) { }

    public override bool TryCapture(BitReader br)
    {
        if (SrcVersion != DwgVersion.AC1018) return false;
        uint bitsize = br.ReadRL();
        long hs = (long)ObjStart * 8 + bitsize;
        if (hs <= br.BitPosition || hs > (long)Rec.BodyEnd * 8) { Fail(br, "帧界(RL vs BodyEnd)"); return false; }
        Rec.HandleStreamStartBit = hs;
        Rec.HasHandleStream = true;
        br.ReadHandle();
        long eedStart = br.BitPosition;
        if (!DwgObjectReader.TrySkipEed(br)) { Fail(br, "EED"); return false; }
        long eedEnd = br.BitPosition;
        int numReactors = br.ReadBL();
        bool xdicMissing = br.ReadB();
        if (numReactors is < 0 or > 64) { Fail(br, $"num_reactors={numReactors}"); return false; }
        Rec.EedStartBit = eedStart;
        Rec.EedBitLen = (int)(eedEnd - eedStart);
        long xdicBit = br.BitPosition - 1;
        long headEnd = br.BitPosition;
        string desc, textDef;
        var v = new MLeaderStyleCapture.MLeaderStyleValues();
        long descStart, descEnd, tdStart, tdEnd;
        try
        {
            v.ContentType = br.ReadBS();
            v.MleaderOrder = br.ReadBS();
            v.LeaderOrder = br.ReadBS();
            v.MaxPoints = br.ReadBL();
            v.FirstSegAngle = br.ReadBD();
            v.SecondSegAngle = br.ReadBD();
            v.LeaderType = br.ReadBS();
            { long c0 = br.BitPosition; v.LeaderLineColor = DwgObjectReader.ReadCmcTriple(br); Cap.DownCmcSpans.Add((c0, br.BitPosition)); }
            v.LeaderLineWeight = br.ReadBL();
            v.EnableLanding = br.ReadB();
            v.LandingGap = br.ReadBD();
            v.EnableDogleg = br.ReadB();
            v.DoglegLength = br.ReadBD();
            descStart = br.BitPosition;
            desc = br.ReadTV();
            descEnd = br.BitPosition;
            v.ArrowHeadSize = br.ReadBD();
            tdStart = br.BitPosition;
            textDef = br.ReadTV();
            tdEnd = br.BitPosition;
            v.AttachLeft = br.ReadBS();
            v.AttachRight = br.ReadBS();
            v.TextAngleType = br.ReadBS();
            v.TextAlignType = br.ReadBS();
            { long c1 = br.BitPosition; v.TextColor = DwgObjectReader.ReadCmcTriple(br); Cap.DownCmcSpans.Add((c1, br.BitPosition)); }
            v.TextHeight = br.ReadBD();
            v.EnableTextFrame = br.ReadB();
            v.TextAlwaysLeft = br.ReadB();
            v.AlignSpace = br.ReadBD();
            { long c2 = br.BitPosition; v.BlockColor = DwgObjectReader.ReadCmcTriple(br); Cap.DownCmcSpans.Add((c2, br.BitPosition)); }
            v.BlockScaleX = br.ReadBD(); v.BlockScaleY = br.ReadBD(); v.BlockScaleZ = br.ReadBD();
            v.UseBlockScale = br.ReadB();
            v.BlockRotation = br.ReadBD();
            v.UseBlockRotation = br.ReadB();
            v.BlockConnection = br.ReadBS();
            v.Scale = br.ReadBD();
            v.IsChanged = br.ReadB();
            v.IsAnnotative = br.ReadB();
            v.BreakSize = br.ReadBD();
            if (br.BitPosition != hs) { Fail(br, "不闭合(游标未落 RL 锚)"); return false; }
            if (desc.Length > 255 || textDef.Length > 255)
            { Fail(br, $"内联串长 desc={desc.Length}/textDef={textDef.Length}"); return false; }
            if (descEnd - descStart > 8 * 260 || tdEnd - tdStart > 8 * 260)
            { Fail(br, "内联串位宽越界"); return false; }
        }
        catch (Exception ex)
        { Fail(br, $"字段区异常:{ex.GetType().Name}"); return false; }
        DwgObjectRecord.SeekTo(br, hs);
        var hOwner = br.ReadHandle();
        var hReactors = new List<DwgHandleReference>(numReactors);
        for (int i = 0; i < numReactors; i++) hReactors.Add(br.ReadHandle());
        DwgHandleReference? hXdic = null;
        if (!xdicMissing) hXdic = br.ReadHandle();
        var hTypeAxis = new DwgHandleReference[4];
        for (int i = 0; i < 4; i++)
        {
            var h = br.ReadHandle();
            if (h.Code != 5) { Fail(br, $"句柄流 类型轴[{i}] code={h.Code}≠5"); return false; }
            hTypeAxis[i] = h;
        }
        long frameEnd = (long)Rec.BodyEnd * 8;
        long tailStart = br.BitPosition;
        int padValue = 0;
        for (long p = tailStart; p < frameEnd; p++)
        {
            bool one = br.ReadB();
            padValue = (padValue << 1) | (one ? 1 : 0);
        }
        int padBits = (int)(frameEnd - tailStart);
        var hrefs = new MLeaderStyleHandleRefs { Owner = hOwner, Xdic = hXdic };
        hrefs.Reactors.AddRange(hReactors);
        hrefs.SetTypeAxis(hTypeAxis[0], hTypeAxis[1], hTypeAxis[2], hTypeAxis[3]);
        hrefs.TailBits = padBits;
        Cap.HandleRefs = hrefs;
        Cap.HandlePadBits = padBits;
        Cap.HandlePadValue = padValue;
        if (padBits > 7)
        { Fail(br, $"句柄流尾段余 {padBits} 位 > 7(非对齐余量)"); return false; }
        Cap.HandleStreamCommitted = true;
        if (padValue != 0 && padValue != (1 << padBits) - 1)
        { Fail(br, $"帧尾对齐余量 {padBits} 位非均匀(0x{padValue:X})"); return false; }
        Cap.UpCaptured = true;
        Cap.UpHeadEndBit = headEnd;
        Cap.UpEedStartBit = eedStart;
        Cap.UpEedEndBit = eedEnd;
        Cap.UpXdicBit = xdicBit;
        Cap.UpDescStartBit = descStart;
        Cap.UpDescEndBit = descEnd;
        Cap.UpTextDefStartBit = tdStart;
        Cap.UpTextDefEndBit = tdEnd;
        Cap.Desc = desc;
        Cap.TextDefault = textDef;
        v.Description = desc;
        v.TextDefault = textDef;
        Cap.Values = v;
        return true;
    }
}
