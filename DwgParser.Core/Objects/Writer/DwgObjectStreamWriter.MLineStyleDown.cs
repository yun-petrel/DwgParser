using DwgParser.Core.IO;

namespace DwgParser.Core.Objects;

public static partial class DwgObjectStreamWriter
{

    internal static bool SetHandles(ref BitWriter? handles, BitWriter? value)
    {
        handles = value;
        return true;
    }

    private static bool CarryAc1018NarrowGroup(DwgObjectRecord rec, Target t, BitWriter body, byte[] src,
        LtypeTable? ltypeTable, ref BitWriter? handles)
        => (DictionaryCarry.CarriesDictionaryAc1015Down(rec, t)
            && DictionaryCarry.CarryDictionaryAc1015Down(body, rec, src, t, out handles))
           || (DictionaryCarry.CarriesCentralStringAc1015Down(rec, t)
               && DictionaryCarry.CarryCentralStringAc1015Down(body, rec, src, out handles))
           || (DictionaryCarry.CarriesAc1021Down(rec, t)
               && DictionaryCarry.CarryDictionaryAc1021Down(body, rec, src, out handles))
           || (DictionaryCarry.CarriesDictDropDroppedEntriesDown(rec, t)
               && DictionaryCarry.CarryDictDropDroppedEntriesDown(body, rec, src, out handles))
           || (DictionaryCarry.CarriesDictR2010Ac1018Down(rec, t)
               && DictionaryCarry.CarryDictR2010Ac1018Down(body, rec, src, out handles))
           || (DictionaryCarry.CarriesCentralStringAc1021Down(rec, t)
               && DictionaryCarry.CarryCentralStringAc1021Down(body, rec, src,
                   DictionaryCarry.CentralStringValue(rec), out handles))
           || (CarriesAppidR2007Down(rec, t) && CarryAppidR2007Down(body, rec, src, t, out handles))
           || (CarriesCellStyleMapAc1021Down(rec, t)
               && CarryCellStyleMapAc1021Down(body, rec, src, out handles))
           || (CarriesCellStyleMapAc1021ToAc1015Down(rec, t)
               && CarryCellStyleMapAc1021ToAc1015Down(body, rec, src, out handles))
           || (CarriesMLeaderStyleAc1021Down(rec, t) && CarryMLeaderStyleAc1021Down(body, rec, src, t))
           || (CarriesMLeaderStyleAc1021ToAc1015(rec, t)
               && CarryMLeaderStyleAc1021ToAc1015(body, rec, src, t, out handles))
           || (CarriesScaleAc1021Down(rec, t) && CarryScaleAc1021Down(body, rec, src, t, out handles))
           || (CarriesScaleAc1021ToAc1015Down(rec, t)
               && CarryScaleAc1021ToAc1015Down(body, rec, src, t, out handles))
           || (CarriesBlockHeaderAc1021Down(rec, t)
               && CarryBlockHeaderAc1021Down(body, rec, src, t, out handles))
           || (CarriesLayoutAc1021Down(rec, t)
               && CarryLayoutAc1021Down(body, rec, src, t, out handles))
           || (CarriesVportAc1021Down(rec, t)
               && CarryVportAc1021Down(body, rec, src, t, out handles))
           || (CarriesVportAc1021ToAc1015(rec, t)
               && CarryVportAc1021ToAc1015(body, rec, src, t, out handles))
           || (CarriesMaterialAc1021Down(rec, t)
               && CarryMaterialAc1021Down(body, rec, src, t, out handles))
           || (CarriesMLineStyleAc1021Down(rec, t)
               && CarryMLineStyleAc1021Down(body, rec, src, t, out handles))
           || (CarriesVisualStyleAc1021Down(rec, t) && CarryVisualStyleAc1021Down(body, rec, src, t))
           || (CarriesVisualStyleR2010Down(rec, t) && CarryVisualStyleR2010Down(body, rec, src, t))
           || (DictionaryCarry.CarriesNoStringFrameAc1021Down(rec, t)
               && DictionaryCarry.CarryNoStringFrameAc1021Down(body, rec, src, out handles))
           || (CarriesLayerR2010ToAc1015(rec, t)
               && CarryLayerR2010ToAc1015(body, rec, src, t, out var lay15Handles)
               && SetHandles(ref handles, lay15Handles))
           || (CarriesVportR2010ToAc1015(rec, t)
               && CarryVportR2010ToAc1015(body, rec, src, t, out var vp15Handles)
               && SetHandles(ref handles, vp15Handles))
           || (CarriesVportR2010Down(rec, t) && CarryVportR2010Down(body, rec, src, t))
           || (CarriesDimStyleR2010Down(rec, t) && CarryDimStyleR2010Down(body, rec, src, t))
           || (CarriesDimStyleR2010ToAc1015(rec, t)
               && CarryDimStyleR2010ToAc1015(body, rec, src, t, out var ds15Handles)
               && SetHandles(ref handles, ds15Handles))
           || (CarriesDimStyleAc1018ToAc1015(rec, t)
               && CarryDimStyleAc1018ToAc1015(body, rec, src, t, out handles))
           || (CarriesMaterialR2010ToAc1015(rec, t)
               && CarryMaterialR2010ToAc1015(body, rec, src, t, out var mat15Handles)
               && SetHandles(ref handles, mat15Handles))
           || (CarriesMaterialAc1021ToAc1015(rec, t)
               && CarryMaterialAc1021ToAc1015(body, rec, src, t, out var mat21Handles)
               && SetHandles(ref handles, mat21Handles))
           || (CarriesMaterialR2010Down(rec, t) && CarryMaterialR2010Down(body, rec, src, t))
           || (CarriesMLineStyleR2010ToAc1015(rec, t)
               && CarryMLineStyleR2010ToAc1015(body, rec, src, t, ltypeTable, out var mls15Handles)
               && SetHandles(ref handles, mls15Handles))
           || (CarriesMLineStyleR2010Down(rec, t)
               && (rec.SourceVersion >= DwgVersion.AC1032
                   ? CarryMLineStyleAc1032ToAc1018(body, rec, src, ltypeTable)
                   : CarryMLineStyleR2010Down(body, rec, src, t)));

