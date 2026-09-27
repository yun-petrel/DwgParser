using DwgParser.Core.IO;
using DwgParser.Core.Objects.Records;

namespace DwgParser.Core.Objects;

public static partial class DwgObjectStreamWriter
{
    private static bool CarriesVisualStyleR2010Down(DwgObjectRecord rec, Target t)
        => t.Version == DwgVersion.AC1018
           && rec.SourceVersion.IsR2010Plus()
           && rec.LogicalType == TypeVisualStyle
           && rec.VisualStyle is { DownCaptured: true }
           && rec.HasHandleStream;

    private static bool CarryVisualStyleR2010Down(BitWriter body, DwgObjectRecord rec, byte[] src,
        Target t)
    {
        var cap = rec.VisualStyle;
        if (cap == null) return false;
        long start = rec.CarryStartBit;
        long body0 = cap.DownBodyStartBit;
        long ss = cap.DownStreamStartBit;
        long hs = rec.HandleStreamStartBit;
        if (start <= 0 || body0 <= start || ss <= body0 || hs <= ss) return false;
        if (cap.DownFieldSpans.Length != 18 || cap.DownScalars.Length != 20) return false;
        bool r2013Plus = rec.SourceVersion >= DwgVersion.AC1027;
        try
        {
            var br = new BitReader(src, 0);
            DwgObjectRecord.SeekTo(br, start);
            br.ReadHandle();
            long eedStart = br.BitPosition;
            if (!SkipEedWriter(br)) return false;
            int eedLen = (int)(br.BitPosition - eedStart);
            int numReactors = br.ReadBL();
            if (numReactors is < 0 or > 8) return false;
            bool xdic = br.ReadB();
            if (r2013Plus) br.ReadB();
            if (br.BitPosition != body0) return false;

            string desc;
            {
                var sr = new BitReader(src, 0) { UnicodeStrings = true };
                DwgObjectRecord.SeekTo(sr, ss);
                desc = sr.ReadTV();
                if (r2013Plus) sr.ReadTV();
                if (sr.BitPosition + 17 != hs) return false;
            }
            if (desc.Length == 0 || desc.Length > 255) return false;

            CopyBits(body, src, start, (int)(eedStart - start));
            if (!TryWriteEedPreR2007Range(body, src, eedStart, eedLen))
                CopyBits(body, src, eedStart, eedLen);
            body.WriteBL(numReactors);
            body.WriteB(xdic);
            body.WriteTV(desc);
            WriteVisualStylePreFields(body, src, cap, withBd2007: false);

            if (DebugTrace)
                System.Console.Error.WriteLine(
                    $"VSDN1018 h={rec.Handle:X} {rec.SourceVersion}→{t.Version} desc='{desc}' " +
                    $"nr={numReactors} xdic={xdic} 剥props+串区{hs - body0}位 out={body.BitPosition}位");
            return true;
        }
        catch { return false; }
    }

    internal static void WriteVisualStylePreFields(BitWriter body, byte[] src,
        VisualStyleCapture cap, bool withBd2007, bool cmcShortForm = false)
    {
        var sp = cap.DownFieldSpans;
        var sc = cap.DownScalars;
        void CopySpan(int i) => CopyBits(body, src, sp[i * 2], (int)(sp[i * 2 + 1] - sp[i * 2]));
        void CmcField(int i, CmcTriple v)
        {
            if (cmcShortForm) body.WriteBS(FoldCmcToPreR2004Index(v.Rgb));
            else CopySpan(i);
        }
        body.WriteBL(sc[0]);
        body.WriteBL(sc[1]);
        body.WriteBL(sc[2]);
        body.WriteBL(sc[3]);
        CopySpan(0);
        CopySpan(1);
        CmcField(2, cap.FaceMonoColor);
        body.WriteBL(sc[4]);
        body.WriteBL(sc[5]);
        body.WriteBL(sc[6]);
        CmcField(3, cap.EdgeIntersectionColor);
        CmcField(4, cap.EdgeObscuredColor);
        body.WriteBL(sc[7]);
        CopySpan(5);
        body.WriteBL(sc[9]);
        CmcField(6, cap.EdgeColor);
        CopySpan(7);
        body.WriteBS((short)sc[10]);
        body.WriteBS((short)sc[11]);
        body.WriteBL(sc[12]);
        CmcField(8, cap.EdgeSilhouetteColor);
        body.WriteBS((short)sc[13]);
        body.WriteRC((byte)sc[14]);
        body.WriteBS((short)sc[15]);
        body.WriteB(sc[19] != 0);
        body.WriteBS(0);
        body.WriteBS((short)sc[8]);
        body.WriteBL(sc[16]);
        {
            int bl = unchecked((int)cap.DownBrightness);
            if (bl == 0) body.WriteBB(2);
            else if ((uint)bl <= 255) { body.WriteBB(1); body.WriteRC((byte)bl); }
            else { body.WriteBB(0); body.WriteRL(bl); }
        }
        body.WriteBL(sc[17]);
        if (withBd2007) body.WriteBD(0.0);
        body.WriteB(sc[18] != 0);
    }
}
