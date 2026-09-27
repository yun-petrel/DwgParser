using DwgParser.Core;
using DwgParser.Core.IO;
using static DwgParser.Core.Objects.DwgObjectStreamWriter;

namespace DwgParser.Core.Objects.Records;

internal sealed class MLineStyleCarryPreR2007ToAc1021
{
    public string Name => "MLINESTYLE:AC1015->AC1021";

    public bool IsCaptured(DwgObjectRecord rec)
        => rec.MLineStyle is { Captured: true, SemanticCaptured: true, Lines.Length: > 0 }
           && !string.IsNullOrEmpty(rec.MLineStyle!.Name);

    internal bool TryCarryFrameStart(BitWriter body, byte[] src, long from, long to,
        DwgObjectRecord rec, Target t)
    {
        if (t.Version != DwgVersion.AC1021 || !t.UnicodeStrings) return false;
        if (rec.SourceVersion != DwgVersion.AC1015) return false;
        var cap = rec.MLineStyle;
        if (cap is not { Captured: true, SemanticCaptured: true } || cap.Lines is not { Length: > 0 } ls)
            return false;
        if (string.IsNullOrEmpty(cap.Name) || from != rec.CarryStartBit) return false;
        if (cap.FillFlag != 0) return false;
        foreach (var l in ls) if (l.ColorFlag != 0) return false;
        uint fillRgb = UpgradedCmcRgb(cap.FillIdx);
        if (fillRgb == 0) return false;
        var lineRgb = new uint[ls.Length];
        for (int i = 0; i < ls.Length; i++)
            if ((lineRgb[i] = UpgradedCmcRgb(ls[i].ColorIdx)) == 0) return false;

        long ownStart = rec.CarryStartBit + 32;
        var br = new BitReader(src);
        DwgObjectRecord.SeekTo(br, ownStart);
        br.ReadHandle();
        long eedStart = br.BitPosition;
        if (!DwgObjectReader.TrySkipEed(br)) return false;
        if (br.BitPosition - eedStart > 2) return false;
        if (br.ReadBL() is < 0 or > 8) return false;
        long headEnd = br.BitPosition;
        body.WriteRL(0);
        CopyBits(body, src, ownStart, (int)(headEnd - ownStart));
        body.WriteB(SourceXdicRefIsNull(src, rec.HandleStreamStartBit, (long)rec.BodyEnd * 8));

        body.WriteBS((short)cap.Flag);
        WriteCmc(body, fillRgb);
        body.WriteBD(cap.StartAngle);
        body.WriteBD(cap.EndAngle);
        body.WriteRC((byte)ls.Length);
        for (int i = 0; i < ls.Length; i++)
        {
            body.WriteBD(ls[i].Offset);
            WriteCmc(body, lineRgb[i]);
            body.WriteBS((short)ls[i].LtIndex);
        }

        long contentStart = body.BitPosition;
        WriteCentralTu(body, cap.Name);
        WriteCentralTu(body, cap.Description);
        int contentBits = (int)(body.BitPosition - contentStart);
        WriteStringStreamSize(body, (uint)contentBits);
        body.WriteB(true);

        int typeBits = (int)(rec.CarryStartBit - (long)rec.BodyStart * 8);
        int dataBits = (int)body.BitPosition - 32;
        body.PatchRL(0, (uint)(typeBits + 32 + dataBits));
        if (System.Environment.GetEnvironmentVariable("DWGDBG") == "1")
            System.Console.Error.WriteLine(
                $"MLS73 h={rec.Handle:X} AC1015->AC1021 RL={typeBits + 32 + dataBits} 数据区{dataBits}位 "
                + $"lines={ls.Length} name='{cap.Name}' xd={SourceXdicRefIsNull(src, rec.HandleStreamStartBit, (long)rec.BodyEnd * 8)}");
        return true;
    }

    private static uint UpgradedCmcRgb(int idx)
        => idx switch
        {
            256 => 0xC0000000u,
            0 => 0xC1000000u,
            257 => 0xC8000000u,
            >= 1 and <= 255 => 0xC3000000u | (uint)idx,
            _ => 0u,
        };

    private static void WriteCmc(BitWriter body, uint packed)
    {
        body.WriteBS(0);
        body.WriteBL(unchecked((int)packed));
        body.WriteRC(0);
    }
}
