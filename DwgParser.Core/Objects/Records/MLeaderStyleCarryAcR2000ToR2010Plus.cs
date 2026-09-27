using DwgParser.Core.IO;
using static DwgParser.Core.Objects.DwgObjectStreamWriter;

namespace DwgParser.Core.Objects.Records;

internal sealed class MLeaderStyleCarryAcR2000ToR2010Plus : MLeaderStyleCarryPlan
{
    public override string Name => "AC1015→R2010+";

    public override bool IsCaptured(DwgObjectRecord rec) => true;

    private enum Kind { BL, BD, BS, B, Cmc }

    private static readonly Kind[] K =
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

    private struct Src
    {
        public long Own, EedS, EedE, DS, DE, TS, TE, Hs;
        public uint Rl;
        public string D, T;
        public long[] Sp;
        public short[] Cm;
    }

    internal override bool TryCarry(BitWriter body, byte[] src, long from, long to,
        DwgObjectRecord rec, Target t)
        => TryCarryFrameStart(body, src, from, to, rec, t);

    internal bool TryCarryFrameStart(BitWriter body, byte[] src, long from, long to,
        DwgObjectRecord rec, Target t)
    {
        if (rec.SourceVersion != DwgVersion.AC1015) return false;
        if (!t.R2010Plus) return false;
        if (from != rec.CarryStartBit + 32) return false;
        if (!Read(src, rec, from, to, out var s)) return false;

        CopyBits(body, src, s.Own, (int)(s.EedS - s.Own));
        body.WriteBS(0);
        CopyBits(body, src, s.EedE, (int)(s.Sp[0] - s.EedE));
        body.WriteB(true);
        if (t.R2013Plus) body.WriteB(false);
        body.WriteBS(2);

        for (int i = 0; i < K.Length; i++)
        {
            if (K[i] != Kind.Cmc)
            {
                CopyBits(body, src, s.Sp[i * 2], (int)(s.Sp[i * 2 + 1] - s.Sp[i * 2]));
                continue;
            }
            body.WriteBS(0);
            body.WriteBL(unchecked((int)VisualStyleCarryAcR2000ToAc1021.UpgradedRgb(s.Cm[i])));
            body.WriteRC(0);
        }

        body.WriteBS(0);
        body.WriteBS(9);
        body.WriteBS(9);
        if (t.R2013Plus)
            body.WriteB(false);

        long ss = body.BitPosition;
        WriteCentralTu(body, s.D); WriteCentralTu(body, s.T);
        int sb = (int)(body.BitPosition - ss);
        WriteStringStreamSize(body, (uint)sb); body.WriteB(true);

        rec.StringFields.Add(new DwgObjectRecord.StringField(s.DS, (int)(s.DE - s.DS), s.D));

        rec.HandleStreamStartBit = s.Hs;
        rec.HasHandleStream = true;
        rec.MLeader ??= new MLeaderStyleCapture();
        rec.MLeader.MLeaderStyleR2000ToR2010 = true;

        if (DebugTrace)
            System.Console.Error.WriteLine(
                $"MLSUP1524 h={rec.Handle:X} {rec.SourceVersion}→{t.Version} body={body.BitPosition}位 " +
                $"EED剥59(cv10+attach22{(t.R2013Plus ? "+textExt1" : "")}) 串流{sb}位 desc='{s.D}' td='{s.T}'");
        return true;
    }

    private static bool Read(byte[] src, DwgObjectRecord rec, long from, long to, out Src s)
    {
        s = default; var sp = new long[K.Length * 2]; var cm = new short[K.Length];
        try
        {
            var br = new BitReader(src, 0);
            DwgObjectRecord.SeekTo(br, rec.CarryStartBit);
            uint rl = br.ReadRL(); long own = rec.CarryStartBit + 32;
            long hs = (long)rec.BodyStart * 8 + rl;
            if (rl == 0 || hs <= own || hs > (long)rec.BodyEnd * 8 || to != hs) return false;
            if (own != from) return false;
            br.SetPosition((int)(own >> 3), (int)(own & 7));
            br.ReadHandle();
            long es = br.BitPosition;
            while (true)
            {
                int z = br.ReadBS();
                if (z < 0 || z > 4096) return false;
                if (z == 0) break;
                br.ReadHandle();
                for (int j = 0; j < z; j++) br.ReadRC();
                if (br.BitPosition > hs) return false;
            }
            long ee = br.BitPosition; int nr = br.ReadBL();
            if (nr < 0 || nr > 8) return false;
            string d = "", t2 = ""; long ds = -1, de = -1, ts = -1, te2 = -1;
            for (int i = 0; i < K.Length; i++)
            {
                if (i == 13) { br.UnicodeStrings = false; ds = br.BitPosition; d = br.ReadTV(); de = br.BitPosition; }
                if (i == 14) { br.UnicodeStrings = false; ts = br.BitPosition; t2 = br.ReadTV(); te2 = br.BitPosition; }
                long p0 = br.BitPosition;
                switch (K[i])
                {
                    case Kind.BL: br.ReadBL(); break;
                    case Kind.BD:
                        double dv = br.ReadBD();
                        if (double.IsNaN(dv)) return false;
                        break;
                    case Kind.BS: br.ReadBS(); break;
                    case Kind.B: br.ReadB(); break;
                    case Kind.Cmc:
                        short ex = br.ReadBS();
                        if (ex < 0) return false;
                        cm[i] = ex; break;
                }
                sp[i * 2] = p0; sp[i * 2 + 1] = br.BitPosition;
                if (br.BitPosition > hs) return false;
            }
            if (ds < 0 || ts < 0 || br.BitPosition != hs) return false;
            if (d.Length > 255 || t2.Length > 255) return false;
            s = new Src { Own = own, EedS = es, EedE = ee, DS = ds, DE = de, TS = ts, TE = te2, Hs = hs, Rl = rl, D = d, T = t2, Sp = sp, Cm = cm };
            return true;
        }
        catch { return false; }
    }
}
