using DwgParser.Core.IO;
using static DwgParser.Core.Objects.DwgObjectStreamWriter;

namespace DwgParser.Core.Objects.Records;

internal sealed class MLeaderStyleCarryAcR2010Down : MLeaderStyleCarryPlan
{
    public override string Name => "R2010+→AC1021";

    public override bool IsCaptured(DwgObjectRecord rec) => rec.MLeader?.DownCaptured == true;

    internal override bool TryCarry(BitWriter body, byte[] src, long from, long to,
        DwgObjectRecord rec, Target t)
    {
        var ml = rec.MLeader;
        if (System.Environment.GetEnvironmentVariable("DWGDBG") == "1")
            System.Console.Error.WriteLine(
                $"MLS21DN? h={rec.Handle:X} cap={ml?.DownCaptured.ToString()} from={from} to={to} " +
                $"own={rec.CarryStartBit} headEnd={ml?.DownHeadEndBit} " +
                $"fields={ml?.DownFieldsStartBit} attach={ml?.DownAttachStartBit} " +
                $"ss={ml?.DownStreamStartBit} hs={rec.HandleStreamStartBit}");
        if (ml is not { DownCaptured: true }) return false;
        long ownStart = rec.CarryStartBit;
        long headEnd = ml.DownHeadEndBit;
        long fieldsStart = ml.DownFieldsStartBit;
        long attachStart = ml.DownAttachStartBit;
        long ssStart = ml.DownStreamStartBit;
        if (to != rec.HandleStreamStartBit) return false;
        if (ownStart >= headEnd || headEnd >= fieldsStart
            || fieldsStart >= attachStart || attachStart >= ssStart || ssStart > to)
            return false;
        if (from < ownStart || from > headEnd) return false;

        CopyBits(body, src, from, (int)(headEnd - from));
        CopyBits(body, src, fieldsStart, (int)(attachStart - fieldsStart));
        CopyBits(body, src, ssStart, (int)(to - ssStart));

        if (System.Environment.GetEnvironmentVariable("DWGDBG") == "1")
            System.Console.Error.WriteLine(
                $"MLS21DN h={rec.Handle:X} {rec.SourceVersion}→{t.Version} " +
                $"剥cv{fieldsStart - headEnd}+attach{ssStart - attachStart}位 " +
                $"数据区{body.BitPosition}位 from={from} to={to} own={ownStart} " +
                $"headEnd={headEnd} fields={fieldsStart} attach={attachStart} ss={ssStart}");
        return true;
    }
}
