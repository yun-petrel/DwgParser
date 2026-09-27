using DwgParser.Core.IO;

namespace DwgParser.Core.Objects.Records;

internal sealed class MLeaderStyleAcR2000 : MLeaderStyleRecord
{
    public override string Family => "R2000";

    public MLeaderStyleAcR2000(ulong handle, DwgVersion sourceVersion,
        DwgObjectRecord record, int objStart) : base(handle, sourceVersion, record, objStart) { }

    private const int LineColorSlot = 7, TextColorSlot = 18, BlockColorSlot = 23;

    public override bool TryCapture(BitReader br)
    {
        if (SrcVersion != DwgVersion.AC1015) { Fail(br, "非 AC1015 源"); return false; }

        uint rl = br.ReadRL();
        long hs = (long)ObjStart * 8 + rl;
        if (rl == 0 || hs <= br.BitPosition || hs > (long)Rec.BodyEnd * 8)
        { Fail(br, $"帧界(RL vs BodyEnd)(rl={rl})"); return false; }

        br.ReadHandle();
        while (true)
        {
            int esz = br.ReadBS();
            if (esz < 0 || esz > 4096) { Fail(br, $"EED尺寸({esz})"); return false; }
            if (esz == 0) break;
            br.ReadHandle();
            for (int i = 0; i < esz; i++) br.ReadRC();
            if (br.BitPosition > hs) { Fail(br, "EED越界"); return false; }
        }
        int numReactors = br.ReadBL();
        if (numReactors is < 0 or > 64) { Fail(br, $"num_reactors={numReactors}"); return false; }

        var v = new MLeaderStyleCapture.MLeaderStyleValues();
        string desc, textDef;
        try
        {
            v.ContentType = br.ReadBS();
            v.MleaderOrder = br.ReadBS();
            v.LeaderOrder = br.ReadBS();
            v.MaxPoints = br.ReadBL();
            v.FirstSegAngle = Bd(br);
            v.SecondSegAngle = Bd(br);
            v.LeaderType = br.ReadBS();
            v.LeaderLineColor = ShortCmc(br, LineColorSlot);
            v.LeaderLineWeight = br.ReadBL();
            v.EnableLanding = br.ReadB();
            v.LandingGap = Bd(br);
            v.EnableDogleg = br.ReadB();
            v.DoglegLength = Bd(br);
            desc = Tv(br);
            v.ArrowHeadSize = Bd(br);
            textDef = Tv(br);
            v.AttachLeft = br.ReadBS();
            v.AttachRight = br.ReadBS();
            v.TextAngleType = br.ReadBS();
            v.TextAlignType = br.ReadBS();
            v.TextColor = ShortCmc(br, TextColorSlot);
            v.TextHeight = Bd(br);
            v.EnableTextFrame = br.ReadB();
            v.TextAlwaysLeft = br.ReadB();
            v.AlignSpace = Bd(br);
            v.BlockColor = ShortCmc(br, BlockColorSlot);
            v.BlockScaleX = Bd(br);
            v.BlockScaleY = Bd(br);
            v.BlockScaleZ = Bd(br);
            v.UseBlockScale = br.ReadB();
            v.BlockRotation = Bd(br);
            v.UseBlockRotation = br.ReadB();
            v.BlockConnection = br.ReadBS();
            v.Scale = Bd(br);
            v.IsChanged = br.ReadB();
            v.IsAnnotative = br.ReadB();
            v.BreakSize = Bd(br);
            if (br.BitPosition != hs)
            { Fail(br, $"不闭合(游标未落 RL 锚)(Δ={br.BitPosition - hs})"); return false; }
            if (desc.Length > 255 || textDef.Length > 255)
            { Fail(br, $"内联串长 desc={desc.Length}/textDef={textDef.Length}"); return false; }
        }
        catch (Exception ex)
        { Fail(br, $"字段区异常:{ex.GetType().Name}"); return false; }

        v.Description = desc;
        v.TextDefault = textDef;
        Cap.Values = v;
        Cap.ValuesCommitted = true;
        if (System.Environment.GetEnvironmentVariable("DWGDBG") == "1")
            System.Console.Error.WriteLine(
                $"MLS15R h={Handle:X} {SrcVersion} {Family} hs={hs} end={br.BitPosition} " +
                $"ct={v.ContentType} mo={v.MleaderOrder} lo={v.LeaderOrder} mp={v.MaxPoints} " +
                $"a1={v.FirstSegAngle:0.####} a2={v.SecondSegAngle:0.####} ty={v.LeaderType} " +
                $"lc={v.LeaderLineColor.Idx} lw={v.LeaderLineWeight} land={v.EnableLanding}/{v.LandingGap:0.####} " +
                $"dog={v.EnableDogleg}/{v.DoglegLength:0.####} arrow={v.ArrowHeadSize:0.####} " +
                $"at={v.AttachLeft}/{v.AttachRight} tat={v.TextAngleType} tai={v.TextAlignType} " +
                $"tc={v.TextColor.Idx} th={v.TextHeight:0.####} tf={v.EnableTextFrame} tal={v.TextAlwaysLeft} " +
                $"asp={v.AlignSpace:0.####} bc={v.BlockColor.Idx} bs={v.BlockScaleX:0.####}/{v.BlockScaleY:0.####}" +
                $"/{v.BlockScaleZ:0.####} ubs={v.UseBlockScale} bro={v.BlockRotation:0.####} " +
                $"ubr={v.UseBlockRotation} bcn={v.BlockConnection} sc={v.Scale:0.####} chg={v.IsChanged} " +
                $"ann={v.IsAnnotative} brk={v.BreakSize:0.####} desc='{desc}' td='{textDef}'");
        return true;
    }

    private static (int Idx, uint Rgb, byte Flag) ShortCmc(BitReader br, int slot)
    {
        short idx = br.ReadBS();
        if (idx < 0) throw new InvalidDataException($"CMC idx({idx}) @{slot}");
        return (idx, 0u, 0);
    }

    private static double Bd(BitReader br)
    {
        double d = br.ReadBD();
        if (double.IsNaN(d)) throw new InvalidDataException("BD NaN");
        return d;
    }

    private static string Tv(BitReader br)
    {
        string s = br.ReadTV();
        if (s.Length > 255) throw new InvalidDataException($"TV 长 {s.Length}");
        return s;
    }
}
