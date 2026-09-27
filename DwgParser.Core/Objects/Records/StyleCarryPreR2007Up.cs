using DwgParser.Core.IO;
using static DwgParser.Core.Objects.DwgObjectStreamWriter;

namespace DwgParser.Core.Objects.Records;

internal sealed class StyleCarryPreR2007Up : StyleCarryPlan
{
    public override string Name => "AC1015/AC1018→R2007+";

    public override bool IsCaptured(DwgObjectRecord rec) => rec.Style?.HasAnchors == true;

    internal override bool TryCarry(BitWriter body, byte[] src, long from, long to,
        DwgObjectRecord rec, Target t)
        => TryCarryImpl(body, src, from, to, rec, t, insertXdicMissing: false);

    internal bool TryCarryFrameStart(BitWriter body, byte[] src, long from, long to,
        DwgObjectRecord rec, Target t)
        => TryCarryImpl(body, src, from, to, rec, t, insertXdicMissing: true);

    internal bool TryCarryR2010FrameStart(BitWriter body, byte[] src, long from, long to,
        DwgObjectRecord rec, Target t)
        => TryCarryImpl(body, src, from, to, rec, t,
            insertXdicMissing: !rec.SourceVersion.IsR2004Plus());

    private bool TryCarryImpl(BitWriter body, byte[] src, long from, long to,
        DwgObjectRecord rec, Target t, bool insertXdicMissing)
    {
        var cap = rec.Style;
        bool carriesRl = PrefixHasRl(rec.SourceVersion) && !t.R2010Plus;
        if (cap == null || cap.NameStartBit <= 0 || cap.NameEndBit <= 0
            || cap.FontStartBit <= 0 || cap.FontEndBit <= 0
            || cap.DataEndBit <= 0)
            return false;
        if (cap.NameEndBit < cap.NameStartBit) return false;
        long ownStart = rec.CarryStartBit + 32;
        if (carriesRl)
        {
            if (from != rec.CarryStartBit) return false;
        }
        else if (from != ownStart)
            return false;
        if (cap.DataEndBit > to) return false;
        if (rec.HandleStreamStartBit != cap.DataEndBit) return false;
        if (cap.NameStartBit < ownStart) return false;
        if (cap.XrefEndBit <= 0 || cap.FontStartBit < cap.XrefEndBit) return false;

        if (carriesRl)
            body.WriteRL(0);

        long eedStart = rec.EedStartBit, eedEnd = rec.EedStartBit + rec.EedBitLen;
        bool eedOk = t.UnicodeStrings && rec.EedBitLen > 2
            && eedStart >= ownStart && eedEnd <= cap.NameStartBit;
        bool eedSimple = false, eedXdata = false;
        if (eedOk)
        {
            eedSimple = TryCarryEedR2007Plus(new BitWriter(16), src, eedStart, eedEnd, rec);
            if (!eedSimple) eedXdata = TryCarryEedXdataR2007Plus(new BitWriter(16), src, eedStart, eedEnd, rec);
        }
        if (eedSimple || eedXdata)
        {
            CopyBits(body, src, ownStart, (int)(eedStart - ownStart));
            if (eedSimple) TryCarryEedR2007Plus(body, src, eedStart, eedEnd, rec);
            else TryCarryEedXdataR2007Plus(body, src, eedStart, eedEnd, rec);
            CopyBits(body, src, eedEnd, (int)(cap.NameStartBit - eedEnd));
            if (DebugTrace)
                System.Console.Error.WriteLine(
                    $"STYUP h={rec.Handle:X} EED [{eedStart},{eedEnd}) → {(eedSimple ? "简单" : "XDATA")} 化 {body.BitPosition}位");
        }
        else
        {
            CopyBits(body, src, ownStart, (int)(cap.NameStartBit - ownStart));
        }

        if (insertXdicMissing)
        {
            body.WriteB(SourceXdicRefIsNull(src, rec.HandleStreamStartBit, (long)rec.BodyEnd * 8));
            if (System.Environment.GetEnvironmentVariable("DWGDBG") == "1")
                System.Console.Error.WriteLine(
                    $"STYXD h={rec.Handle:X} hs={rec.HandleStreamStartBit} bodyEnd={(long)rec.BodyEnd * 8} null={SourceXdicRefIsNull(src, rec.HandleStreamStartBit, (long)rec.BodyEnd * 8)}");
        }
        if (t.R2013Plus && rec.SourceVersion < DwgVersion.AC1027)
        {
            body.WriteB(false);
        }
        if (DebugTrace) System.Console.Error.WriteLine($"STYSTAGE h={rec.Handle:X} prefix后={body.BitPosition}");
        body.WriteBS((short)cap.XrefResolved);
        if (DebugTrace) System.Console.Error.WriteLine($"STYSTAGE h={rec.Handle:X} xref后={body.BitPosition} xrefEnd={cap.XrefEndBit} fontStart={cap.FontStartBit}");
        CopyBits(body, src, cap.XrefEndBit, (int)(cap.FontStartBit - cap.XrefEndBit));

        long contentStart = body.BitPosition;
        WriteCentralTu(body, cap.Name);
        WriteCentralTu(body, cap.Font);
        WriteCentralTu(body, cap.BigFont);
        int contentBits = (int)(body.BitPosition - contentStart);
        WriteStringStreamSize(body, (uint)contentBits);
        body.WriteB(true);

        if (carriesRl)
        {
            int typeBits = (int)(rec.CarryStartBit - (long)rec.BodyStart * 8);
            int dataBits = (int)body.BitPosition - 32;
            body.PatchRL(0, (uint)(typeBits + 32 + dataBits));
        }
        if (DebugTrace)
            System.Console.Error.WriteLine(
                $"STYUP h={rec.Handle:X} {rec.SourceVersion}→{t.Version} xref→单BS 串流{contentBits}位 body={body.BitPosition} SF={rec.StringFields.Count} ins={insertXdicMissing}");
        return true;
    }
}
