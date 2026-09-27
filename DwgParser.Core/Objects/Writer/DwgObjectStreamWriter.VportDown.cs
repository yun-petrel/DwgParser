using DwgParser.Core.IO;

namespace DwgParser.Core.Objects;

public static partial class DwgObjectStreamWriter
{
    private static bool CarriesVportR2010Down(DwgObjectRecord rec, Target t)
        => t.Version == DwgVersion.AC1018
           && rec.SourceVersion.IsR2010Plus()
           && rec.LogicalType == TypeVport
           && rec.Vport is { ValuesCommitted: true, XrefResolved: 0 or 256,
               DownXrefEndBit: > 0, DownLightsStartBit: > 0, DownLightsEndBit: > 0,
               DownGridTailBit: > 0 } cap
           && !string.IsNullOrEmpty(cap.Name)
           && rec.StringStreamStartBit > 0;

    private static bool CarriesVportR2010ToAc1015(DwgObjectRecord rec, Target t)
        => t.Version == DwgVersion.AC1015
           && rec.SourceVersion.IsR2010Plus()
           && rec.LogicalType == TypeVport
           && rec.Vport is { ValuesCommitted: true, XrefResolved: 0 or 256,
               DownXrefEndBit: > 0, DownLightsStartBit: > 0, DownLightsEndBit: > 0,
               DownGridTailBit: > 0 } cap
           && !string.IsNullOrEmpty(cap.Name)
           && rec.StringStreamStartBit > 0
           && rec.HasHandleStream && rec.HandleStreamStartBit > 0;

    private static bool CarryVportR2010Down(BitWriter body, DwgObjectRecord rec, byte[] src,
        Target t)
        => CarryVportCore(body, rec, src, t, rec.CarryStartBit, rec.StringStreamStartBit);

    internal static bool CarryVportR2010ToAc1015(BitWriter body, DwgObjectRecord rec,
        byte[] src, Target t, out BitWriter? handles)
    {
        handles = null;
        if (!CarryVportCore(body, rec, src, t, rec.CarryStartBit, rec.StringStreamStartBit,
                ac1015: true))
            return false;
        long hsStart = rec.HandleStreamStartBit;
        long dataEnd = (long)rec.BodyEnd * 8;
        var srcHs = new BitWriter(16);
        if (!CopyVportHandlesDownToAc1018(srcHs, src, hsStart, dataEnd, rec)) return false;
        var shaped = srcHs.ToArray();
        if (HandleStreamHasXdicSlot(shaped, srcHs.BitPosition))
        {
            handles = srcHs;
            return true;
        }
        if (FindAfterNumReactors(rec, src, PrefixHasRl(rec.SourceVersion), out _, out uint nr) < 0)
            return false;
        if (nr > 64) return false;
        var hs = new BitWriter(16);
        if (!InsertXdicSlotAfterHead(srcHs, hs, nr + 1)) return false;
        handles = hs;
        return true;
    }

    private static bool CarryVportCore(BitWriter body, DwgObjectRecord rec, byte[] src,
        Target t, long start, long ssStart, bool ac1015 = false)
    {
        var cap = rec.Vport!;
        if (start <= 0 || ssStart <= start) return false;
        if (cap.DownXrefEndBit <= start || cap.DownXrefEndBit > cap.DownLightsStartBit
            || cap.DownLightsStartBit > cap.DownLightsEndBit
            || cap.DownLightsEndBit > cap.DownGridTailBit || cap.DownGridTailBit > ssStart)
            return false;
        try
        {
            var br = new BitReader(src, 0);
            DwgObjectRecord.SeekTo(br, start);
            br.ReadHandle();
            long eedStart = br.BitPosition;
            if (!SkipEedWriter(br)) return false;
            int eedLen = (int)(br.BitPosition - eedStart);
            long numReactors = br.ReadBL();
            if (numReactors is < 0 or > 10000) return false;
            bool xdic = br.ReadB();
            if (rec.SourceVersion >= DwgVersion.AC1027) br.ReadB();
            int resolved = br.ReadBS();
            if (resolved is not (0 or 256) || br.BitPosition != cap.DownXrefEndBit) return false;

            CopyBits(body, src, start, (int)(eedStart - start));
            if (!TryWriteEedPreR2007Range(body, src, eedStart, eedLen))
                CopyBits(body, src, eedStart, eedLen);
            body.WriteBL((int)numReactors);
            if (!ac1015) body.WriteB(xdic);

            body.WriteTV(cap.Name);

            body.WriteB(true);
            body.WriteBS((short)resolved);
            body.WriteB(resolved == 256);

            CopyBits(body, src, cap.DownXrefEndBit, (int)(cap.DownLightsStartBit - cap.DownXrefEndBit));

            CopyBits(body, src, cap.DownLightsEndBit, (int)(cap.DownGridTailBit - cap.DownLightsEndBit));

            if (DebugTrace)
                System.Console.Error.WriteLine(
                    $"VPORTDOWN h={rec.Handle:X} {rec.SourceVersion}→{t.Version} name='{cap.Name}' " +
                    $"xref=(1,{resolved},{resolved == 256}) nr={numReactors} xdic={xdic} " +
                    $"start={start} eed={eedStart}+{eedLen} xrefEnd={cap.DownXrefEndBit} " +
                    $"lights=[{cap.DownLightsStartBit},{cap.DownLightsEndBit}) " +
                    $"gridTail={cap.DownGridTailBit} ss={ssStart} out={body.BitPosition}");
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool CopyVportHandlesDownToAc1018(BitWriter handles, byte[] src,
        long hsStartBit, long dataEndBit, DwgObjectRecord rec)
    {
        var refs = new System.Collections.Generic.List<(long start, int bits, int code)>();
        var r = new BitReader(src);
        for (long hp = hsStartBit; hp + 8 <= dataEndBit; )
        {
            DwgObjectRecord.SeekTo(r, hp);
            int cc = r.ReadRC();
            int sz = cc & 0xF;
            int bits = 8 + sz * 8;
            if (hp + bits > dataEndBit) break;
            refs.Add((hp, bits, cc >> 4));
            hp += bits;
        }
        int[] expect = [4, 5, 4, 5, 3, 5, 5];
        if (refs.Count != expect.Length) return false;
        for (int i = 0; i < refs.Count; i++)
            if (refs[i].code != expect[i]) return false;
        foreach (int i in new[] { 0, 1, 5, 6 })
            CopyBits(handles, src, refs[i].start, refs[i].bits);
        if (DebugTrace)
            System.Console.Error.WriteLine(
                $"VPORTHS2 h={rec.Handle:X} {rec.SourceVersion}→AC1018 剥 R_2007a 三枚 [4,5,3] " +
                $"refs=7→4 hs={handles.BitPosition}位");
        return true;
    }
}