    private static bool CarriesMLineStyleR2010Down(DwgObjectRecord rec, Target t)
        => t.Version == DwgVersion.AC1018
           && rec.SourceVersion.IsR2010Plus()
           && rec.LogicalType == TypeMLineStyle
           && rec.StringStreamStartBit > 0;

    private static bool CarryMLineStyleAc1032ToAc1018(BitWriter body, DwgObjectRecord rec,
        byte[] src, LtypeTable? ltypeTable)
    {
        long start = rec.CarryStartBit;
        long ssStart = rec.StringStreamStartBit;
        if (start <= 0 || ssStart <= start || ltypeTable is null) return false;
        try
        {
            var br = new BitReader(src, 0);
            DwgObjectRecord.SeekTo(br, start);
            br.ReadHandle();
            long eedStart = br.BitPosition;
            if (!SkipEedWriter(br)) return false;
            int eedLen = (int)(br.BitPosition - eedStart);
            long numReactors = br.ReadBL();
            if (numReactors is < 0 or > 8) return false;
            bool xdic = br.ReadB();
            br.ReadB();
            long numericStart = br.BitPosition;
            br.ReadBS(); SkipCmcMainstream(br);
            br.ReadBD(); br.ReadBD();
            int numLines = br.ReadRC();
            if (numLines is < 1 or > 256) return false;
            var lineEnd = new long[numLines];
            for (int i = 0; i < numLines; i++)
            {
                br.ReadBD(); SkipCmcMainstream(br);
                lineEnd[i] = br.BitPosition;
            }
            if (br.BitPosition != ssStart) return false;
            string name, desc;
            {
                var sr = new BitReader(src) { UnicodeStrings = true };
                DwgObjectRecord.SeekTo(sr, ssStart);
                name = sr.ReadTV();
                desc = sr.ReadTV();
            }
            if (name.Length == 0 || name.Length > 255 || desc.Length > 255) return false;

            CopyBits(body, src, start, (int)(eedStart - start));
            if (!TryWriteEedPreR2007Range(body, src, eedStart, eedLen))
                CopyBits(body, src, eedStart, eedLen);
            body.WriteBL((int)numReactors);
            body.WriteB(xdic);
            body.WriteTV(name);
            body.WriteTV(desc);
            long cursor = numericStart;
            for (int i = 0; i < numLines; i++)
            {
                CopyBits(body, src, cursor, (int)(lineEnd[i] - cursor));
                body.WriteBS((short)MLineStyleLineIndex(src, rec, i, ltypeTable));
                cursor = lineEnd[i];
            }
            if (DebugTrace)
                System.Console.Error.WriteLine(
                    $"MLINE32DN h={rec.Handle:X} AC1032→AC1018 name='{name}' lines={numLines} " +
                    $"num=[{numericStart},{ssStart}) out={body.BitPosition}");
            return true;
        }
        catch { return false; }
    }

    private static int MLineStyleLineIndex(byte[] src, DwgObjectRecord rec, int i, LtypeTable table)
    {
        var r = ReadMLineStyleTailRefs(src, rec, out int count);
        var h = r[i];
        return LtypeIndexForHandle(h, table);
    }

