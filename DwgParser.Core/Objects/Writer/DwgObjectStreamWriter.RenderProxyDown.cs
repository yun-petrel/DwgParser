using DwgParser.Core.IO;

namespace DwgParser.Core.Objects;

public static partial class DwgObjectStreamWriter
{
    private static string? ProxyInlineNameFor(string dxfClassName) => dxfClassName switch
    {
        "RAPIDRTRENDERSETTINGS" => "cn:AcDbRapidRTRenderSettings",
        "MENTALRAYRENDERSETTINGS" => "cn:AcDbMentalRayRenderSettings",
        _ => null,
    };

    private const long ProxyVersionAc1018 = 0x69001F;

    internal static bool SynthesizesRenderSettingsProxy(DwgObjectRecord rec, Target t)
        => (t.Version == DwgVersion.AC1018 || t.Version == DwgVersion.AC1015)
           && rec.SourceVersion >= DwgVersion.AC1027
           && rec.ClassName is { Length: > 0 } cn && ProxyInlineNameFor(cn) is not null
           && RvProxyVar() != 0;

    private static int RvProxyVar()
    {
        var s = System.Environment.GetEnvironmentVariable("RVPROXY");
        return int.TryParse(s, out var v) ? v : 1;
    }

    internal static bool CarryRenderSettingsProxyDown(BitWriter body, DwgObjectRecord rec, byte[] src,
        Target t)
    {
        long start = rec.CarryStartBit;
        if (start <= 0 || !rec.HasHandleStream || rec.HandleStreamStartBit <= start) return false;
        if (rec.ClassName is not { Length: > 0 } cn0 || ProxyInlineNameFor(cn0) is not { Length: > 0 } inlineName)
            return false;
        bool ac1015 = t.Version == DwgVersion.AC1015;
        long hsSrc = rec.HandleStreamStartBit;
        if (hsSrc > (long)rec.BodyEnd * 8) return false;
        try
        {
            var br = new BitReader(src, 0);
            DwgObjectRecord.SeekTo(br, start);
            if (br.ReadHandle().Value == 0) return false;
            if (!DwgObjectReader.TrySkipEed(br)) return false;
            long nr = br.ReadBL();
            if (nr is < 0 or > 8) return false;
            long xmBit = ac1015 ? br.BitPosition : -1;
            br.ReadB();
            long dsBit = -1;
            if (rec.SourceVersion >= DwgVersion.AC1027) { dsBit = br.BitPosition; br.ReadB(); }
            long payloadStart = br.BitPosition;
            if (payloadStart >= hsSrc) return false;

            CopyHeaderSkipBits(body, src, start, payloadStart, xmBit, dsBit);

            body.WriteBL(rec.TypeCode);
            if (!ac1015) body.WriteTV(inlineName);
            body.WriteBL((int)ProxyVersionAc1018);
            body.WriteB(false);

            CopyBits(body, src, payloadStart, (int)(hsSrc - payloadStart));
            if (DebugTrace)
                System.Console.Error.WriteLine(
                    $"RVPXY h={rec.Handle:X} {rec.SourceVersion}→{t.Version} 源类={rec.TypeCode} " +
                    $"name=\"{inlineName}\" ver=0x{ProxyVersionAc1018:X} carry={start} xm={xmBit} ds={dsBit} " +
                    $"payload=[{payloadStart},{hsSrc})={hsSrc - payloadStart} 位 out={body.BitPosition} " +
                    $"var={RvProxyVar()}");
            return true;
        }
        catch { return false; }
    }

    private static void CopyHeaderSkipBits(BitWriter body, byte[] src, long from, long to, long a, long c)
    {
        long p = from;
        foreach (var cut in new[] { a, c })
        {
            if (cut < from || cut >= to) continue;
            CopyBits(body, src, p, (int)(cut - p));
            p = cut + 1;
        }
        CopyBits(body, src, p, (int)(to - p));
    }
}
