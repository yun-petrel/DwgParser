using DwgParser.Core.IO;
using static DwgParser.Core.Objects.DwgObjectStreamWriter;

namespace DwgParser.Core.Objects.Records;

internal sealed class ViewStyleCarryToAc1018 : ViewStyleCarryPlan
{
    private const short TypeDetailViewStyle = 511;

    public override string Name => "VSTY:R2010+→AC1018(三串内联)";

    public override bool IsCaptured(DwgObjectRecord rec)
        => rec.HasHandleStream && rec.StringStreamStartBit > 0;

    private static bool ReadCmc(BitReader br)
    {
        short idx = br.ReadBS();
        br.ReadBL();
        byte flag = br.ReadRC();
        return idx is >= 0 and <= 256 && flag == 0;
    }

    internal override bool TryCarry(BitWriter body, DwgObjectRecord rec, byte[] src,
        Target t, out BitWriter? handles)
    {
        handles = null;
        if (rec.LogicalType != TypeDetailViewStyle) return false;
        long start = rec.CarryStartBit
            + (rec.SourceVersion.IsR2010Plus() ? 0 : 32);
        long ss = rec.StringStreamStartBit;
        long hs = rec.HandleStreamStartBit;
        long hsEnd = (long)rec.BodyEnd * 8;
        if (start <= 0 || ss <= start || hs <= ss || hs >= hsEnd) return false;
        try
        {
            var br = new BitReader(src, 0);
            DwgObjectRecord.SeekTo(br, start);
            br.ReadHandle();
            long eedStart = br.BitPosition;
            if (!SkipEedWriter(br)) return false;
            int eedLen = (int)(br.BitPosition - eedStart);
            long numReactors = br.ReadBL();
            if (numReactors is < 0 or > 100000) return false;
            bool xdic = br.ReadB();
            long afterXdic = br.BitPosition;
            long dsStart = -1, dsEnd = -1;
            if (rec.SourceVersion >= DwgVersion.AC1027)
            {
                dsStart = br.BitPosition;
                br.ReadB();
                dsEnd = br.BitPosition;
            }
            long mvStart = dsStart >= 0 ? dsEnd : afterXdic;
            if (br.ReadBS() != 0) return false;
            long descIns = br.BitPosition;
            br.ReadB();
            long vfStart = br.BitPosition, vfEnd = br.BitPosition;
            if (rec.SourceVersion >= DwgVersion.AC1032)
            {
                vfStart = br.BitPosition;
                br.ReadBL();
                vfEnd = br.BitPosition;
            }
            if (br.ReadBS() != 0) return false;
            br.ReadBL();
            if (!ReadCmc(br)) return false;
            br.ReadBD();
            long exIns = br.BitPosition;
            br.ReadBD();
            br.ReadRC();
            if (!ReadCmc(br)) return false;
            br.ReadBD();
            br.ReadBL();
            if (!ReadCmc(br)) return false;
            if (!ReadCmc(br)) return false;
            br.ReadBD();
            br.ReadBL();
            br.ReadBD();
            br.ReadBL();
            long patIns = br.BitPosition;
            br.ReadBL();
            if (!ReadCmc(br)) return false;
            br.ReadBL();
            if (!ReadCmc(br)) return false;
            br.ReadRC();
            long tailEnd = br.BitPosition;
            if (tailEnd != ss) return false;
            if (!(descIns < exIns && exIns < patIns && patIns < tailEnd)) return false;

            long footer = hs - 17;
            if (footer < ss) return false;
            var sb = new BitReader(src, 0);
            DwgObjectRecord.SeekTo(sb, ss);
            var tu = new System.Collections.Generic.List<string>();
            while (true)
            {
                int len = sb.ReadBS();
                if (len < 0 || len > 512) return false;
                var buf = new System.Text.StringBuilder(len);
                for (int i = 0; i < len; i++) buf.Append((char)sb.ReadRS());
                long nxt = sb.BitPosition;
                if (nxt > footer) return false;
                tu.Add(buf.ToString());
                if (nxt == footer) break;
            }
            bool hasVf = rec.SourceVersion >= DwgVersion.AC1032;
            int iDesc = 0, iEx = hasVf ? 2 : 1, iPat = hasVf ? 3 : 2;
            if (tu.Count != iPat + 1) return false;

            CopyBits(body, src, start, (int)(eedStart - start));
            if (!TryWriteEedPreR2007Range(body, src, eedStart, eedLen))
                CopyBits(body, src, eedStart, eedLen);
            body.WriteBL((int)numReactors);
            body.WriteB(xdic);
            CopyBits(body, src, mvStart, (int)(descIns - mvStart));
            body.WriteTV(tu[iDesc]);
            CopyBits(body, src, descIns, (int)(vfStart - descIns));
            CopyBits(body, src, vfEnd, (int)(exIns - vfEnd));
            body.WriteTV(tu[iEx]);
            CopyBits(body, src, exIns, (int)(patIns - exIns));
            body.WriteTV(tu[iPat]);
            CopyBits(body, src, patIns, (int)(tailEnd - patIns));

            if (DebugTrace)
                System.Console.Error.WriteLine(
                    $"VS1018 h={rec.Handle:X} {rec.SourceVersion}→{t.Version} tu={tu.Count} " +
                    $"desc=\"{tu[iDesc]}\" ex=\"{tu[iEx]}\" pat={tu[iPat].Length}c " +
                    $"ins=+{descIns}/+{exIns}/+{patIns} tailEnd={tailEnd} ss={ss} out={body.BitPosition}");
            return true;
        }
        catch
        {
            handles = null;
            return false;
        }
    }
}
