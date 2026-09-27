using DwgParser.Core.IO;
using static DwgParser.Core.Objects.DwgObjectStreamWriter;

namespace DwgParser.Core.Objects.Records;

internal sealed class ViewStyleCarryAcR2018Down : ViewStyleCarryPlan
{
    private const short TypeSectionViewStyle = 510;

    public override string Name => "VSTY:R2018降版(剥display_name+viewstyle_flags)";

    public override bool IsCaptured(DwgObjectRecord rec) => rec.HasHandleStream;

    internal override bool TryCarry(BitWriter body, DwgObjectRecord rec, byte[] src,
        Target t, out BitWriter? handles)
    {
        handles = null;
        long start = rec.CarryStartBit;
        long ss = rec.StringStreamStartBit;
        long hs = rec.HandleStreamStartBit;
        long hsEnd = (long)rec.BodyEnd * 8;
        if (start <= 0 || ss <= start || hs <= ss || hs >= hsEnd) return false;
        try
        {
            var br = new BitReader(src, 0);
            DwgObjectRecord.SeekTo(br, start);
            br.ReadHandle();
            if (!SkipEedWriter(br)) return false;
            long numReactors = br.ReadBL();
            if (numReactors is < 0 or > 100000) return false;
            br.ReadB();
            long afterXdic = br.BitPosition;
            long dsStart = -1, dsEnd = -1;
            if (rec.SourceVersion >= DwgVersion.AC1027)
            {
                dsStart = br.BitPosition;
                br.ReadB();
                dsEnd = br.BitPosition;
            }
            br.ReadBS();
            br.ReadB();
            long vfStart = br.BitPosition;
            br.ReadBL();
            long vfEnd = br.BitPosition;
            if (vfEnd > ss) return false;

            long footer = hs - 17;
            long dataBits = footer - ss;
            if (dataBits < 0 || dataBits > 0x7FFF) return false;
            var sb = new BitReader(src, 0);
            DwgObjectRecord.SeekTo(sb, ss);
            long dnStart = -1, dnEnd = -1;
            int tuCount = 0;
            string[] firstTwo = new string[2];
            while (true)
            {
                long cur = sb.BitPosition;
                int len = sb.ReadBS();
                if (len < 0 || len > 512) return false;
                var sbuf = new System.Text.StringBuilder(len);
                for (int i = 0; i < len; i++)
                {
                    int cu = sb.ReadRS();
                    if (tuCount < 2) sbuf.Append((char)cu);
                }
                long nxt = sb.BitPosition;
                if (nxt > footer) return false;
                if (tuCount < 2) firstTwo[tuCount] = sbuf.ToString();
                tuCount++;
                if (tuCount == 2) { dnStart = cur; dnEnd = nxt; }
                if (nxt == footer) break;
            }
            if (tuCount < 2 || dnStart < 0 || dnEnd > footer) return false;
            int expectTu = rec.LogicalType == TypeSectionViewStyle ? 5 : 4;
            if (tuCount != expectTu) return false;

            long headEnd = dsStart >= 0 ? dsEnd : afterXdic;
            CopyCommonHead(body, src, start, headEnd, dsStart, dsEnd, afterXdic, t);
            CopyBits(body, src, headEnd, (int)(vfStart - headEnd));
            CopyBits(body, src, vfEnd, (int)(ss - vfEnd));
            CopyBits(body, src, ss, (int)(dnStart - ss));
            CopyBits(body, src, dnEnd, (int)(footer - dnEnd));
            long newBits = dataBits - (dnEnd - dnStart);
            WriteStringStreamSize(body, (uint)newBits);
            body.WriteB(ReadSrcBit(src, hs - 1));

            var hw = new BitWriter(16);
            long dataEndBit = (long)rec.BodyEnd * 8;
            long meaningful = LastMeaningfulHandleEnd(src, hs, dataEndBit);
            if (meaningful <= hs || meaningful > dataEndBit) meaningful = dataEndBit;
            CopyBits(hw, src, hs, (int)(meaningful - hs));
            handles = hw;

            if (DebugTrace)
                System.Console.Error.WriteLine(
                    $"VSDROP h={rec.Handle:X} t={rec.TypeCode} vf=[{vfStart},{vfEnd})={vfEnd - vfStart}b " +
                    $"tuCount={tuCount} dn=\"{firstTwo[1]}\"(TU1) desc=\"{firstTwo[0]}\"(TU0) " +
                    $"dataBits {dataBits}→{newBits}");
            return true;
        }
        catch
        {
            handles = null;
            return false;
        }
    }
}