    private static ulong[] ReadMLineStyleTailRefs(byte[] src, DwgObjectRecord rec, out int count)
    {
        var hdr = new BitReader(src, 0);
        DwgObjectRecord.SeekTo(hdr, rec.CarryStartBit);
        hdr.ReadHandle();
        if (!SkipEedWriter(hdr)) throw new InvalidDataException("mls eed");
        long numReactors = hdr.ReadBL();
        if (numReactors is < 0 or > 8) throw new InvalidDataException("mls nr");
        bool xdicMissing = hdr.ReadB();
        hdr.ReadB();
        hdr.ReadBS(); SkipCmcMainstream(hdr); hdr.ReadBD(); hdr.ReadBD();
        int numLines = hdr.ReadRC();
        if (numLines is < 1 or > 256) throw new InvalidDataException("mls lines");

        var hr = new BitReader(src, 0);
        DwgObjectRecord.SeekTo(hr, rec.HandleStreamStartBit);
        var prefix = new List<(int Code, ulong Value)>(2);
        for (int k = 0; k < 1 + (int)numReactors + (xdicMissing ? 0 : 1); k++)
        {
            var p = hr.ReadHandle();
            prefix.Add((p.Code, p.Value));
        }
        var tail = new ulong[numLines];
        for (int i = 0; i < numLines; i++)
        {
            var p = hr.ReadHandle();
            if (p.Code != 5) throw new InvalidDataException("mls tail ref code");
            tail[i] = p.Resolve(rec.Handle);
        }
        count = numLines;
        return tail;
    }

    private static bool RebuildMLineStyleHandlesAc1032ToAc1018(BitWriter handles, byte[] src,
        long hsStartBit, long dataEndBit, DwgObjectRecord rec)
    {
        try
        {
            ReadMLineStyleTailRefs(src, rec, out _);
            var hr = new BitReader(src, 0);
            DwgObjectRecord.SeekTo(hr, hsStartBit);
            var hdr = new BitReader(src, 0);
            DwgObjectRecord.SeekTo(hdr, rec.CarryStartBit);
            hdr.ReadHandle();
            SkipEedWriter(hdr);
            int prefixCount = 1 + (int)hdr.ReadBL() + (hdr.ReadB() ? 0 : 1);
            hdr.ReadB();
            for (int k = 0; k < prefixCount; k++)
            {
                long refStart = hr.BitPosition;
                var p = hr.ReadHandle();
                CopyBits(handles, src, refStart, (int)(hr.BitPosition - refStart));
                _ = p;
            }
            if (DebugTrace)
                System.Console.Error.WriteLine(
                    $"MLINE32HS h={rec.Handle:X} AC1032→AC1018 剥尾行引用 hs={handles.BitPosition}位");
            return true;
        }
        catch { return false; }
    }


    private static bool CarryMLineStyleR2010Down(BitWriter body, DwgObjectRecord rec, byte[] src,
        Target t)
        => CarryMLineStyleCore(body, rec, src, t, rec.CarryStartBit, rec.StringStreamStartBit,
                               rec.HandleStreamStartBit);

    private static bool CarryMLineStyleCore(BitWriter body, DwgObjectRecord rec, byte[] src,
        Target t, long start, long ssStart, long footerEnd)
    {
        if (start <= 0 || ssStart <= start || footerEnd <= ssStart) return false;
        try
        {
            var br = new BitReader(src, 0);
            DwgObjectRecord.SeekTo(br, start);
            br.ReadHandle();
            long eedStart = br.BitPosition;
            if (!SkipEedWriter(br)) return false;
            int eedLen = (int)(br.BitPosition - eedStart);
            long numReactors = br.ReadBL();
            if (numReactors is < 0 or > 8) return false;
            bool xdic = br.ReadB();
            if (rec.SourceVersion >= DwgVersion.AC1027) br.ReadB();
            long numericStart = br.BitPosition;
            if (numericStart > ssStart) return false;

            string name, desc;
            {
                var sr = new BitReader(src) { UnicodeStrings = true };
                DwgObjectRecord.SeekTo(sr, ssStart);
                name = sr.ReadTV();
                desc = sr.ReadTV();
                if (sr.BitPosition + 17 != footerEnd) return false;
            }
            if (name.Length == 0 || name.Length > 255 || desc.Length > 255) return false;

            CopyBits(body, src, start, (int)(eedStart - start));
            if (!TryWriteEedPreR2007Range(body, src, eedStart, eedLen))
                CopyBits(body, src, eedStart, eedLen);
            body.WriteBL((int)numReactors);
            body.WriteB(xdic);
            body.WriteTV(name);
            body.WriteTV(desc);
            CopyBits(body, src, numericStart, (int)(ssStart - numericStart));

            if (DebugTrace)
                System.Console.Error.WriteLine(
                    $"MLINE_DN h={rec.Handle:X} {rec.SourceVersion}→{t.Version} name='{name}' desc='{desc}' " +
                    $"nr={numReactors} xdic={xdic} num=[{numericStart},{ssStart}) out={body.BitPosition}");
            return true;
        }
        catch
        {
            return false;
        }
    }
}
