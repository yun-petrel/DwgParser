using DwgParser.Core.IO;
using static DwgParser.Core.Objects.DwgObjectStreamWriter;

namespace DwgParser.Core.Objects.Records;

internal sealed class LtypeCarryPreR2007Up : LtypeCarryPlan
{
    public override string Name => "AC1015/AC1018→R2007+";

    public override bool IsCaptured(DwgObjectRecord rec) => rec.LType?.HasAnchors == true;

    internal override bool TryCarry(BitWriter body, byte[] src, long from, long to,
        DwgObjectRecord rec, Target t)
    {
        var cap = rec.LType;
        if (rec.StringFields.Count != 2 || cap == null
            || cap.SaStartBit <= 0 || cap.PatStartBit <= 0)
            return false;
        bool carriesRl = PrefixHasRl(rec.SourceVersion) && !t.R2010Plus;
        long ownStart = rec.CarryStartBit + 32;
        var name = rec.StringFields[0];
        var desc = rec.StringFields[1];
        long nameStart = name.Start, descStart = name.Start + name.BitLen;
        if (desc.Start != descStart || descStart > cap.PatStartBit) return false;
        if (rec.HandleStreamStartBit != cap.SaStartBit + 2048) return false;

        int prefixBits = (int)(nameStart - ownStart);
        bool srcPre2004 = rec.SourceVersion < DwgVersion.AC1018;
        bool srcLacksDs = rec.SourceVersion < DwgVersion.AC1027;
        int patBits = (int)(cap.SaStartBit - cap.PatStartBit);
        var tw = new BitWriter(16);
        WriteCentralTu(tw, name.Value);
        WriteCentralTu(tw, desc.Value);
        int tuBits = (int)tw.BitPosition;
        int dataBits = prefixBits + (srcPre2004 ? 1 : 0) + (srcLacksDs && t.R2013Plus ? 1 : 0)
            + 2 + patBits + tuBits + 17;

        bool headAlreadyWritten;
        if (carriesRl)
        {
            if (from != rec.CarryStartBit) return false;
            headAlreadyWritten = false;
        }
        else if (from == ownStart)
        {
            headAlreadyWritten = false;
        }
        else if (from == nameStart)
        {
            headAlreadyWritten = true;
        }
        else return false;

        if (to < cap.SaStartBit + 2048) return false;

        if (carriesRl)
        {
            int typeBits = (int)(rec.CarryStartBit - (long)rec.BodyStart * 8);
            body.WriteRL((uint)(typeBits + 32 + dataBits));
        }
        if (!headAlreadyWritten)
        {
            CopyBits(body, src, ownStart, prefixBits);
            if (srcPre2004)
            {
                body.WriteB(SourceXdicRefIsNull(src, rec.HandleStreamStartBit, (long)rec.BodyEnd * 8));
            }
            if (srcLacksDs && t.R2013Plus) body.WriteB(false);
        }
        body.WriteBS((short)cap.XrefResolved);
        CopyBits(body, src, cap.PatStartBit, patBits);
        WriteCentralTu(body, name.Value);
        WriteCentralTu(body, desc.Value);
        WriteStringStreamSize(body, (uint)tuBits);
        body.WriteB(true);
        if (DebugTrace)
            System.Console.Error.WriteLine(
                $"LTUP h={rec.Handle:X} {rec.SourceVersion}→{t.Version} " +
                $"from={from} hsWritten={headAlreadyWritten} data {to - ownStart}→{dataBits} 位" +
                "（挖 strings_area 2048 + name/desc 集中化）");
        return true;
    }
}
