using DwgParser.Core.IO;

namespace DwgParser.Core.Objects;

public static partial class DwgObjectStreamWriter
{
    private static bool CarriesRenderSettingsTvToAc1015(DwgObjectRecord rec, Target t)
        => t.Version == DwgVersion.AC1015
           && rec.SourceVersion == DwgVersion.AC1018
           && rec.RenderSettings is { Captured: true } rs
           && (rs.SettingsKind == "RAPIDRT" || rs.SettingsKind == "MENTALRAY")
           && rec.StringFields.Count > 0;

    private static bool CarryRenderSettingsTvToAc1015(BitWriter body, byte[] src, long from, long to,
        DwgObjectRecord rec, bool rlInBody)
    {
        var spans = new List<(long Start, int Bits, int Len)>();
        long cursor = from;
        foreach (var f in rec.StringFields)
        {
            if (f.Start < cursor || f.Start + f.BitLen > to) return false;
            if (f.BitLen == 0) { cursor = f.Start + f.BitLen; continue; }
            var probe = new BitReader(src, 0);
            DwgObjectRecord.SeekTo(probe, f.Start);
            int len = probe.ReadBS();
            if (len <= 0) { cursor = f.Start + f.BitLen; continue; }
            byte last = 0;
            for (int i = 0; i < len; i++) last = probe.ReadRC();
            if (probe.BitPosition - f.Start != f.BitLen) return false;
            if (last != 0x00) return false;
            spans.Add((f.Start, f.BitLen, len));
            cursor = f.Start + f.BitLen;
        }
        if (spans.Count == 0) return false;

        long pos = from;
        foreach (var (start, bits, len) in spans)
        {
            CopyBits(body, src, pos, (int)(start - pos));
            body.WriteBS(unchecked((short)(len - 1)));
            CopyBits(body, src, start + bits - (long)len * 8, (len - 1) * 8);
            pos = start + bits;
        }
        CopyBits(body, src, pos, (int)(to - pos));

        int stripped = spans.Count * 8;
        if (rlInBody && stripped > 0)
        {
            uint rlNow = new BitReader(body.ToArray(), 0).ReadRL();
            body.PatchRL(0, unchecked((uint)(rlNow - stripped)));
            if (DebugTrace)
                System.Console.Error.WriteLine(
                    $"RSTVDN15RL h={rec.Handle:X} RL {rlNow}→{rlNow - stripped}");
        }

        if (DebugTrace)
            System.Console.Error.WriteLine(
                $"RSTVDN15 h={rec.Handle:X} t={rec.TypeCode} {rec.SourceVersion}→AC1015 " +
                $"剥 {spans.Count} 枚尾 NUL（−{spans.Count * 8} 位）窗=[{from},{to})");
        return true;
    }


    private static int AppendRenderSettingsXdicSlotAc1015(BitWriter handles, int handlesBitLen,
        DwgObjectRecord rec)
    {
        if (HandleStreamHasXdicSlot(handles.ToArray(), handles.BitPosition)) return handlesBitLen;
        handles.WriteRC(0x30);
        if (DebugTrace)
            System.Console.Error.WriteLine(
                $"RSTVXD15 h={rec.Handle:X} t={rec.TypeCode} AC1018→AC1015 补 xdic 硬主槽 (3,0) " +
                $"hs {handlesBitLen}→{handles.BitPosition} 位");
        return (int)handles.BitPosition;
    }
}
