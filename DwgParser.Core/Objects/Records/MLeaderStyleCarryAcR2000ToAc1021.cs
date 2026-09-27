using DwgParser.Core.IO;
using static DwgParser.Core.Objects.DwgObjectStreamWriter;

namespace DwgParser.Core.Objects.Records;

internal sealed class MLeaderStyleCarryAcR2000ToAc1021 : MLeaderStyleCarryPlan
{
    public override string Name => "AC1015→AC1021";

    public override bool IsCaptured(DwgObjectRecord rec) => true;

    private enum Kind { BL, BD, BS, B, Cmc }

    private static readonly Kind[] Kinds =
    {
        Kind.BS,
        Kind.BS,
        Kind.BS,
        Kind.BL,
        Kind.BD,
        Kind.BD,
        Kind.BS,
        Kind.Cmc,
        Kind.BL,
        Kind.B,
        Kind.BD,
        Kind.B,
        Kind.BD,
        Kind.BD,
        Kind.BS,
        Kind.BS,
        Kind.BS,
        Kind.BS,
        Kind.Cmc,
        Kind.BD,
        Kind.B,
        Kind.B,
        Kind.BD,
        Kind.Cmc,
        Kind.BD,
        Kind.BD,
        Kind.BD,
        Kind.B,
        Kind.BD,
        Kind.B,
        Kind.BS,
        Kind.BD,
        Kind.B,
        Kind.B,
        Kind.BD,
    };

    private struct Source
    {
        public long OwnStart, EedStart, EedEnd, DescStart, DescEnd, TdStart, TdEnd, Hs;
        public uint Rl;
        public string? Desc, TextDef;
        public long[]? Spans;
        public short[]? CmcIndex;
    }

    internal bool TryCarryFrameStart(BitWriter body, byte[] src, long from, long to,
        DwgObjectRecord rec, Target t)
    {
        string why = "-";
        bool ok = TryCarryCore(body, src, from, to, rec, t, ref why);
        if (DebugTrace)
            System.Console.Error.WriteLine(
                $"MLS15 h={rec.Handle:X} ok={ok} why={why ?? "-"} src={rec.SourceVersion} tgt={t.Version} " +
                $"from={from} carry={rec.CarryStartBit} to={to} LT={rec.LogicalType} SF={rec.StringFields.Count}");
        return ok;
    }

    internal override bool TryCarry(BitWriter body, byte[] src, long from, long to,
        DwgObjectRecord rec, Target t)
        => TryCarryFrameStart(body, src, from, to, rec, t);

    private bool TryCarryCore(BitWriter body, byte[] src, long from, long to,
        DwgObjectRecord rec, Target t, ref string why)
    {
        if (rec.SourceVersion != DwgVersion.AC1015) { why = "src≠AC1015"; return false; }
        if (t.Version != DwgVersion.AC1021) { why = "tgt≠AC1021"; return false; }
        if (from != rec.CarryStartBit) { why = $"from≠carry({from} vs {rec.CarryStartBit})"; return false; }
        if (!PrefixHasRl(rec.SourceVersion)) { why = "无前置RL"; return false; }
        if (t.R2010Plus) { why = "R2010+目标"; return false; }
        if (!TryReadSource(src, rec, from, to, out var s, ref why)) return false;
        if (s.Spans == null || s.CmcIndex == null || s.Desc == null || s.TextDef == null) { why = "捕获字段缺失"; return false; }

        int typeBits = (int)(rec.CarryStartBit - (long)rec.BodyStart * 8);
        var spans = s.Spans;
        var cmc = s.CmcIndex;
        body.WriteRL(0);
        CopyBits(body, src, s.OwnStart, (int)(s.EedStart - s.OwnStart));
        var eedProbe = new BitWriter(16);
        if (TryCarryEedR2007Plus(eedProbe, src, s.EedStart, s.EedEnd, rec))
            TryCarryEedR2007Plus(body, src, s.EedStart, s.EedEnd, rec);
        else
            CopyBits(body, src, s.EedStart, (int)(s.EedEnd - s.EedStart));
        CopyBits(body, src, s.EedEnd, (int)(spans[0] - s.EedEnd));
        body.WriteB(SourceXdicRefIsNull(src, s.Hs, (long)rec.BodyEnd * 8));
        for (int i = 0; i < Kinds.Length; i++)
        {
            if (Kinds[i] != Kind.Cmc)
            {
                CopyBits(body, src, spans[i * 2], (int)(spans[i * 2 + 1] - spans[i * 2]));
                continue;
            }
            body.WriteBS(0);
            body.WriteBL(unchecked((int)VisualStyleCarryAcR2000ToAc1021.UpgradedRgb(cmc[i])));
            body.WriteRC(0);
        }

        long ssStart = body.BitPosition;
        WriteCentralTu(body, s.Desc);
        WriteCentralTu(body, s.TextDef);
        int ssBits = (int)(body.BitPosition - ssStart);
        WriteStringStreamSize(body, (uint)ssBits);
        body.WriteB(true);

        rec.StringFields.Add(new DwgObjectRecord.StringField(
            s.DescStart, (int)(s.DescEnd - s.DescStart), s.Desc));
        int dataBits = (int)body.BitPosition - 32;
        uint newRl = (uint)(typeBits + 32 + dataBits);
        body.PatchRL(0, newRl);

        if (DebugTrace)
            System.Console.Error.WriteLine(
                $"MLSUP1521 h={rec.Handle:X} {rec.SourceVersion}→{t.Version} RL {s.Rl}->{newRl} " +
                $"串流{ssBits}位 EED=丢弃 desc='{s.Desc}' td='{s.TextDef}' xdic={SourceXdicRefIsNull(src, s.Hs, (long)rec.BodyEnd * 8)}");
        return true;
    }

