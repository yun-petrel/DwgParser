using DwgParser.Core.IO;
using static DwgParser.Core.Objects.DwgObjectStreamWriter;

namespace DwgParser.Core.Objects.Records;

internal sealed class VisualStyleCarryAc1018ToAc1021 : VisualStyleCarryPlan
{
    public override string Name => "AC1018→AC1021";

    public override bool IsCaptured(DwgObjectRecord rec) => rec.VisualStyle?.Captured == true;

    internal override bool TryCarry(BitWriter body, byte[] src, long from, long to,
        DwgObjectRecord rec, Target t)
    {
        var cap = rec.VisualStyle;
        if (cap == null || !cap.Captured) return false;
        if (rec.SourceVersion != DwgVersion.AC1018 || t.Version != DwgVersion.AC1021) return false;
        if (from != rec.CarryStartBit) return false;
        if (to != rec.HandleStreamStartBit) return false;
        var sp = cap.FieldSpans;
        if (sp.Length != 60) return false;
        long descStart = cap.BodyStartBit;
        long styleStart = cap.StreamStartBit;
        if (descStart < from + 32 || descStart >= styleStart) return false;
        if (styleStart >= to) return false;

        int typeBits = (int)(rec.CarryStartBit - (long)rec.BodyStart * 8);

        body.WriteRL(0);

        long eedEnd = rec.EedStartBit + rec.EedBitLen;
        if (rec.EedBitLen > 0 && rec.EedStartBit >= from + 32 && eedEnd <= descStart)
        {
            CopyBits(body, src, from + 32, (int)(rec.EedStartBit - from - 32));
            body.WriteBS(0);
            CopyBits(body, src, eedEnd, (int)(descStart - eedEnd));
        }
        else
        {
            CopyBits(body, src, from + 32, (int)(descStart - from - 32));
        }

        string desc = ReadDescFromCapture(src, descStart, styleStart);
        if (desc == null) return false;

        void CopySpan(int i) => CopyBits(body, src, sp[i * 2], (int)(sp[i * 2 + 1] - sp[i * 2]));
        for (int i = 0; i < 30; i++) CopySpan(i);

        body.WriteBD(0.0);
        body.WriteB(cap.InternalOnly != 0);

        long contentStart = body.BitPosition;
        WriteCentralTu(body, desc);
        int contentBits = (int)(body.BitPosition - contentStart);
        WriteStringStreamSize(body, (uint)contentBits);
        body.WriteB(true);

        rec.StringFields.Add(new DwgObjectRecord.StringField(descStart, (int)(styleStart - descStart), desc));

        int dataBits = (int)body.BitPosition - 32;
        body.PatchRL(0, (uint)(typeBits + 32 + dataBits));

        if (DebugTrace)
            System.Console.Error.WriteLine(
                $"VSUP1821 h={rec.Handle:X} {rec.SourceVersion}→{t.Version} body={body.BitPosition}位 " +
                $"串流{contentBits}位 EED=丢弃 RL={(uint)(typeBits + 32 + dataBits)}");
        return true;
    }

    internal static string? ReadDescFromCapture(byte[] src, long descStart, long styleStart)
    {
        try
        {
            var br = new BitReader(src);
            DwgObjectRecord.SeekTo(br, descStart);
            br.UnicodeStrings = false;
            string s = br.ReadTV();
            return s;
        }
        catch { return null; }
    }
}