    private static bool TryReadSource(byte[] src, DwgObjectRecord rec, long from, long to,
        out Source s, ref string why)
    {
        s = default;
        var spans = new long[Kinds.Length * 2];
        var cmc = new short[Kinds.Length];
        try
        {
            var br = new BitReader(src, 0);
            DwgObjectRecord.SeekTo(br, from);
            uint rl = br.ReadRL();
            long ownStart = from + 32;
            long hs = (long)rec.BodyStart * 8 + rl;
            if (rl == 0 || hs <= ownStart || hs > (long)rec.BodyEnd * 8)
            { why = $"RL越界(rl={rl} hs={hs} bodyEnd={(long)rec.BodyEnd * 8})"; return false; }
            if (to != hs) { why = $"to≠hs({to} vs {hs})"; return false; }
            br.SetPosition((int)(ownStart >> 3), (int)(ownStart & 7));

            br.ReadHandle();
            long eedStart = br.BitPosition;
            while (true)
            {
                int esz = br.ReadBS();
                if (esz < 0 || esz > 4096) { why = $"EED尺寸({esz})"; return false; }
                if (esz == 0) break;
                br.ReadHandle();
                for (int i = 0; i < esz; i++) br.ReadRC();
                if (br.BitPosition > hs) { why = "EED越界"; return false; }
            }
            long eedEnd = br.BitPosition;
            int numReactors = br.ReadBL();
            if (numReactors is < 0 or > 64) { why = $"nreact({numReactors})"; return false; }

            string? desc = null, textDef = null;
            long descStart = -1, descEnd = -1, tdStart = -1, tdEnd = -1;
            for (int i = 0; i < Kinds.Length; i++)
            {
                if (i == 13)
                {
                    br.UnicodeStrings = false;
                    descStart = br.BitPosition;
                    desc = br.ReadTV();
                    br.UnicodeStrings = false;
                    if (desc == null || desc.Length > 255) { why = $"desc('{desc}')"; return false; }
                    descEnd = br.BitPosition;
                }
                if (i == 14)
                {
                    br.UnicodeStrings = false;
                    tdStart = br.BitPosition;
                    textDef = br.ReadTV();
                    br.UnicodeStrings = false;
                    tdEnd = br.BitPosition;
                }
                long p0 = br.BitPosition;
                switch (Kinds[i])
                {
                    case Kind.BL: br.ReadBL(); break;
                    case Kind.BD:
                        double d = br.ReadBD();
                        if (double.IsNaN(d)) { why = $"BD NaN @{i}"; return false; }
                        break;
                    case Kind.BS: br.ReadBS(); break;
                    case Kind.B: br.ReadB(); break;
                    case Kind.Cmc:
                        short idx = br.ReadBS();
                        if (idx < 0) { why = $"CMC idx({idx}) @{i}"; return false; }
                        cmc[i] = idx;
                        break;
                }
                spans[i * 2] = p0;
                spans[i * 2 + 1] = br.BitPosition;
                if (br.BitPosition > hs) { why = $"字段越界 @{i}"; return false; }
            }
            if (descStart < 0 || tdStart < 0) { why = "串定位失败"; return false; }
            if (br.BitPosition != hs) { why = $"Δ={br.BitPosition - hs}"; return false; }

            s = new Source
            {
                OwnStart = ownStart, EedStart = eedStart, EedEnd = eedEnd,
                DescStart = descStart, DescEnd = descEnd, TdStart = tdStart, TdEnd = tdEnd,
                Hs = hs, Rl = rl, Desc = desc, TextDef = textDef,
                Spans = spans, CmcIndex = cmc,
            };
            return true;
        }
        catch (System.Exception ex) { why = $"异常 {ex.GetType().Name}"; return false; }
    }

    internal static List<long>? TryLocateShortCmcBits(byte[] src, DwgObjectRecord rec)
    {
        var head = new BitReader(src, rec.BodyStart);
        _ = head.ReadBS();
        long frameStart = head.BitPosition;
        uint rl = head.ReadRL();
        if (rl == 0 || rl > ((long)rec.BodyEnd - rec.BodyStart + 8) * 8) return null;
        long hs = (long)rec.BodyStart * 8 + rl;
        string why = "-";
        if (!TryReadSource(src, rec, frameStart, hs, out Source s, ref why) || s.Spans is null) return null;
        var bits = new List<long>(3);
        for (int i = 0; i < Kinds.Length; i++)
            if (Kinds[i] == Kind.Cmc) bits.Add(s.Spans[i * 2]);
        return bits;
    }
}
